namespace StiglerDiet.Solvers;

using System.Diagnostics;
using Google.OrTools.LinearSolver;
using StiglerDiet.Solvers.Interfaces;

/// <summary>
/// Linear programming solver backed by Google OR-Tools GLOP.
/// </summary>
public sealed class GoogleSolver : ISolver
{
    private bool _disposed;
    private double _wallTime;
    private long _iterations;

    public List<Variable> Variables { get; } = [];
    public List<Constraint> Constraints { get; } = [];
    public Objective Objective { get; } = new();

    public Variable MakeNumVar(double lb, double ub, string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var variable = new Variable(name, lb, ub);
        Variables.Add(variable);
        return variable;
    }

    public Constraint MakeConstraint(double lb, double ub, string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var constraint = new Constraint(name, lb, ub);
        Constraints.Add(constraint);
        return constraint;
    }

    public ResultStatus Solve()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var started = Stopwatch.GetTimestamp();
        _iterations = 0;
        foreach (var variable in Variables)
        {
            variable.Solution = 0;
        }

        try
        {
            // The public model is mutable. Rebuild it on every solve so coefficients,
            // removed rows/columns, and objective direction cannot become stale.
            using var solver = new Solver("StiglerDietSolver", Solver.OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);
            var nativeVariables = Variables.ToDictionary(v => v,
                v => solver.MakeNumVar(v.LowerBound, v.UpperBound, v.Name));

            foreach (var constraint in Constraints)
            {
                var nativeConstraint = solver.MakeConstraint(constraint.LowerBound, constraint.UpperBound, constraint.Name);
                foreach (var (variable, coefficient) in constraint.Coefficients)
                {
                    nativeConstraint.SetCoefficient(nativeVariables[variable], coefficient);
                }
            }

            var objective = solver.Objective();
            foreach (var (variable, coefficient) in Objective.Coefficients)
            {
                objective.SetCoefficient(nativeVariables[variable], coefficient);
            }
            if (Objective.IsMinimization)
            {
                objective.SetMinimization();
            }
            else
            {
                objective.SetMaximization();
            }

            var result = solver.Solve() switch
            {
                Solver.ResultStatus.OPTIMAL => ResultStatus.OPTIMAL,
                Solver.ResultStatus.FEASIBLE => ResultStatus.FEASIBLE,
                Solver.ResultStatus.INFEASIBLE => ResultStatus.INFEASIBLE,
                Solver.ResultStatus.UNBOUNDED => ResultStatus.UNBOUNDED,
                _ => ResultStatus.ABNORMAL,
            };
            _iterations = solver.Iterations();

            if (result is ResultStatus.OPTIMAL or ResultStatus.FEASIBLE)
            {
                foreach (var (variable, nativeVariable) in nativeVariables)
                {
                    variable.Solution = nativeVariable.SolutionValue();
                }
            }

            return result;
        }
        finally
        {
            _wallTime = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }
    }

    public double WallTime() => _wallTime;
    public long Iterations() => _iterations;
    public int NumVariables() => Variables.Count;
    public int NumConstraints() => Constraints.Count;

    public void Dispose() => _disposed = true;
}
