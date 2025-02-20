namespace StiglerDiet.Solvers;

public class Variable
{
    public string Name { get; }
    public double LowerBound { get; }
    public double UpperBound { get; }
    public double Solution { get; set; }

    public Variable(string name, double lb, double ub)
    {
        Name = name;
        LowerBound = lb;
        UpperBound = ub;
    }
    public double SolutionValue() => Solution;
}
