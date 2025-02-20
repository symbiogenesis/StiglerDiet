namespace StiglerDiet.Solvers;

using System.Collections.Generic;

public class Constraint
{
    public string Name { get; }
    public double LowerBound { get; }
    public double UpperBound { get; }
    public Dictionary<Variable, double> Coefficients { get; } = [];

    public Constraint(string name, double lb, double ub)
    {
        Name = name;
        LowerBound = lb;
        UpperBound = ub;
    }
    public void SetCoefficient(Variable v, double coeff)
    {
        Coefficients[v] = coeff;
    }
}
