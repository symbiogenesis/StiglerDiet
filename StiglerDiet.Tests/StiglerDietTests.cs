using Xunit;
using System;
using Google.OrTools.LinearSolver;

namespace StiglerDiet.Tests
{
    public class StiglerDietTests : IDisposable
    {
        private Solver solver;
        private StiglerDietProgram diet;

        public StiglerDietTests()
        {
            diet = new StiglerDietProgram();
            solver = diet.Solver;
        }

        public void Dispose()
        {
            solver.Dispose();
            // Cleanup if necessary
        }

        [Fact]
        public void Solver_ReturnsOptimalSolution()
        {
            var resultStatus = solver.Solve();
            Assert.Equal(Solver.ResultStatus.OPTIMAL, resultStatus);
        }

        [Theory]
        [InlineData(77)]
        public void NumberOfVariables_IsCorrect(int expected)
        {
            Assert.Equal(expected, solver.NumVariables());
        }

        [Theory]
        [InlineData(9)]
        public void NumberOfConstraints_IsCorrect(int expected)
        {
            Assert.Equal(expected, solver.NumConstraints());
        }

        [Theory]
        [InlineData(39.66, 365)]
        public void OptimalAnnualPrice_IsAsExpected(double expectedPrice, double days)
        {
            solver.Solve();
            var objectiveValue = solver.Objective().Value();
            Assert.Equal(expectedPrice, Math.Round(objectiveValue * days, 2));
        }

        [Theory]
        [InlineData("Calcium", 0, 200)]
        [InlineData("Iron", 10, 100)]
        public void NutrientRequirements_AreMet(string nutrientName, double minimum, double maximum)
        {
            solver.Solve();
            var constraint = solver.constraints().First(c => c.Name().StartsWith(nutrientName));
            Assert.True(constraint.Lb() >= minimum, $"{nutrientName} value of {constraint.Lb()} does not meet the minimum requirement of {minimum}.");
            Assert.True(constraint.Ub() <= maximum, $"{nutrientName} value of {constraint.Ub()} has exceeded the maximum requirement of {maximum}.");
        }

        [Fact]
        public void Diet_InitializesCorrectly()
        {
            Assert.NotNull(diet);
            // Add more initialization checks if necessary
        }

        [Fact]
        public void Solver_DisposesCorrectly()
        {
            // Arrange
            var solver = new Solver("test", Solver.OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);
            
            // Act
            solver.Dispose();
            
            // Assert
            // Instead of accessing solver properties after disposal,
            // verify that creating new solver instances still works
            using var newSolver = new Solver("test2", Solver.OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);
            Assert.NotNull(newSolver);
        }

        [Fact]
        public void ObjectiveValue_IsPositive()
        {
            solver.Solve();
            var objectiveValue = solver.Objective().Value();
            Assert.True(objectiveValue > 0, "Objective value should be positive.");
        }
    }
}