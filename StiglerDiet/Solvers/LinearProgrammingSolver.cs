namespace StiglerDiet.Solvers;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using StiglerDiet.Solvers.Interfaces;

/// <summary>
/// Glop-style barrier method solver
/// </summary>
public sealed class LinearProgrammingSolver(int maxIterations = 1000) : ISolver
{
    private double _wallTime = 0.0;
    private int _iterations = 0;

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
        var startTime = Stopwatch.GetTimestamp();

        int baseM = Constraints.Count;
        int baseN = Variables.Count;

        if (baseM == 0 || baseN == 0)
        {
            _wallTime = Stopwatch.GetElapsedTime(startTime).TotalSeconds;
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

        // Use 1D array for Adata (row-major)
        double[] Adata = new double[m * n];
        double[] bvals = new double[m];

        for (int i = 0; i < baseM; i++)
        {
            var constr = Constraints[i];
            foreach (var kvp in constr.Coefficients)
            {
                int idx = varIndices[kvp.Key];
                Adata[i * n + idx] = kvp.Value;
            }
            bvals[i] = double.IsNegativeInfinity(constr.LowerBound) ? 0.0 : constr.LowerBound;
        }

        int rowOffset = baseM;
        foreach (var v in Variables)
        {
            if (!double.IsPositiveInfinity(v.UpperBound))
            {
                int varIndex = varIndices[v];
                Adata[rowOffset * n + varIndex] = -1.0;
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
                    Adata[rowOffset * n + idx] = -kvp.Value;
                }
                bvals[rowOffset] = -c.UpperBound;
                rowOffset++;
            }
        }

        int iterations = 0;
        var solution = Enumerable.Repeat(1.0, n).ToArray();
        double t = 1.0, mu = 2.0;
        int innerIter = 50;

        // Helper for residuals (vectorized)
        void ComputeResiduals(double[] x, double[] residuals)
        {
            Array.Clear(residuals, 0, m);
            for (int i = 0; i < m; i++)
            {
                double r = -bvals[i];
                int rowStart = i * n;
                for (int j = 0; j < n; j++)
                    r += Adata[rowStart + j] * x[j];
                residuals[i] = r;
            }
        }

        double BarrierValue(double[] xVec, double[] residuals)
        {
            double sum = 0.0;
            for (int i = 0; i < m; i++)
            {
                double diff = residuals[i];
                // Use tolerance for barrier check
                if (diff <= Tolerance) 
                    return double.PositiveInfinity; 
                sum -= Math.Log(diff);
            }
            double obj = 0.0;
            for (int j = 0; j < n; j++)
                obj += cVec[j] * xVec[j];
            // Check for potential overflow before returning
            double barrierObj = t * obj + sum;
            return double.IsNaN(barrierObj) || double.IsInfinity(barrierObj) ? double.PositiveInfinity : barrierObj;
        }

        var candidate = new double[n];
        var grad = new double[n];
        var residuals = new double[m];

        // Optimize the barrier function with backtracking line search.
        while (iterations < maxIterations && m / t >= Tolerance)
        {
            for (int inner = 0; inner < innerIter; inner++)
            {
                iterations++;
                ComputeResiduals(solution, residuals);

                // Check feasibility using the defined tolerance
                bool feasible = true;
                for (int i = 0; i < m; i++)
                {
                    // Consistent check with BarrierValue logic
                    if (residuals[i] <= Tolerance) 
                    {
                        feasible = false;
                        break;
                    }
                }
                // If not feasible according to tolerance, stop inner iterations for this t
                if (!feasible) 
                    break; 

                Array.Clear(grad, 0, n);

                // Efficient gradient computation (vectorized)
                for (int i = 0; i < m; i++)
                {
                    double invResidual = 1.0 / residuals[i];
                    int rowStart = i * n;
                    for (int j = 0; j < n; j++)
                        grad[j] += -Adata[rowStart + j] * invResidual;
                }
                double norm = 0;
                for (int j = 0; j < n; j++)
                {
                    grad[j] += t * cVec[j];
                    norm += grad[j] * grad[j];
                }
                norm = Math.Sqrt(norm);
                if (norm < Tolerance) break;

                double stepSize = 1.0, alpha = 0.25, beta = 0.5;
                double currentVal = BarrierValue(solution, residuals);
                // Handle cases where the current point is already infeasible or problematic
                if (double.IsPositiveInfinity(currentVal)) break; 

                while (true)
                {
                    double improvement = 0.0;
                    for (int j = 0; j < n; j++)
                    {
                        double step = stepSize * grad[j];
                        double newVal = Math.Max(solution[j] - step, LowerBoundThreshold);
                        improvement += grad[j] * (newVal - solution[j]);
                        candidate[j] = newVal;
                    }
                    ComputeResiduals(candidate, residuals);
                    double candidateVal = BarrierValue(candidate, residuals);

                    // Check for valid candidate value and sufficient decrease
                    if (!double.IsPositiveInfinity(candidateVal) && candidateVal <= currentVal + alpha * improvement)
                        break;
                    stepSize *= beta;
                    if (stepSize < MinStepSize)
                        break;
                }
                Array.Copy(candidate, solution, n);
            }
            t *= mu;
        }

        _iterations = iterations;
        // Final feasibility check before declaring optimal
        ComputeResiduals(solution, residuals);
        for(int i = 0; i < m; ++i) {
            if (residuals[i] <= Tolerance) {
                 _wallTime = Stopwatch.GetElapsedTime(startTime).TotalSeconds;
                 // Return INFEASIBLE if final solution doesn't meet tolerance
                 return ResultStatus.INFEASIBLE; 
            }
        }

        for (int j = 0; j < baseN; j++)
            Variables[j].Solution = Math.Abs(solution[j]) < Tolerance ? 0.0 : solution[j];

        _wallTime = Stopwatch.GetElapsedTime(startTime).TotalSeconds;
        return ResultStatus.OPTIMAL;
    }

    public double WallTime() => _wallTime;
    public long Iterations() => _iterations;
    public int NumVariables() => Variables.Count;
    public int NumConstraints() => Constraints.Count;

    public void Dispose()
    {
    }
}
