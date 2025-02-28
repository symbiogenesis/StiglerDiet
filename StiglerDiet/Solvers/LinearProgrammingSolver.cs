namespace StiglerDiet.Solvers;

using MathNet.Numerics.LinearAlgebra;
using StiglerDiet.Solvers.Interfaces;
using System;
using System.Collections.Generic;

/// <summary>
/// Glop-style barrier method solver
/// </summary>
public class LinearProgrammingSolver : ISolver
{
    private double _wallTime;
    private int _iterations;

    private const double Tolerance = 1e-7;
    private const double LowerBoundThreshold = 1e-9;
    private const double MinStepSize = 1e-12;
    private const int MaxIterations = 1000;
    private const int InnerIterations = 50;

    private static readonly int processorCount = Environment.ProcessorCount;
    private readonly ParallelOptions _parallelOptions = new() { MaxDegreeOfParallelism = processorCount };
    private readonly ParallelOptions _lightParallelOptions = new() { MaxDegreeOfParallelism = Math.Max(1, processorCount / 2) };


    public List<Variable> Variables { get; } = [];
    public List<Constraint> Constraints { get; } = [];
    public Objective Objective { get; } = new();

    public Variable MakeNumVar(double lb, double ub, string name)
    {
        var v = new Variable(name, lb, ub);
        Variables.Add(v);
        return v;
    }

    public Constraint MakeConstraint(double lb, double ub, string name)
    {
        var c = new Constraint(name, lb, ub);
        Constraints.Add(c);
        return c;
    }

    public ResultStatus Solve()
    {
        var startTime = DateTime.UtcNow;
        
        // Quick validation
        if (Constraints.Count == 0 || Variables.Count == 0)
        {
            _wallTime = (DateTime.UtcNow - startTime).TotalSeconds;
            return ResultStatus.INFEASIBLE;
        }

        // Setup problem and run solver
        int numVars = Variables.Count;
        var varIndices = CreateVariableIndices();
        var (constraintMatrix, rhsValues) = CreateConstraintSystem(varIndices);
        var objectiveVector = CreateObjectiveVector(varIndices);
        var solution = SolveWithBarrierMethod(constraintMatrix, rhsValues, objectiveVector);
        
        // Set the solution values
        for (int i = 0; i < numVars; i++)
            Variables[i].Solution = Math.Abs(solution[i]) < Tolerance ? 0.0 : solution[i];

        _wallTime = (DateTime.UtcNow - startTime).TotalSeconds;
        return ResultStatus.OPTIMAL;
    }
    
    private Dictionary<Variable, int> CreateVariableIndices()
    {
        var indices = new Dictionary<Variable, int>(Variables.Count);
        for (int i = 0; i < Variables.Count; i++)
            indices[Variables[i]] = i;
        return indices;
    }
    
    private (Matrix<double>, Vector<double>) CreateConstraintSystem(Dictionary<Variable, int> varIndices)
    {
        var rows = new List<double[]>();
        var rhs = new List<double>();
        int n = Variables.Count;

        // Process user constraints (both lower and upper bounds)
        foreach (var constraint in Constraints)
        {
            AddConstraintBounds(constraint, varIndices, rows, rhs, n);
        }

        // Add variable upper bounds
        foreach (var variable in Variables)
        {
            if (!double.IsPositiveInfinity(variable.UpperBound))
            {
                var row = new double[n];
                row[varIndices[variable]] = -1.0;
                rows.Add(row);
                rhs.Add(-variable.UpperBound);
            }
        }

        return (Matrix<double>.Build.DenseOfRowArrays(rows), 
                Vector<double>.Build.DenseOfEnumerable(rhs));
    }
    
    private void AddConstraintBounds(Constraint constraint, Dictionary<Variable, int> varIndices, 
                                    List<double[]> rows, List<double> rhs, int n)
    {
        // Add lower bound constraint if it exists
        if (!double.IsNegativeInfinity(constraint.LowerBound))
        {
            var row = new double[n];
            foreach (var (variable, coefficient) in constraint.Coefficients)
                row[varIndices[variable]] = coefficient;
            rows.Add(row);
            rhs.Add(constraint.LowerBound);
        }

        // Add upper bound constraint if it exists
        if (!double.IsPositiveInfinity(constraint.UpperBound))
        {
            var row = new double[n];
            foreach (var (variable, coefficient) in constraint.Coefficients)
                row[varIndices[variable]] = -coefficient;
            rows.Add(row);
            rhs.Add(-constraint.UpperBound);
        }
    }
    
