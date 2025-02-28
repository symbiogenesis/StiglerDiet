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
                double step = PerformLineSearch(x, grad, slacks, A, b, c, t, candidate, lbVector);
                if (step < MinStepSize)
                    break;
                
                UpdateSolution(x, grad, step, lbVector, candidate);
                candidate.CopyTo(x);
            }
            
            t *= mu; // Increase barrier parameter
        }
        
        return x;
    }

    private static void ComputeSlacks(Matrix<double> A, Vector<double> x, Vector<double> b, Vector<double> slacks)
    {
        A.Multiply(x, slacks);
        slacks.Subtract(b, slacks);
    }

    private static bool AreSlacksPositive(Vector<double> slacks)
    {
        for (int i = 0; i < slacks.Count; i++)
        {
            if (slacks[i] <= 0.0)
                return false;
        }
        return true;
    }

    private static void ComputeGradient(Matrix<double> A, Vector<double> c, Vector<double> slacks, 
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
        
        // Specialized fast path for dense column-major storage
        if (A.Storage is MathNet.Numerics.LinearAlgebra.Storage.DenseColumnMajorMatrixStorage<double> denseCMStorage)
        {
            // Access underlying storage directly when possible
            try
            {
                var data = denseCMStorage.Data;
                var invSlacksVector = Vector<double>.Build.Dense(invSlacks);
                
                if (slackCount > 100 && gradCount > 100)
                {
                    // For very large matrices, use built-in matrix operations
                    var ATinvSlacks = A.TransposeThisAndMultiply(invSlacksVector);
                    grad.Add(ATinvSlacks, grad);
                    return;
                }
                else
                {
                    // For medium-sized problems, use direct memory access with SIMD optimization
                    int vectorSize = System.Numerics.Vector<double>.Count;
                    
                    // Process columns in the optimal order for memory access
                    Parallel.For(0, gradCount, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, k =>
                    {
                        double sum = 0.0;
                        int colOffset = k * slackCount;
                        int j = 0;
                        
                        // Use SIMD vectorization where possible
                        for (; j + vectorSize <= slackCount; j += vectorSize)
                        {
                            var matrixVec = new System.Numerics.Vector<double>(data, colOffset + j);
                            var slackVec = new System.Numerics.Vector<double>(invSlacks, j);
                            sum += System.Numerics.Vector.Dot(matrixVec, slackVec);
                        }
                        
                        // Handle remainder
                        for (; j < slackCount; j++)
                            sum += data[colOffset + j] * invSlacks[j];
                            
                        grad[k] += sum;
                    });
                    return;
                }
            }
            catch
            {
                // Fall back if direct access fails
            }
        }
        
        // Choose best strategy based on problem dimensions and hardware
        int processorCount = Environment.ProcessorCount;
        bool useParallel = Math.Max(slackCount, gradCount) > 1000 || (slackCount * gradCount > 100000);
        int optimalChunkSize = Math.Min(16, System.Numerics.Vector<double>.Count * 2); // Optimize chunk size
        
        if (slackCount > gradCount * 2)
        {
            // Column-first traversal (better when slacks >> variables)
            if (useParallel)
            {
                Parallel.For(0, gradCount, new ParallelOptions { MaxDegreeOfParallelism = processorCount }, k =>
                {
                    double sum = 0.0;
                    int j = 0;
                    
                    // Process in optimal vector-friendly chunks
                    for (; j + optimalChunkSize - 1 < slackCount; j += optimalChunkSize)
                    {
                        double chunkSum = 0.0;
                        for (int offset = 0; offset < optimalChunkSize; offset++)
                            chunkSum += A[j + offset, k] * invSlacks[j + offset];
                        sum += chunkSum;
                    }
                    
                    // Handle remaining elements
                    for (; j < slackCount; j++)
                        sum += A[j, k] * invSlacks[j];
                        
                    grad[k] += sum;
                });
            }
            else
            {
                // Sequential version for smaller problems
                for (int k = 0; k < gradCount; k++)
                {
                    double sum = 0.0;
                    for (int j = 0; j < slackCount; j++)
                        sum += A[j, k] * invSlacks[j];
                    grad[k] += sum;
                }
            }
        }
        else
        {
            // Row-first traversal (better when variables ≈ slacks or variables > slacks)
            int j = 0;
            for (; j + optimalChunkSize - 1 < slackCount; j += optimalChunkSize)
            {
                // Prefetch inverse slack values for better cache locality
                double[] localInvSlacks = new double[optimalChunkSize];
                for (int idx = 0; idx < optimalChunkSize; idx++)
                    localInvSlacks[idx] = invSlacks[j + idx];
                
                if (useParallel && gradCount > processorCount * 4)
                {
                    Parallel.For(0, gradCount, new ParallelOptions { MaxDegreeOfParallelism = processorCount }, k =>
                    {
                        double sum = 0.0;
                        for (int idx = 0; idx < optimalChunkSize; idx++)
                            sum += A[j + idx, k] * localInvSlacks[idx];
                        
                        // Thread-safe update
                        lock (grad)
                        {
                            grad[k] += sum;
                        }
                    });
                }
                else
                {
                    for (int k = 0; k < gradCount; k++)
                    {
                        double sum = 0.0;
                        for (int idx = 0; idx < optimalChunkSize; idx++)
                            sum += A[j + idx, k] * localInvSlacks[idx];
                        grad[k] += sum;
                    }
                }
            }
            
            // Handle remaining rows
            for (; j < slackCount; j++)
            {
                double is0 = invSlacks[j];
                for (int k = 0; k < gradCount; k++)
                    grad[k] += A[j, k] * is0;
            }
        }
    }

    private static void UpdateSolution(Vector<double> x, Vector<double> grad, double step,
                                      Vector<double> lbVector, Vector<double> result)
    {
        x.Subtract(step * grad, result);
        result.PointwiseMaximum(lbVector, result);
    }
    
    private static double PerformLineSearch(Vector<double> x, Vector<double> grad, Vector<double> slacks, 
                                          Matrix<double> A, Vector<double> b, Vector<double> c, double t,
                                          Vector<double> candidate, Vector<double> lbVector)
    {
        double step = 1.0;
        double currentValue = ComputeBarrierValue(x, slacks, c, t);
        var tempSlacks = Vector<double>.Build.Dense(slacks.Count);
        
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
    
    private static double ComputeBarrierValue(Vector<double> x, Vector<double> slacks, Vector<double> c, double t)
    {
        return t * c.DotProduct(x) - slacks.PointwiseLog().Sum();
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