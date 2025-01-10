using Google.OrTools.LinearSolver;
using StiglerDiet.Models;

namespace StiglerDiet.Tests
{
    public class OriginalStiglerDietTests
    {
        private readonly NutritionFacts recommendedDailyAllowance;
        private readonly List<FoodItem> foodItems;

        public OriginalStiglerDietTests()
        {
            recommendedDailyAllowance = OriginalConstants.RecommendedDailyAllowance;
            foodItems = OriginalConstants.FoodItems;
        }

        private static Solver CreateSolver() => new("test", Solver.OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);

        [Fact]
        public void Solver_ReturnsOptimalSolution()
        {
            // Arrange
            using var solver = CreateSolver();

            // Act
            var (foodsResult, nutrientsResult, resultStatus) = StiglerDietProgram.FindOptimalDiet(solver, recommendedDailyAllowance, foodItems);

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
            StiglerDietProgram.FindOptimalDiet(solver, recommendedDailyAllowance, foodItems);

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
            StiglerDietProgram.FindOptimalDiet(solver, recommendedDailyAllowance, foodItems);

            // Assert
            Assert.Equal(expected, solver.NumConstraints());
        }

        [Theory]
        [InlineData(39.66, 365)]
        public void OptimalAnnualPrice_IsAsExpected(double expectedPrice, double days)
        {
            // Arrange
            using var solver = CreateSolver();

            // Act
            var (foodsResult, nutrientsResult, resultStatus) = StiglerDietProgram.FindOptimalDiet(solver, recommendedDailyAllowance, foodItems);
            var objectiveValue = solver.Objective().Value();

            // Assert
            Assert.Equal(expectedPrice, Math.Round(objectiveValue * days, 2));
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
            StiglerDietProgram.FindOptimalDiet(solver, recommendedDailyAllowance, foodItems);
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
            var (foodsResult, nutrientsResult, resultStatus) = StiglerDietProgram.FindOptimalDiet(solver, recommendedDailyAllowance, foodItems);

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
            StiglerDietProgram.FindOptimalDiet(solver, recommendedDailyAllowance, foodItems);
            var objectiveValue = solver.Objective().Value();

            // Assert
            Assert.True(objectiveValue > 0, "Objective value should be positive.");
        }
    }
}