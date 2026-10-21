namespace StiglerDiet.Tests;

using StiglerDiet.Models;
using StiglerDiet.Solvers;

public class DietInputTests
{
    private static FoodItem Food => new("Test food", 3, "lb.", 2, new NutritionFacts { Calories = 10 });

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RejectsInvalidFoodPrices(double price)
    {
        using var solver = new LinearProgrammingSolver();
        Assert.Throws<ArgumentException>(() => StiglerDietProgram.FindOptimalDiet(solver,
            new NutritionFacts(), [Food with { Price = price }]));
        Assert.Empty(solver.Variables);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RejectsInvalidFoodQuantities(double quantity)
    {
        using var solver = new LinearProgrammingSolver();
        Assert.Throws<ArgumentException>(() => StiglerDietProgram.FindOptimalDiet(solver,
            new NutritionFacts(), [Food with { Quantity = quantity }]));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RejectsInvalidNutritionInRequirementsAndFoods(double calories)
    {
        using var solver = new LinearProgrammingSolver();
        var invalid = new NutritionFacts { Calories = calories };
        Assert.Throws<ArgumentException>(() => StiglerDietProgram.FindOptimalDiet(solver, invalid, [Food]));
        Assert.Throws<ArgumentException>(() => StiglerDietProgram.FindOptimalDiet(solver,
            new NutritionFacts(), [Food with { NutritionFacts = invalid }]));
    }
}
