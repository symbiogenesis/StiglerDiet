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
        var varIndicies = new Dictionary<Variable, int>(baseN);
        for (int i = 0; i < baseN; i++)
        {
            varIndicies[Variables[i]] = i;
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
            int idx = varIndicies[kvp.Key];
            cVec[idx] = sign * kvp.Value;
        }

        double[,] Adata = new double[m, n];
        double[] bvals = new double[m];

        for (int i = 0; i < baseM; i++)
        {
            var constr = Constraints[i];
            foreach (var kvp in constr.Coefficients)
            {
                int idx = varIndicies[kvp.Key];
                Adata[i, idx] = kvp.Value;
            }
            bvals[i] = double.IsNegativeInfinity(constr.LowerBound) ? 0.0 : constr.LowerBound;
        }

        int rowOffset = baseM;
        foreach (var v in Variables)
        {
            if (!double.IsPositiveInfinity(v.UpperBound))
            {
                int varIndex = varIndicies[v];
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
                    int idx = varIndicies[kvp.Key];
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

        // Helper: compute barrier function value.
        double BarrierValue(double[] xVec)
        {
            double sum = 0.0;
            for (int i = 0; i < m; i++)
            {
                double diff = -bvals[i];
                for (int j = 0; j < n; j++)
                {
                    diff += Adata[i, j] * xVec[j];
                }
                if (diff <= 0)
                    return double.PositiveInfinity;
                sum -= Math.Log(diff);
            }
            double obj = 0.0;
            for (int j = 0; j < n; j++)
                obj += cVec[j] * xVec[j];
            return t * obj + sum;
        }

        // Optimize the barrier function with backtracking line search.
        for (int outer = 0; outer < maxIter / innerIter; outer++)
        {
            for (int inner = 0; inner < innerIter; inner++)
            {
                iterations++;
                bool feasible = true;
                var grad = new double[n];
                for (int i = 0; i < m; i++)
                {
                    double Ax = 0.0;
                    for (int j = 0; j < n; j++)
                        Ax += Adata[i, j] * solution[j];

                    double residual = Ax - bvals[i];
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
                // Add contribution from the objective.
                for (int j = 0; j < n; j++)
                    grad[j] += t * cVec[j];

                // Compute gradient norm.
                double norm = 0;
                for (int j = 0; j < n; j++)
                    norm += grad[j] * grad[j];
                norm = Math.Sqrt(norm);
                if (norm < Tolerance) break;

                // Backtracking line search parameters.
                double stepSize = 1.0;
                double alpha = 0.25;
                double beta = 0.5;

                // Compute current barrier value using the helper function.
                double currentVal = BarrierValue(solution);

                // Copy current x to test a candidate update.
                var candidate = new double[n];

                // Determine step size that gives sufficient decrease.
                while (true)
                {
                    for (int j = 0; j < n; j++)
                    {
                        candidate[j] = Math.Max(solution[j] - stepSize * grad[j], LowerBoundThreshold);
                    }

                    // Compute candidate barrier value.
                    double candidateVal = BarrierValue(candidate);

                    // Armijo condition.
                    double improvement = 0.0;
                    for (int j = 0; j < n; j++)
                        improvement += grad[j] * (candidate[j] - solution[j]);
                    if (candidateVal <= currentVal + alpha * stepSize * improvement)
                        break;
                    stepSize *= beta;
                    if (stepSize < MinStepSize)
                        break; // prevent too small steps.
                }

                // Update solution.
                Array.Copy(candidate, solution, n);
            }
            t *= mu; // Increase barrier weight.
            if (m / t < Tolerance) break;
        }

        // Fix near-zero entries.
        for (int j = 0; j < n; j++)
        {
            if (Math.Abs(solution[j]) < Tolerance)
                solution[j] = 0.0;
        }

        _iterations = iterations;

        for (int j = 0; j < baseN; j++)
            Variables[j].Solution = solution[j];

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
