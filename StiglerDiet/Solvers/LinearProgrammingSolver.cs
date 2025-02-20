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

        double[] cVec = new double[n];
        foreach (var kvp in Objective.Coefficients)
        {
            int idx = varIndicies[kvp.Key];
            cVec[idx] = kvp.Value;
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

        bool isMin = Objective.IsMinimization;
        const int maxIter = 1000;

        int iterations = 0;

        // If maximizing, flip c (we minimize).
        if (!isMin)
        {
            for (int j = 0; j < n; j++)
                cVec[j] = -cVec[j];
        }

        // Initialize x with 1's.
        var solution = Enumerable.Repeat(1.0, n).ToArray();

        // Phase 1: Find feasible starting point.
        double minDiff = double.PositiveInfinity;
        for (int i = 0; i < m; i++)
        {
            double Ax = 0.0;
            for (int j = 0; j < n; j++)
                Ax += Adata[i, j] * solution[j];
            minDiff = Math.Min(minDiff, Ax - bvals[i]);
        }
        if (minDiff <= 0)
        {
            double offset = Math.Abs(minDiff) + 1.0;
            for (int j = 0; j < n; j++)
                solution[j] += offset;
        }

        // Barrier method parameters.
        double t = 1.0;         // initial scaling parameter
        double mu = 2.0;        // factor to increase t
        double tol = 1e-7;      // tolerance for convergence
        int innerIter = 50;     // inner iterations

        // Local helper to compute (A*x - b) for constraint i.
        double ConstraintResidual(int i, double[] xVec)
        {
            double sum = 0.0;
            for (int j = 0; j < n; j++)
                sum += Adata[i, j] * xVec[j];
            return sum - bvals[i];
        }

        // Helper: compute barrier function value.
        double BarrierValue(double[] xVec)
        {
            double sum = 0.0;
            for (int i = 0; i < m; i++)
            {
                double diff = ConstraintResidual(i, xVec);
                if(diff <= 0)
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
                var residuals = new double[m];
                bool feasible = true;
                // Precompute residuals for all constraints.
                for (int i = 0; i < m; i++)
                {
                    double Ax = 0.0;
                    for (int j = 0; j < n; j++)
                        Ax += Adata[i, j] * solution[j];
                    residuals[i] = Ax - bvals[i];
                    if (residuals[i] <= 0)
                        feasible = false;
                }
                if (!feasible)
                    break;

                // Compute gradient using precomputed residuals.
                var grad = new double[n];
                for (int i = 0; i < m; i++)
                {
                    for (int j = 0; j < n; j++)
                        grad[j] += -Adata[i, j] / residuals[i];
                }
                // Add contribution from the objective.
                for (int j = 0; j < n; j++)
                    grad[j] += t * cVec[j];

                // Compute gradient norm.
                double norm = 0;
                for (int j = 0; j < n; j++)
                    norm += grad[j] * grad[j];
                norm = Math.Sqrt(norm);
                if (norm < tol) break;

                // Backtracking line search parameters.
                double stepSize = 1.0;
                double alpha = 0.25;
                double beta = 0.5;
                double currentVal = 0.0;

                // Compute current barrier value using residuals.
                for (int i = 0; i < m; i++)
                {
                    // If any residual is nonpositive then BarrierValue would be infinity.
                    if (residuals[i] <= 0)
                    {
                        currentVal = double.PositiveInfinity;
                        break;
                    }
                    currentVal -= Math.Log(residuals[i]);
                }
                double obj = 0.0;
                for (int j = 0; j < n; j++)
                    obj += cVec[j] * solution[j];
                currentVal += t * obj;

                // Copy current x to test a candidate update.
                var xCandidate = new double[n];

                // Get descent direction.
                var descent = new double[n];
                for (int j = 0; j < n; j++)
                    descent[j] = -grad[j];

                // Determine step size that gives sufficient decrease.
                while (true)
                {
                    for (int j = 0; j < n; j++)
                    {
                        xCandidate[j] = solution[j] + stepSize * descent[j];
                        // Ensure positivity.
                        if (xCandidate[j] < 1e-9)
                            xCandidate[j] = 1e-9;
                    }

                    // Compute candidate barrier value.
                    double candidateVal = 0.0;
                    for (int i = 0; i < m; i++)
                    {
                        double AxCandidate = 0.0;
                        for (int j = 0; j < n; j++)
                            AxCandidate += Adata[i, j] * xCandidate[j];
                        double resCandidate = AxCandidate - bvals[i];
                        if (resCandidate <= 0)
                        {
                            candidateVal = double.PositiveInfinity;
                            break;
                        }
                        candidateVal -= Math.Log(resCandidate);
                    }
                    obj = 0.0;
                    for (int j = 0; j < n; j++)
                        obj += cVec[j] * xCandidate[j];
                    candidateVal += t * obj;

                    // Armijo condition.
                    double improvement = 0.0;
                    for (int j = 0; j < n; j++)
                        improvement += grad[j] * (xCandidate[j] - solution[j]);
                    if (candidateVal <= currentVal + alpha * stepSize * improvement)
                        break;
                    stepSize *= beta;
                    if (stepSize < 1e-12)
                        break; // prevent too small steps.
                }
                // Update x.
                Array.Copy(xCandidate, solution, n);
            }
            t *= mu; // Increase barrier weight.
            if (m / t < tol) break;
        }

        // Fix near-zero entries.
        for (int j = 0; j < n; j++)
        {
            if (Math.Abs(solution[j]) < tol)
                solution[j] = 0.0;
        }

        _iterations = iterations;

        for (int j = 0; j < baseN; j++)
            Variables[j].Solution = solution[j];

        _wallTime = (DateTime.UtcNow - startTime).TotalSeconds;

        // Return infeasible if no solution is found.
        if (solution == null || solution.Length == 0 || solution.All(double.IsNaN))
        {
            return ResultStatus.INFEASIBLE;
        }

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
