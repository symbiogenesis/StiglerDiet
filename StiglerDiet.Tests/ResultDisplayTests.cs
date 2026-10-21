namespace StiglerDiet.Tests;

using StiglerDiet.Models;
using StiglerDiet.Solvers;

[Collection("Console output")]
public class ResultDisplayTests
{
    [Fact]
    public void ZeroRequirementsDisplayAsNotApplicable()
    {
        using var solver = new LinearProgrammingSolver();
        var diet = new OptimalDailyDiet(ResultStatus.OPTIMAL)
        {
            new(new FoodItem("Test", 1, "lb.", 1, new NutritionFacts()), 1, 1)
        };
        diet.NutritionFacts = new NutritionFacts { Calories = 10 };

        string output = Capture(() => StiglerDietProgram.LogResults(solver, diet, new NutritionFacts()));

        Assert.Contains("N/A", output);
        Assert.DoesNotContain("NaN", output);
        Assert.DoesNotContain(double.PositiveInfinity.ToString("N2"), output);
    }

    [Fact]
    public void AnnualQuantityConvertsPackagesBeforeScalingThePeriod()
    {
        using var solver = new LinearProgrammingSolver();
        var diet = new OptimalDailyDiet(ResultStatus.OPTIMAL)
        {
            new(new FoodItem("Small package", 1e-308, "lb.", 1e-308, new NutritionFacts()), 1, 1e308)
        };

        string output = Capture(() => StiglerDietProgram.LogResults(solver, diet, new NutritionFacts()));

        Assert.Contains($"{365:N2} (lb.)", output);
        Assert.DoesNotContain(double.PositiveInfinity.ToString("N2"), output);
    }

    private static string Capture(Action action)
    {
        TextWriter previous = Console.Out;
        using var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(previous);
        }
    }
}
