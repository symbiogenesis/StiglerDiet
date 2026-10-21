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
            var variables = Variables.ToHashSet();
            if (variables.Count != Variables.Count ||
                Variables.Any(v => double.IsNaN(v.LowerBound) || double.IsNaN(v.UpperBound)) ||
                Constraints.Any(c => double.IsNaN(c.LowerBound) || double.IsNaN(c.UpperBound)) ||
                Objective.Coefficients.Any(c => !variables.Contains(c.Key) || !double.IsFinite(c.Value)) ||
                Constraints.Any(c => c.Coefficients.Any(coefficient =>
                    !variables.Contains(coefficient.Key) || !double.IsFinite(coefficient.Value))))
            {
                return ResultStatus.ABNORMAL;
            }

            if (Variables.Any(v => InvalidBounds(v.LowerBound, v.UpperBound)) ||
                Constraints.Any(c => InvalidBounds(c.LowerBound, c.UpperBound)))
            {
                return ResultStatus.INFEASIBLE;
            }

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

            var nativeResult = solver.Solve();
            _iterations = solver.Iterations();
            if (nativeResult == Solver.ResultStatus.INFEASIBLE)
            {
                // GLOP presolve also reports INFEASIBLE for an unbounded model.
                // Resolve that ambiguity with primal simplex on the original model.
                solver.SetSolverSpecificParametersAsString("use_preprocessing: false use_dual_simplex: false");
                nativeResult = solver.Solve();
                _iterations += solver.Iterations();
            }

            var result = nativeResult switch
            {
                Solver.ResultStatus.OPTIMAL => ResultStatus.OPTIMAL,
                Solver.ResultStatus.FEASIBLE => ResultStatus.FEASIBLE,
                Solver.ResultStatus.INFEASIBLE => ResultStatus.INFEASIBLE,
                Solver.ResultStatus.UNBOUNDED => ResultStatus.UNBOUNDED,
                _ => ResultStatus.ABNORMAL,
            };

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

    private static bool InvalidBounds(double lowerBound, double upperBound) =>
        lowerBound > upperBound || double.IsPositiveInfinity(lowerBound) || double.IsNegativeInfinity(upperBound);

    public double WallTime() => _wallTime;
    public long Iterations() => _iterations;
    public int NumVariables() => Variables.Count;
    public int NumConstraints() => Constraints.Count;

    public void Dispose() => _disposed = true;
}
