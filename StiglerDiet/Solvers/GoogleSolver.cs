namespace StiglerDiet.Solvers;

using System.Collections.Generic;
using Google.OrTools.LinearSolver;
using StiglerDiet.Solvers.Interfaces;
using static Google.OrTools.LinearSolver.Solver;

/// <summary>
/// Linear Programming Solver.
/// </summary>
public class GoogleSolver : ISolver
{
    private readonly Solver _solver = new("StiglerDietSolver", OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);

    private List<Variable>? _variables;
    private List<Constraint>? _constraints;
    private Objective? _objective;

    public List<Variable> Variables => _variables ??= GetVariables();
    public List<Constraint> Constraints => _constraints ??= GetConstraints();
    public Objective Objective => _objective ??= GetObjective();

    public Variable MakeNumVar(double lb, double ub, string name)
    {
        var v = _solver.MakeNumVar(lb, ub, name);
        return new Variable(v.Name(), v.Lb(), v.Ub());
    }

    public Constraint MakeConstraint(double lb, double ub, string name)
    {
        var c = _solver.MakeConstraint(lb, ub, name);
        return new Constraint(c.Name(), c.Lb(), c.Ub());
    }

    public ResultStatus Solve()
    {
        var result = _solver.Solve();

        _variables = GetVariables();
        _constraints = GetConstraints();
        _objective = GetObjective();

        return result switch
        {
            Solver.ResultStatus.OPTIMAL => ResultStatus.OPTIMAL,
            Solver.ResultStatus.FEASIBLE => ResultStatus.FEASIBLE,
            Solver.ResultStatus.INFEASIBLE => ResultStatus.INFEASIBLE,
            Solver.ResultStatus.UNBOUNDED => ResultStatus.UNBOUNDED,
            Solver.ResultStatus.ABNORMAL => ResultStatus.ABNORMAL,
            _ => throw new InvalidDataException("Unknown result status"),
        };
    }

    public double WallTime() => _solver.WallTime();
    public long Iterations() => _solver.Iterations();
    public int NumVariables() => _solver.NumVariables();
    public int NumConstraints() => _solver.NumConstraints();

    public void Dispose()
    {
        _solver.Dispose();
    }

    
    private List<Variable> GetVariables() => _solver.variables().Select(v => new Variable(v.Name(), v.Lb(), v.Ub())).ToList();
    private List<Constraint> GetConstraints() => _solver.constraints().Select(c => new Constraint(c.Name(), c.Lb(), c.Ub())).ToList();
    private Objective GetObjective()
    {
        var objective = _solver.Objective();

        Dictionary<Variable, double> coefficients = [];

        foreach (var v in _solver.variables())
        {
            var coefficient = objective.GetCoefficient(v);
            coefficients.Add(new(v.Name(), v.Lb(), v.Ub()), coefficient);
        }

        return new(coefficients, objective.Minimization()); 
    }
}
