namespace StiglerDiet.Tests;

using StiglerDiet.Models;
using StiglerDiet.Solvers;
using StiglerDiet.Solvers.Interfaces;

public class DietModelTests
{
    private static FoodItem Food => new("Test food", 3, "lb.", 2, new NutritionFacts { Calories = 10 });

    [Fact]
    public void RepeatedCallsReplaceThePreviousModel()
    {
        using var solver = new StubSolver { Spending = 2 };
        var first = StiglerDietProgram.FindOptimalDiet(solver, new NutritionFacts { Calories = 20 }, [Food]);
        solver.Spending = 1;
        var second = StiglerDietProgram.FindOptimalDiet(solver, new NutritionFacts { Calories = 10 }, [Food]);

        Assert.Equal(2, Assert.Single(first).Price);
        Assert.Equal(1, Assert.Single(second).Price);
        Assert.Single(solver.Variables);
        Assert.Equal(9, solver.Constraints.Count);
        Assert.Single(solver.Objective.Coefficients);
        Assert.Equal(10, second.NutritionFacts.Calories);
    }

    [Fact]
    public void RepeatedFoodEntriesDoNotCollideInTheModel()
    {
        using var solver = new StubSolver { Spending = 1 };
        var food = Food;
        var result = StiglerDietProgram.FindOptimalDiet(solver, new NutritionFacts { Calories = 20 }, [food, food]);

        Assert.Equal(ResultStatus.OPTIMAL, result.ResultStatus);
        Assert.Equal(2, result.Count);
        Assert.Equal(2, solver.Variables.Select(v => v.Name).Distinct().Count());
    }

    [Fact]
    public void ConvertsDollarsToPackagesAndAccumulatesNutritionPerDollar()
    {
        using var solver = new StubSolver { Spending = 2 };
        var result = StiglerDietProgram.FindOptimalDiet(solver, new NutritionFacts { Calories = 20 }, [Food]);
        var item = Assert.Single(result);

        Assert.Equal(2, item.Price);
        Assert.Equal(1, item.Quantity);
        Assert.Equal(20, result.NutritionFacts.Calories);
        Assert.Equal(1, solver.Objective.Coefficients[Assert.Single(solver.Variables)]);
        Assert.Equal(10, solver.Constraints.Single(c => c.Name == "Calories").Coefficients[solver.Variables[0]]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void DoesNotPublishAnInvalidSolutionEvenWhenSolverClaimsOptimal(double spending)
    {
        using var solver = new StubSolver { Spending = spending };
        var result = StiglerDietProgram.FindOptimalDiet(solver, new NutritionFacts { Calories = 20 }, [Food]);

        Assert.Equal(ResultStatus.ABNORMAL, result.ResultStatus);
        Assert.Empty(result);
        Assert.Equal(0, result.NutritionFacts.Calories);
    }

    [Fact]
    public void DoesNotPublishOverflowedNutritionOrQuantities()
    {
        using var solver = new StubSolver { Spending = double.MaxValue };
        var result = StiglerDietProgram.FindOptimalDiet(solver, new NutritionFacts(), [Food]);
        Assert.Equal(ResultStatus.ABNORMAL, result.ResultStatus);
        Assert.Empty(result);
    }

    private sealed class StubSolver : ISolver
    {
        public double Spending { get; set; }
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
            foreach (var variable in Variables) variable.Solution = Spending;
            return ResultStatus.OPTIMAL;
        }
        public double WallTime() => 0;
        public long Iterations() => 0;
        public int NumVariables() => Variables.Count;
        public int NumConstraints() => Constraints.Count;
        public void Dispose() { }
    }
}
