namespace StiglerDiet.Solvers;

using System;
using System.Collections.Generic;
using StiglerDiet.Solvers.Interfaces;

/// <summary>
/// Linear Programming Solver.
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

        // Count extra rows for variable and constraint upper bounds.
        int extraVarRows = 0;
        foreach (var v in Variables)
            if (!double.IsPositiveInfinity(v.UpperBound)) extraVarRows++;
        int extraConstrRows = 0;
        foreach (var c in Constraints)
            if (!double.IsPositiveInfinity(c.UpperBound)) extraConstrRows++;

        int m = baseM + extraVarRows + extraConstrRows;
        int n = baseN;

        double[] cVec = new double[n];
        foreach (var kvp in Objective.Coefficients)
        {
            int idx = Variables.IndexOf(kvp.Key);
            if (idx >= 0) cVec[idx] = kvp.Value;
        }

        double[,] Adata = new double[m, n];
        double[] bvals = new double[m];

        for (int i = 0; i < baseM; i++)
        {
            var constr = Constraints[i];
            foreach (var kvp in constr.Coefficients)
            {
                int idx = Variables.IndexOf(kvp.Key);
                if (idx >= 0)
                    Adata[i, idx] = kvp.Value;
            }
            bvals[i] = double.IsNegativeInfinity(constr.LowerBound) ? 0.0 : constr.LowerBound;
        }

        int rowOffset = baseM;
        foreach (var v in Variables)
        {
            if (!double.IsPositiveInfinity(v.UpperBound))
            {
                int varIndex = Variables.IndexOf(v);
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
                    int idx = Variables.IndexOf(kvp.Key);
                    if (idx >= 0)
                        Adata[rowOffset, idx] = -kvp.Value;
                }
                bvals[rowOffset] = -c.UpperBound;
                rowOffset++;
            }
        }

        bool isMin = Objective.IsMinimization;
        double[] solution = GlopStyleSolver.Solve(cVec, Adata, bvals, out int iterations, isMin, maxIter: 1000);
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
