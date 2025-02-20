namespace StiglerDiet.Solvers;

using System;
using System.Collections.Generic;
using StiglerDiet.Solvers.Interfaces;

/// <summary>
/// Glop-style barrier method solver
/// </summary>
public class LinearProgrammingSolver : ISolver
{
    private double _wallTime = 0.0;
    private int _iterations = 0;
    private bool disposedValue;

    private const double Tolerance = 1e-7;
    private const double LowerBoundThreshold = 1e-9;
    private const double MinStepSize = 1e-12;

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

        int baseM = Constraints.Count;
        int baseN = Variables.Count;

        if (baseM == 0 || baseN == 0)
        {
            _wallTime = (DateTime.UtcNow - startTime).TotalSeconds;
            return ResultStatus.INFEASIBLE;
        }

        // Cache variable indices.
        var varIndices = new Dictionary<Variable, int>(baseN);
        for (int i = 0; i < baseN; i++)
        {
            varIndices[Variables[i]] = i;
        }

        // Count extra rows for variable and constraint upper bounds.
        int extraVarRows = Variables.Count(v => !double.IsPositiveInfinity(v.UpperBound));
        int extraConstrRows = Constraints.Count(c => !double.IsPositiveInfinity(c.UpperBound));

        int m = baseM + extraVarRows + extraConstrRows;
        int n = baseN;

        bool isMin = Objective.IsMinimization;
        double sign = isMin ? 1.0 : -1.0;

        // Initialize cVec with sign factor combined.
        double[] cVec = new double[n];
        foreach (var kvp in Objective.Coefficients)
        {
            int idx = varIndices[kvp.Key];
            cVec[idx] = sign * kvp.Value;
        }

        double[,] Adata = new double[m, n];
        double[] bvals = new double[m];

        for (int i = 0; i < baseM; i++)
        {
            var constr = Constraints[i];
            foreach (var kvp in constr.Coefficients)
            {
                int idx = varIndices[kvp.Key];
                Adata[i, idx] = kvp.Value;
            }
            bvals[i] = double.IsNegativeInfinity(constr.LowerBound) ? 0.0 : constr.LowerBound;
        }

        int rowOffset = baseM;
        foreach (var v in Variables)
        {
            if (!double.IsPositiveInfinity(v.UpperBound))
            {
                int varIndex = varIndices[v];
                Adata[rowOffset, varIndex] = -1.0;
                bvals[rowOffset] = -v.UpperBound;
                rowOffset++;
            }
        }
        foreach (var c in Constraints)
        {
            if (!double.IsPositiveInfinity(c.UpperBound))
            {
                foreach (var kvp in c.Coefficients)
                {
                    int idx = varIndices[kvp.Key];
                    Adata[rowOffset, idx] = -kvp.Value;
                }
                bvals[rowOffset] = -c.UpperBound;
                rowOffset++;
            }
        }

        const int maxIter = 1000;
        int iterations = 0;

        // Initialize x with 1's.
        var solution = Enumerable.Repeat(1.0, n).ToArray();

        // Barrier method parameters.
        double t = 1.0;         // initial scaling parameter
        double mu = 2.0;        // factor to increase t
        int innerIter = 50;     // inner iterations

        // Local function to compute the residual for a constraint row.
        double Residual(int i, double[] x)
        {
            double r = -bvals[i];
            for (int j = 0; j < n; j++)
            {
                r += Adata[i, j] * x[j];
            }
            return r;
        }

        double BarrierValue(double[] xVec)
        {
            double sum = 0.0;
            for (int i = 0; i < m; i++)
            {
                double diff = Residual(i, xVec);
                if (diff <= 0)
                    return double.PositiveInfinity;
                sum -= Math.Log(diff);
            }
            double obj = 0.0;
            for (int j = 0; j < n; j++)
                obj += cVec[j] * xVec[j];
            return t * obj + sum;
        }

        var candidate = new double[n];

        // Optimize the barrier function with backtracking line search.
        while (iterations < maxIter && m / t >= Tolerance)
        {
            for (int inner = 0; inner < innerIter; inner++)
            {
                iterations++;
                bool feasible = true;
                var grad = new double[n];
                for (int i = 0; i < m; i++)
                {
                    double residual = Residual(i, solution);
                    if (residual <= 0)
                    {
                        feasible = false;
                        break;
                    }
                    for (int j = 0; j < n; j++)
                        grad[j] += -Adata[i, j] / residual;
                }
                if (!feasible)
                    break;

                // Compute gradient norm and add contribution from the objective.
                double norm = 0;
                for (int j = 0; j < n; j++)
                {
                    grad[j] += t * cVec[j];
                    norm += grad[j] * grad[j];
                }
                norm = Math.Sqrt(norm);
                if (norm < Tolerance) break;

                // Backtracking line search parameters.
                double stepSize = 1.0;
                double alpha = 0.25;
                double beta = 0.5;

                // Compute current barrier value using the helper function.
                double currentVal = BarrierValue(solution);

                // Determine step size that gives sufficient decrease.
                while (true)
                {
                    double improvement = 0.0;
                    for (int j = 0; j < n; j++)
                    {
                        double newVal = Math.Max(solution[j] - stepSize * grad[j], LowerBoundThreshold);
                        improvement += grad[j] * (newVal - solution[j]);
                        candidate[j] = newVal;
                    }
                    
                    double candidateVal = BarrierValue(candidate);
                    
                    if (candidateVal <= currentVal + alpha * improvement)
                        break;
                    stepSize *= beta;
                    if (stepSize < MinStepSize)
                        break; // prevent too small steps.
                }

                // Update solution.
                Array.Copy(candidate, solution, n);
            }
            t *= mu; // Increase barrier weight.
        }

        _iterations = iterations;

        for (int j = 0; j < baseN; j++)
            Variables[j].Solution = Math.Abs(solution[j]) < Tolerance ? 0.0 : solution[j];

        _wallTime = (DateTime.UtcNow - startTime).TotalSeconds;

        return ResultStatus.OPTIMAL;
    }

    public double WallTime() => _wallTime;
    public long Iterations() => _iterations;
    public int NumVariables() => Variables.Count;
    public int NumConstraints() => Constraints.Count;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
