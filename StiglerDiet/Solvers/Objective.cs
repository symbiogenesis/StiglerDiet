namespace StiglerDiet.Solvers;

using System.Collections.Generic;

public class Objective(Dictionary<Variable, double>? coefficients = default, bool isMinimization = true)
{
    public Dictionary<Variable, double> Coefficients { get; } = coefficients ?? [];
    public bool IsMinimization { get; private set; } = isMinimization;
    public void SetCoefficient(Variable v, double coeff)
    {
        Coefficients[v] = coeff;
    }
    public void SetMinimization() => IsMinimization = true;
    public void SetMaximization() => IsMinimization = false;
}
