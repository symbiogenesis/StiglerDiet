namespace StiglerDiet.Solvers;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using StiglerDiet.Solvers.Interfaces;

/// <summary>
/// Solves continuous linear programs using a two-phase primal simplex method.
/// </summary>
public sealed class LinearProgrammingSolver : ISolver
{
    private const double FeasibilityTolerance = 1e-7;
    private readonly int _maxIterations;
    private double _wallTime;
    private int _iterations;

    public LinearProgrammingSolver(int maxIterations = 1000)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxIterations);
        _maxIterations = maxIterations;
    }

    public List<Variable> Variables { get; } = [];
    public List<Constraint> Constraints { get; } = [];
    public Objective Objective { get; } = new();

    public Variable MakeNumVar(double lb, double ub, string name)
    {
        var variable = new Variable(name, lb, ub);
        Variables.Add(variable);
        return variable;
    }

    public Constraint MakeConstraint(double lb, double ub, string name)
    {
        var constraint = new Constraint(name, lb, ub);
        Constraints.Add(constraint);
        return constraint;
    }

    public ResultStatus Solve()
    {
        long startTime = Stopwatch.GetTimestamp();
        _iterations = 0;
        foreach (var variable in Variables)
            variable.Solution = double.NaN;

        try
        {
            return SolveCore();
        }
        finally
        {
            // ISolver follows OR-Tools' convention: wall time is in milliseconds.
            _wallTime = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;
        }
    }

    private ResultStatus SolveCore()
    {
        var indices = new Dictionary<Variable, int>();
        var transforms = new VariableTransform[Variables.Count];
        int columnCount = 0;
        for (int i = 0; i < Variables.Count; i++)
        {
            var variable = Variables[i];
            if (!indices.TryAdd(variable, i) || double.IsNaN(variable.LowerBound) || double.IsNaN(variable.UpperBound))
                return ResultStatus.ABNORMAL;
            if (variable.LowerBound > variable.UpperBound || double.IsPositiveInfinity(variable.LowerBound) || double.IsNegativeInfinity(variable.UpperBound))
                return ResultStatus.INFEASIBLE;

            // The tableau has only nonnegative variables. Shift a lower bound,
            // reflect an upper-only bound, or split an unrestricted variable.
            if (double.IsFinite(variable.LowerBound))
                transforms[i] = new(variable.LowerBound, columnCount++, 1.0, -1);
            else if (double.IsFinite(variable.UpperBound))
                transforms[i] = new(variable.UpperBound, columnCount++, -1.0, -1);
            else
            {
                transforms[i] = new(0.0, columnCount, 1.0, columnCount + 1);
                columnCount += 2;
            }
        }

        var rows = new List<double[]>();
        for (int i = 0; i < Variables.Count; i++)
        {
            var variable = Variables[i];
            if (double.IsFinite(variable.LowerBound) && double.IsFinite(variable.UpperBound))
            {
                var row = new double[columnCount + 1];
                row[transforms[i].Column] = 1.0;
                row[columnCount] = variable.UpperBound - variable.LowerBound;
                if (!double.IsFinite(row[columnCount]))
                    return ResultStatus.ABNORMAL;
                rows.Add(row);
            }
        }

        foreach (var constraint in Constraints)
        {
            if (double.IsNaN(constraint.LowerBound) || double.IsNaN(constraint.UpperBound))
                return ResultStatus.ABNORMAL;
            if (constraint.LowerBound > constraint.UpperBound || double.IsPositiveInfinity(constraint.LowerBound) || double.IsNegativeInfinity(constraint.UpperBound))
                return ResultStatus.INFEASIBLE;

            var coefficients = new double[columnCount];
            double offset = 0.0;
            foreach (var (variable, coefficient) in constraint.Coefficients)
            {
                if (!double.IsFinite(coefficient) || !indices.TryGetValue(variable, out int index))
                    return ResultStatus.ABNORMAL;
                var transform = transforms[index];
                offset += coefficient * transform.Offset;
                coefficients[transform.Column] += coefficient * transform.Sign;
                if (transform.NegativeColumn >= 0)
                    coefficients[transform.NegativeColumn] -= coefficient;
            }
            if (!double.IsFinite(offset))
                return ResultStatus.ABNORMAL;

            if (double.IsFinite(constraint.UpperBound))
            {
                var status = AddInequality(rows, coefficients, constraint.UpperBound - offset, 1.0);
                if (status != ResultStatus.OPTIMAL)
                    return status;
            }
            if (double.IsFinite(constraint.LowerBound))
            {
                var status = AddInequality(rows, coefficients, offset - constraint.LowerBound, -1.0);
                if (status != ResultStatus.OPTIMAL)
                    return status;
            }
        }

        var objective = new double[columnCount];
        double objectiveScale = 0.0;
        foreach (var (variable, coefficient) in Objective.Coefficients)
        {
            if (!double.IsFinite(coefficient) || !indices.TryGetValue(variable, out int index))
                return ResultStatus.ABNORMAL;
            var transform = transforms[index];
            double value = Objective.IsMinimization ? -coefficient : coefficient;
            objective[transform.Column] += value * transform.Sign;
            if (transform.NegativeColumn >= 0)
                objective[transform.NegativeColumn] -= value;
            objectiveScale = Math.Max(objectiveScale, Math.Abs(value));
        }
        if (objectiveScale > 0.0)
            for (int i = 0; i < objective.Length; i++)
                objective[i] /= objectiveScale;

        var tableau = new SimplexTableau(rows, columnCount);
        var result = tableau.Solve(objective, _maxIterations, out double[] values, out _iterations);
        if (result != ResultStatus.OPTIMAL)
            return result;

        var solution = new double[Variables.Count];
        for (int i = 0; i < Variables.Count; i++)
        {
            var transform = transforms[i];
            solution[i] = transform.Offset + transform.Sign * values[transform.Column];
            if (transform.NegativeColumn >= 0)
                solution[i] -= values[transform.NegativeColumn];
            if (!WithinBounds(solution[i], Variables[i].LowerBound, Variables[i].UpperBound))
                return ResultStatus.ABNORMAL;
        }
        // Check the result in the original units, before publishing any values.
        foreach (var constraint in Constraints)
        {
            double activity = 0.0;
            foreach (var (variable, coefficient) in constraint.Coefficients)
                activity += coefficient * solution[indices[variable]];
            if (!WithinBounds(activity, constraint.LowerBound, constraint.UpperBound))
                return ResultStatus.ABNORMAL;
        }
        for (int i = 0; i < Variables.Count; i++)
            Variables[i].Solution = solution[i];
        return ResultStatus.OPTIMAL;
    }

    private static ResultStatus AddInequality(List<double[]> rows, double[] coefficients, double bound, double sign)
    {
        if (!double.IsFinite(bound))
            return ResultStatus.ABNORMAL;
        double scale = 0.0;
        foreach (double coefficient in coefficients)
        {
            if (!double.IsFinite(coefficient))
                return ResultStatus.ABNORMAL;
            scale = Math.Max(scale, Math.Abs(coefficient));
        }
        if (scale == 0.0)
            return bound < 0.0 ? ResultStatus.INFEASIBLE : ResultStatus.OPTIMAL;

        var row = new double[coefficients.Length + 1];
        for (int j = 0; j < coefficients.Length; j++)
            row[j] = sign * coefficients[j] / scale;
        row[coefficients.Length] = bound / scale;
        if (!double.IsFinite(row[coefficients.Length]))
            return ResultStatus.ABNORMAL;
        rows.Add(row);
        return ResultStatus.OPTIMAL;
    }

    private static bool WithinBounds(double value, double lower, double upper) =>
        double.IsFinite(value) &&
        (double.IsNegativeInfinity(lower) || value >= lower - FeasibilityTolerance * Math.Max(1.0, Math.Abs(lower))) &&
        (double.IsPositiveInfinity(upper) || value <= upper + FeasibilityTolerance * Math.Max(1.0, Math.Abs(upper)));

    private readonly record struct VariableTransform(double Offset, int Column, double Sign, int NegativeColumn);

    private sealed class SimplexTableau
    {
        private double[,] _table;
        private int[] _basis;
        private readonly int _structuralColumns;
        private readonly int _nonArtificialColumns;
        private int RowCount => _basis.Length;
        private int ColumnCount => _table.GetLength(1) - 1;

        public SimplexTableau(List<double[]> rows, int structuralColumns)
        {
            _structuralColumns = structuralColumns;
            _nonArtificialColumns = structuralColumns + rows.Count;
            int artificialCount = 0;
            foreach (var row in rows)
                if (row[structuralColumns] < 0.0)
                    artificialCount++;
            int columns = _nonArtificialColumns + artificialCount;
            _table = new double[rows.Count + 1, columns + 1];
            _basis = new int[rows.Count];
            int artificial = _nonArtificialColumns;
            for (int i = 0; i < rows.Count; i++)
            {
                double sign = rows[i][structuralColumns] < 0.0 ? -1.0 : 1.0;
                for (int j = 0; j < structuralColumns; j++)
                    _table[i, j] = sign * rows[i][j];
                _table[i, structuralColumns + i] = sign;
                _table[i, columns] = sign * rows[i][structuralColumns];
                if (sign > 0.0)
                    _basis[i] = structuralColumns + i;
                else
                {
                    _basis[i] = artificial++;
                    _table[i, _basis[i]] = 1.0;
                }
            }
        }

        public ResultStatus Solve(double[] objective, int maxIterations, out double[] values, out int iterations)
        {
            values = [];
            iterations = 0;
            if (ColumnCount > _nonArtificialColumns)
            {
                var phaseOne = new double[ColumnCount];
                for (int j = _nonArtificialColumns; j < ColumnCount; j++)
                    phaseOne[j] = -1.0;
                SetObjective(phaseOne);
                var phaseOneStatus = Optimize(maxIterations, ref iterations, phaseOne: true);
                if (phaseOneStatus != ResultStatus.OPTIMAL)
                    return ResultStatus.ABNORMAL;
                // The phase-I objective is minus the sum of artificial values.
                if (_table[RowCount, ColumnCount] > FeasibilityTolerance)
                    return ResultStatus.INFEASIBLE;

                var retainedRows = new List<int>();
                for (int i = 0; i < RowCount; i++)
                {
                    if (_basis[i] >= _nonArtificialColumns)
                    {
                        if (Math.Abs(_table[i, ColumnCount]) > FeasibilityTolerance)
                            return ResultStatus.ABNORMAL;
                        _table[i, ColumnCount] = 0.0;
                        int entering = -1;
                        for (int j = 0; j < _nonArtificialColumns; j++)
                        {
                            if (_table[i, j] != 0.0 && Array.IndexOf(_basis, j) < 0)
                            {
                                entering = j;
                                break;
                            }
                        }
                        if (entering < 0)
                            continue; // A redundant zero row needs no basis variable.
                        if (iterations >= maxIterations || !Pivot(i, entering))
                            return ResultStatus.ABNORMAL;
                        iterations++;
                    }
                    retainedRows.Add(i);
                }
                RemoveArtificialColumns(retainedRows);
            }

            SetObjective(objective);
            var status = Optimize(maxIterations, ref iterations);
            if (status != ResultStatus.OPTIMAL)
                return status;

            values = new double[_structuralColumns];
            for (int i = 0; i < RowCount; i++)
            {
                double value = _table[i, ColumnCount];
                if (!double.IsFinite(value) || value < -FeasibilityTolerance)
                    return ResultStatus.ABNORMAL;
                if (_basis[i] < _structuralColumns)
                    values[_basis[i]] = Math.Max(0.0, value);
            }
            return ResultStatus.OPTIMAL;
        }

        private void SetObjective(double[] coefficients)
        {
            for (int j = 0; j <= ColumnCount; j++)
                _table[RowCount, j] = j < coefficients.Length ? coefficients[j] : 0.0;
            for (int i = 0; i < RowCount; i++)
            {
                double basicCoefficient = _basis[i] < coefficients.Length ? coefficients[_basis[i]] : 0.0;
                if (basicCoefficient == 0.0)
                    continue;
                for (int j = 0; j <= ColumnCount; j++)
                    _table[RowCount, j] -= basicCoefficient * _table[i, j];
            }
        }

        private ResultStatus Optimize(int maxIterations, ref int iterations, bool phaseOne = false)
        {
            while (true)
            {
                if (!double.IsFinite(_table[RowCount, ColumnCount]))
                    return ResultStatus.ABNORMAL;
                // Phase I only needs a feasible basis. Stopping when its
                // nonnegative artificial sum reaches zero also avoids chasing
                // reduced-cost roundoff after every artificial has left.
                if (phaseOne && Math.Abs(_table[RowCount, ColumnCount]) <= FeasibilityTolerance)
                    return ResultStatus.OPTIMAL;
                int entering = -1;
                // Bland's rule prevents cycling at degenerate vertices.
                for (int j = 0; j < ColumnCount; j++)
                {
                    if (!double.IsFinite(_table[RowCount, j]))
                        return ResultStatus.ABNORMAL;
                    if (_table[RowCount, j] > 0.0 && Array.IndexOf(_basis, j) < 0)
                    {
                        entering = j;
                        break;
                    }
                }
                if (entering < 0)
                    return ResultStatus.OPTIMAL;
                if (iterations >= maxIterations)
                    return ResultStatus.ABNORMAL;

                int leaving = -1;
                double smallestRatio = double.PositiveInfinity;
                for (int i = 0; i < RowCount; i++)
                {
                    double coefficient = _table[i, entering];
                    if (coefficient <= 0.0)
                        continue;
                    double ratio = _table[i, ColumnCount] / coefficient;
                    if (!double.IsFinite(ratio) || ratio < -FeasibilityTolerance)
                        return ResultStatus.ABNORMAL;
                    if (ratio < smallestRatio || (ratio == smallestRatio && (leaving < 0 || _basis[i] < _basis[leaving])))
                    {
                        smallestRatio = ratio;
                        leaving = i;
                    }
                }
                if (leaving < 0)
                    return ResultStatus.UNBOUNDED;
                if (!Pivot(leaving, entering))
                    return ResultStatus.ABNORMAL;
                iterations++;
            }
        }

        private bool Pivot(int row, int column)
        {
            double pivot = _table[row, column];
            for (int j = 0; j <= ColumnCount; j++)
                _table[row, j] /= pivot;
            _table[row, column] = 1.0;
            for (int i = 0; i <= RowCount; i++)
            {
                if (i == row)
                    continue;
                double factor = _table[i, column];
                for (int j = 0; j <= ColumnCount; j++)
                {
                    _table[i, j] -= factor * _table[row, j];
                    if (!double.IsFinite(_table[i, j]))
                        return false;
                }
                _table[i, column] = 0.0;
            }
            _basis[row] = column;
            return true;
        }

        private void RemoveArtificialColumns(List<int> retainedRows)
        {
            var table = new double[retainedRows.Count + 1, _nonArtificialColumns + 1];
            var basis = new int[retainedRows.Count];
            for (int i = 0; i < retainedRows.Count; i++)
            {
                int source = retainedRows[i];
                for (int j = 0; j < _nonArtificialColumns; j++)
                    table[i, j] = _table[source, j];
                table[i, _nonArtificialColumns] = _table[source, ColumnCount];
                basis[i] = _basis[source];
            }
            _table = table;
            _basis = basis;
        }
    }

    public double WallTime() => _wallTime;
    public long Iterations() => _iterations;
    public int NumVariables() => Variables.Count;
    public int NumConstraints() => Constraints.Count;
    public void Dispose() { }
}
