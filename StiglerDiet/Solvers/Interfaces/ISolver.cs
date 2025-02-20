namespace StiglerDiet.Solvers.Interfaces;

using System.Collections.Generic;
using StiglerDiet.Solvers;

public interface ISolver : IDisposable
{
    public List<Variable> Variables { get; }
    public List<Constraint> Constraints { get; }
    public Objective Objective { get; }

    public Constraint MakeConstraint(double lb, double ub, string name);
    public Variable MakeNumVar(double lb, double ub, string name);
    public ResultStatus Solve();

    public double WallTime();
    public long Iterations();
    public int NumVariables();
    public int NumConstraints();
}