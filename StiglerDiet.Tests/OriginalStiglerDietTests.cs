namespace StiglerDiet.Tests;

using Google.OrTools.LinearSolver;
using StiglerDiet.Models;

public class OriginalStiglerDietTests
{
    private readonly NutritionFacts minimumDailyAllowance;
    private readonly List<FoodItem> foodItems;

    public OriginalStiglerDietTests()
    {
        minimumDailyAllowance = CsvParser.LoadMinimumDailyAllowance();
        foodItems = CsvParser.LoadFoodItems();
    }

    private static Solver CreateSolver() => new("test", Solver.OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);

    [Fact]
    public void Solver_ReturnsOptimalSolution()
    {
        // Arrange
        using var solver = CreateSolver();

        // Act
        var (foodsResult, nutrientsResult, resultStatus) = StiglerDietProgram.FindOptimalDiet(solver, minimumDailyAllowance, foodItems);

        // Assert
        Assert.NotNull(foodsResult);
        Assert.NotNull(nutrientsResult);
    }

    [Theory]
    [InlineData(77)]
    public void NumberOfVariables_IsCorrect(int expected)
    {
        // Arrange
        using var solver = CreateSolver();

        // Act
        StiglerDietProgram.FindOptimalDiet(solver, minimumDailyAllowance, foodItems);

        // Assert
        Assert.Equal(expected, solver.NumVariables());
    }

    [Theory]
    [InlineData(9)]
    public void NumberOfConstraints_IsCorrect(int expected)
    {
        // Arrange
        using var solver = CreateSolver();

        // Act
        StiglerDietProgram.FindOptimalDiet(solver, minimumDailyAllowance, foodItems);

        // Assert
        Assert.Equal(expected, solver.NumConstraints());
    }

    [Fact]
    public void OptimalAnnualPrice_IsAsExpected()
    {
        // Arrange
        using var solver = CreateSolver();

        // Act
        var (foodsResult, nutrientsResult, resultStatus) = StiglerDietProgram.FindOptimalDiet(solver, minimumDailyAllowance, foodItems);
        double? totalDailyCost = foodsResult?.Sum(item => item.DailyPrice);

        // Assert
        Assert.NotNull(totalDailyCost);
        Assert.Equal(39.66, double.Round(totalDailyCost.Value * 365, 2));
    }

    [Fact]
    public void OptimalDailyPrice_IsAsExpected()
    {
        // Arrange
        using var solver = CreateSolver();

        // Act
        var (foodsResult, nutrientsResult, resultStatus) = StiglerDietProgram.FindOptimalDiet(solver, minimumDailyAllowance, foodItems);
        double? totalDailyCost = foodsResult?.Sum(item => item.DailyPrice);

        // Assert
        Assert.NotNull(totalDailyCost);
        Assert.Equal(0.11, double.Round(totalDailyCost.Value, 2));
    }

    [Theory]
    [InlineData("Calories", 3.0)]
    [InlineData("Protein", 70)]
    [InlineData("Calcium", 0.8)]
    [InlineData("Iron", 12)]
    [InlineData("VitaminA", 5.0)]
    [InlineData("VitaminB1", 1.8)]
    [InlineData("VitaminB2", 2.7)]
    [InlineData("Niacin", 18)]
    [InlineData("VitaminC", 75)]
    public void NutrientRequirements_AreMet(string nutrientName, double minimum)
    {
        // Arrange
        using var solver = CreateSolver();

        // Act
        StiglerDietProgram.FindOptimalDiet(solver, minimumDailyAllowance, foodItems);
        var constraint = solver.constraints().First(c => c.Name().StartsWith(nutrientName));

        // Assert
        Assert.True(constraint.Lb() >= minimum, $"{nutrientName} value of {constraint.Lb()} does not meet the minimum requirement of {minimum}.");
    }

    [Fact]
    public void Diet_InitializesCorrectly()
    {
        // Arrange
        using var solver = CreateSolver();

        // Act
        var (foodsResult, nutrientsResult, resultStatus) = StiglerDietProgram.FindOptimalDiet(solver, minimumDailyAllowance, foodItems);

        // Assert
        Assert.NotNull(foodsResult);
        Assert.NotNull(nutrientsResult);
    }

    [Fact]
    public void ObjectiveValue_IsPositive()
    {
        // Arrange
        using var solver = CreateSolver();

        // Act
        StiglerDietProgram.FindOptimalDiet(solver, minimumDailyAllowance, foodItems);
        var objectiveValue = solver.Objective().Value();

        // Assert
        Assert.True(objectiveValue > 0, "Objective value should be positive.");
    }

    [Fact]
    public void Solver_ReturnsOptimalResultStatus()
    {
        // Arrange
        using var solver = CreateSolver();

        // Act
        var (foodsResult, nutrientsResult, resultStatus) = StiglerDietProgram.FindOptimalDiet(solver, minimumDailyAllowance, foodItems);

        // Assert
        Assert.Equal(Solver.ResultStatus.OPTIMAL, resultStatus);
        Assert.NotNull(nutrientsResult);
        Assert.NotNull(foodsResult);
        Assert.NotEmpty(foodsResult);
    }

    [Fact]
    public void Solver_ReturnsInfeasibleResultStatus()
    {
        // Arrange
        var modifiedAllowance = new NutritionFacts
        {
            // Set unrealistic minimums that cannot be satisfied
            Calcium = double.MaxValue,
            Iron = double.MaxValue,
            Protein = double.MaxValue,
            VitaminA = double.MaxValue,
            VitaminB1 = double.MaxValue,
            VitaminB2 = double.MaxValue,
            VitaminC = double.MaxValue,
        };

        using var solver = CreateSolver();
        
        // Act
        var (foodsResult, nutrientsResult, resultStatus) = StiglerDietProgram.FindOptimalDiet(solver, modifiedAllowance, foodItems);
        
        // Assert
        Assert.True(resultStatus == Solver.ResultStatus.ABNORMAL, $"Expected ABNORMAL, but got {resultStatus}.");
        Assert.Null(foodsResult);
        Assert.Null(nutrientsResult);
    }
}