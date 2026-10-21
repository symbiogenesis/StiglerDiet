namespace StiglerDiet.Tests;

using System.Runtime.InteropServices;
using StiglerDiet.Models;
using StiglerDiet.Solvers;

public class GoogleSolverTests
{
    [GoogleSolverFact]
    public void SolvesConfiguredModelAndReturnsTheSameModelObjects()
    {
        using var solver = new GoogleSolver();
        var x = solver.MakeNumVar(0, double.PositiveInfinity, "x");
        var y = solver.MakeNumVar(0, double.PositiveInfinity, "y");
        var demand = solver.MakeConstraint(6, double.PositiveInfinity, "demand");
        demand.SetCoefficient(x, 1);
        demand.SetCoefficient(y, 2);
        var objective = solver.Objective;
        objective.SetCoefficient(x, 1);
        objective.SetCoefficient(y, 3);

        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());

        Assert.Same(x, solver.Variables[0]);
        Assert.Same(y, solver.Variables[1]);
        Assert.Same(demand, solver.Constraints[0]);
        Assert.Same(objective, solver.Objective);
        Assert.Equal(2, solver.NumVariables());
        Assert.Equal(1, solver.NumConstraints());
        Assert.Equal(6, x.SolutionValue(), 8);
        Assert.Equal(0, y.SolutionValue(), 8);
        Assert.Equal(6, ObjectiveValue(solver), 8);
        Assert.Equal(6, demand.Coefficients.Sum(c => c.Value * c.Key.SolutionValue()), 8);
    }

    [GoogleSolverFact]
    public void ReloadsCoefficientChangesRemovalsAndObjectiveDirectionBetweenSolves()
    {
        using var solver = new GoogleSolver();
        var x = solver.MakeNumVar(0, 10, "x");
        var y = solver.MakeNumVar(0, 10, "y");
        var demand = solver.MakeConstraint(4, double.PositiveInfinity, "demand");
        demand.SetCoefficient(x, 1);
        demand.SetCoefficient(y, 1);
        solver.Objective.SetCoefficient(x, 1);
        solver.Objective.SetCoefficient(y, 2);
        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(4, x.SolutionValue(), 8);

        demand.Coefficients.Remove(x);
        solver.Objective.Coefficients.Remove(x);
        solver.Variables.Remove(x);
        demand.SetCoefficient(y, 2);
        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(2, y.SolutionValue(), 8);
        Assert.Equal(4, ObjectiveValue(solver), 8);

        solver.Constraints.Clear();
        solver.Objective.SetCoefficient(y, 3);
        solver.Objective.SetMaximization();
        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(10, y.SolutionValue(), 8);
        Assert.Equal(30, ObjectiveValue(solver), 8);

        solver.Objective.SetMinimization();
        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(0, y.SolutionValue(), 8);
    }

    [GoogleSolverFact]
    public void HonorsNegativeVariableBoundsAndUpperOnlyConstraint()
    {
        using var solver = new GoogleSolver();
        var x = solver.MakeNumVar(-5, 3, "x");
        var upper = solver.MakeConstraint(double.NegativeInfinity, -2, "upper");
        upper.SetCoefficient(x, 1);
        solver.Objective.SetCoefficient(x, 1);
        solver.Objective.SetMaximization();

        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(-2, x.SolutionValue(), 8);

        solver.Constraints.Clear();
        solver.Objective.SetMinimization();
        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(-5, x.SolutionValue(), 8);
    }

    [GoogleSolverFact]
    public void ClearsEarlierSolutionWhenTheModelBecomesInfeasible()
    {
        using var solver = new GoogleSolver();
        var x = solver.MakeNumVar(0, 10, "x");
        solver.Objective.SetCoefficient(x, 1);
        solver.Objective.SetMaximization();
        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(10, x.SolutionValue(), 8);

        var impossible = solver.MakeConstraint(11, double.PositiveInfinity, "impossible");
        impossible.SetCoefficient(x, 1);

        Assert.Equal(ResultStatus.INFEASIBLE, solver.Solve());
        Assert.Equal(0, x.SolutionValue());
    }

    [GoogleSolverFact]
    public void SolvesTheOriginalDietAtThePublishedCostAndMeetsEveryAllowance()
    {
        using var solver = new GoogleSolver();
        var allowance = CsvParser.LoadMinimumDailyAllowance();
        var diet = StiglerDietProgram.FindOptimalDiet(solver, allowance, CsvParser.LoadFoodItems());

        Assert.Equal(ResultStatus.OPTIMAL, diet.ResultStatus);
        Assert.Equal(39.6617315454663, diet.Sum(item => item.Price) * 365, 7);
        Assert.Equal(diet.Sum(item => item.Price), ObjectiveValue(solver), 9);
        Assert.Equal(77, solver.NumVariables());
        Assert.Equal(9, solver.NumConstraints());
        foreach (var nutrient in NutritionFacts.Properties)
        {
            var actual = (double)nutrient.GetValue(diet.NutritionFacts)!;
            var minimum = (double)nutrient.GetValue(allowance)!;
            Assert.True(actual + 1e-7 >= minimum,
                $"{nutrient.Name}: the diet provides {actual} but needs {minimum}.");
        }
    }

    [GoogleSolverFact]
    public void RejectsInvalidCoefficientsAndForeignVariablesWithoutPublishingASolution()
    {
        using var solver = new GoogleSolver();
        var x = solver.MakeNumVar(0, 10, "x");
        x.Solution = 7;
        solver.Objective.SetCoefficient(x, double.NaN);
        Assert.Equal(ResultStatus.ABNORMAL, solver.Solve());
        Assert.Equal(0, x.SolutionValue());

        solver.Objective.SetCoefficient(x, double.PositiveInfinity);
        Assert.Equal(ResultStatus.ABNORMAL, solver.Solve());

        solver.Objective.Coefficients.Clear();
        var row = solver.MakeConstraint(0, 1, "row");
        row.SetCoefficient(new Variable("foreign", 0, 1), 1);
        Assert.Equal(ResultStatus.ABNORMAL, solver.Solve());

        row.Coefficients.Clear();
        row.SetCoefficient(x, double.NegativeInfinity);
        Assert.Equal(ResultStatus.ABNORMAL, solver.Solve());
    }

    [GoogleSolverFact]
    public void RejectsDuplicateVariablesAndNaNBounds()
    {
        using var solver = new GoogleSolver();
        var x = solver.MakeNumVar(0, 1, "x");
        solver.Variables.Add(x);
        Assert.Equal(ResultStatus.ABNORMAL, solver.Solve());

        solver.Variables.Clear();
        solver.MakeNumVar(double.NaN, 1, "bad lower bound");
        Assert.Equal(ResultStatus.ABNORMAL, solver.Solve());

        solver.Variables.Clear();
        solver.MakeConstraint(0, double.NaN, "bad upper bound");
        Assert.Equal(ResultStatus.ABNORMAL, solver.Solve());
    }

    [GoogleSolverFact]
    public void ReportsContradictoryBoundsAsInfeasible()
    {
        using var solver = new GoogleSolver();
        solver.MakeNumVar(2, 1, "impossible variable");
        Assert.Equal(ResultStatus.INFEASIBLE, solver.Solve());

        solver.Variables.Clear();
        solver.MakeConstraint(1, 0, "impossible row");
        Assert.Equal(ResultStatus.INFEASIBLE, solver.Solve());
    }

    [GoogleSolverFact]
    public void DistinguishesUnboundedFromInfeasibleModelsAfterPresolve()
    {
        using var solver = new GoogleSolver();
        var x = solver.MakeNumVar(0, double.PositiveInfinity, "x");
        x.Solution = 7;
        solver.Objective.SetCoefficient(x, -1);

        Assert.Equal(ResultStatus.UNBOUNDED, solver.Solve());
        Assert.Equal(0, x.SolutionValue());

        var impossible = solver.MakeConstraint(double.NegativeInfinity, -1, "impossible");
        impossible.SetCoefficient(x, 1);
        Assert.Equal(ResultStatus.INFEASIBLE, solver.Solve());
        Assert.Equal(0, x.SolutionValue());
    }

    private static double ObjectiveValue(GoogleSolver solver) =>
        solver.Objective.Coefficients.Sum(c => c.Value * c.Key.SolutionValue());
}

public sealed class GoogleSolverFactAttribute : FactAttribute
{
    public GoogleSolverFactAttribute()
    {
        if (OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            Skip = "Google OR-Tools supplies Windows native libraries only for x64; run these tests with an x64 test host.";
        }
    }
}