    private Vector<double> CreateObjectiveVector(Dictionary<Variable, int> varIndices)
    {
        double sign = Objective.IsMinimization ? 1.0 : -1.0;
        var objVector = Vector<double>.Build.Dense(Variables.Count);
        
        foreach (var (variable, coefficient) in Objective.Coefficients)
            objVector[varIndices[variable]] = sign * coefficient;
            
        return objVector;
    }

    private Vector<double> SolveWithBarrierMethod(Matrix<double> A, Vector<double> b, Vector<double> c)
    {
        int n = c.Count;
        int m = b.Count;
        
        // Initialize solver state
        double t = 1.0;
        double mu = 2.0;
        var x = Vector<double>.Build.Dense(n, 1.0);
        var slacks = Vector<double>.Build.Dense(m);
        var grad = Vector<double>.Build.Dense(n);
        var candidate = Vector<double>.Build.Dense(n);
        var lbVector = Vector<double>.Build.Dense(n, LowerBoundThreshold);
        var tempSlacks = Vector<double>.Build.Dense(m); // Preallocate the temporary vector here
        _iterations = 0;
        
        // Main barrier method loop
        while (_iterations < MaxIterations && m / t >= Tolerance)
        {
            for (int i = 0; i < InnerIterations && _iterations < MaxIterations; i++)
            {
                _iterations++;
                
                // Update slacks = Ax - b
                ComputeSlacks(A, x, b, slacks);
                
                // Check slack feasibility
                if (!AreSlacksPositive(slacks))
                    break;
                    
                // Compute gradient
                ComputeGradient(A, c, slacks, t, grad);
                
                if (grad.L2Norm() < Tolerance)
                    break;
                
                // Line search and update x
                double step = PerformLineSearch(x, grad, slacks, A, b, c, t, candidate, lbVector, tempSlacks);
                if (step < MinStepSize)
                    break;
                
                UpdateSolution(x, grad, step, lbVector, candidate);
                candidate.CopyTo(x);
            }
            
            t *= mu; // Increase barrier parameter
        }
        
        return x;
    }

    private double PerformLineSearch(Vector<double> x, Vector<double> grad, Vector<double> slacks, 
                                        Matrix<double> A, Vector<double> b, Vector<double> c, double t,
                                        Vector<double> candidate, Vector<double> lbVector, Vector<double> tempSlacks)
    {
        double step = 1.0;
        double currentValue = ComputeBarrierValue(x, slacks, c, t);
        // Reuse the preallocated vector instead of creating a new one
        
        while (step >= MinStepSize)
        {
            // Try new candidate solution
            UpdateSolution(x, grad, step, lbVector, candidate);
            
            // Compute candidate slacks
            ComputeSlacks(A, candidate, b, tempSlacks);
            
            if (AreSlacksPositive(tempSlacks))
            {
                double candidateValue = ComputeBarrierValue(candidate, tempSlacks, c, t);
                double descent = grad.DotProduct(x.Subtract(candidate));
                
                if (candidateValue <= currentValue + 0.25 * step * descent)
                    return step;
            }
            
            step *= 0.5; // Try smaller step
        }
        
        return 0.0;
    }

    private static void ComputeSlacks(Matrix<double> A, Vector<double> x, Vector<double> b, Vector<double> slacks)
    {
        A.Multiply(x, slacks);
        slacks.Subtract(b, slacks);
    }

    private static bool AreSlacksPositive(Vector<double> slacks)
    {
        int count = slacks.Count;
        int i = 0;
        
        // Get direct access to data if possible
        if (slacks.Storage is MathNet.Numerics.LinearAlgebra.Storage.DenseVectorStorage<double> denseStorage)
        {
            double[] data = denseStorage.Data;
            
            // Process in chunks for better cache utilization
            const int chunkSize = 128;
            for (; i + chunkSize <= count; i += chunkSize)
            {
                for (int j = 0; j < chunkSize; j++)
                {
                    if (data[i + j] <= 0.0)
                        return false;
                }
            }
            
            // Handle remaining elements
            for (; i < count; i++)
            {
                if (data[i] <= 0.0)
                    return false;
            }
        }
        else
        {
            // Fallback for non-dense storage
            for (i = 0; i < count; i++)
            {
                if (slacks[i] <= 0.0)
                    return false;
            }
        }
        
        return true;
    }

    private void ComputeGradient(Matrix<double> A, Vector<double> c, Vector<double> slacks,
                                    double t, Vector<double> grad)
    {
        // Initialize gradient with objective
        c.CopyTo(grad);
        grad.Multiply(t, grad);

        int slackCount = slacks.Count;
        int gradCount = grad.Count;

        // Pre-compute all slack inverses once (major optimization)
        var invSlacks = new double[slackCount];
        for (int j = 0; j < slackCount; j++)
            invSlacks[j] = -1.0 / slacks[j];

        // Attempt direct access to matrix data for maximum performance
        if (A.Storage is MathNet.Numerics.LinearAlgebra.Storage.DenseColumnMajorMatrixStorage<double> denseCMStorage)
        {
            try
            {
                var data = denseCMStorage.Data;
                int vectorSize = System.Numerics.Vector<double>.Count;

                // Parallelize the outer loop (gradient elements)
                Parallel.For(0, gradCount, _parallelOptions, k =>
                {
                    double sum = 0.0;
                    int colOffset = k * slackCount; // Offset for the current column

                    // Use SIMD vectorization within the inner loop
                    int j = 0;
                    for (; j <= slackCount - vectorSize; j += vectorSize)
                    {
                        var matrixVec = new System.Numerics.Vector<double>(data, colOffset + j);
                        var slackVec = new System.Numerics.Vector<double>(invSlacks, j);
                        sum += System.Numerics.Vector.Dot(matrixVec, slackVec);
                    }

                    // Handle remaining elements (if slackCount is not a multiple of vectorSize)
                    for (; j < slackCount; j++)
                    {
                        sum += data[colOffset + j] * invSlacks[j];
                    }

                    grad[k] += sum;
                });
                return; // Exit after successful SIMD processing
            }
            catch
            {
                // Fallback if direct access fails
            }
        }

        // Fallback: If direct access is not possible, use the original logic (or a simplified version)
        for (int k = 0; k < gradCount; k++)
        {
            double sum = 0.0;
            for (int j = 0; j < slackCount; j++)
            {
                sum += A[j, k] * invSlacks[j];
            }
            grad[k] += sum;
        }
    }

    private static void UpdateSolution(Vector<double> x, Vector<double> grad, double step,
                                      Vector<double> lbVector, Vector<double> result)
    {
        x.Subtract(step * grad, result);
        result.PointwiseMaximum(lbVector, result);
    }

    private double ComputeBarrierValue(Vector<double> x, Vector<double> slacks, Vector<double> c, double t)
    {
        double objectiveTerm = t * c.DotProduct(x);
        double barrierTerm = 0.0;
        int m = slacks.Count;
        
        // Check if we can use direct data access for slacks
        if (slacks.Storage is MathNet.Numerics.LinearAlgebra.Storage.DenseVectorStorage<double> denseSlacks)
        {
            double[] slackData = denseSlacks.Data;
            
            // For large problems, use parallel processing with chunking
            if (m > 500)
            {
                int chunkSize = 512;
                int chunks = (m + chunkSize - 1) / chunkSize;
                double[] partialSums = new double[chunks];
                
                Parallel.For(0, chunks, _lightParallelOptions, chunkIndex =>
                {
                    int start = chunkIndex * chunkSize;
                    int end = Math.Min(start + chunkSize, m);
                    double localSum = 0.0;
                    
                    for (int i = start; i < end; i++)
                    {
                        // Fast path using direct array access
                        localSum -= Math.Log(slackData[i]);
                    }
                    
                    partialSums[chunkIndex] = localSum;
                });
                
                // Sum up partial results
                for (int i = 0; i < chunks; i++)
                    barrierTerm += partialSums[i];
            }
            else
            {
                // Sequential processing for small problems
                for (int i = 0; i < m; i++)
                    barrierTerm -= Math.Log(slackData[i]);
            }
        }
        else
        {
            // Fallback for non-dense storage
            if (m > 500)
            {
                int chunkSize = 512;
                int chunks = (m + chunkSize - 1) / chunkSize;
                double[] partialSums = new double[chunks];
                
                Parallel.For(0, chunks, chunkIndex =>
                {
                    int start = chunkIndex * chunkSize;
                    int end = Math.Min(start + chunkSize, m);
                    double localSum = 0.0;
                    
                    for (int i = start; i < end; i++)
                        localSum -= Math.Log(slacks[i]);
                    
                    partialSums[chunkIndex] = localSum;
                });
                
                for (int i = 0; i < chunks; i++)
                    barrierTerm += partialSums[i];
            }
            else
            {
                for (int i = 0; i < m; i++)
                    barrierTerm -= Math.Log(slacks[i]);
            }
        }
        
        return objectiveTerm + barrierTerm;
    }

    public double WallTime() => _wallTime;
    public long Iterations() => _iterations;
    public int NumVariables() => Variables.Count;
    public int NumConstraints() => Constraints.Count;

    public void Dispose()
    {
        // Do nothing.
    }
}