using Xunit;
using System;
using Google.OrTools.LinearSolver;

namespace StiglerDiet.Tests
{
    public class StiglerDietTests : IDisposable
    {
        private Solver solver;
        private StiglerDiet diet;

        public StiglerDietTests()
        {
            solver = Solver.CreateSolver("GLOP");
            diet = new StiglerDiet(solver);
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
        [InlineData(80)]
        public void NumberOfVariables_IsCorrect(int expected)
        {
            Assert.Equal(expected, solver.NumVariables());
        }

        [Theory]
        [InlineData(9)]
        [InlineData(10)]
        public void NumberOfConstraints_IsCorrect(int expected)
        {
            Assert.Equal(expected, solver.NumConstraints());
        }

        [Theory]
        [InlineData(39.66, 365)]
        [InlineData(50.00, 300)]
        public void OptimalAnnualPrice_IsAsExpected(double expectedPrice, double days)
        {
            solver.Solve();
            var objectiveValue = solver.Objective().Value();
            Assert.Equal(expectedPrice, Math.Round(objectiveValue * days, 2));
        }

        [Theory]
        [InlineData("Calcium", 100)]
        [InlineData("Iron", 50)]
        public void NutrientRequirements_AreMet(string nutrientName, double minimum)
        {
            solver.Solve();
            var constraint = solver.Constraint(nutrientName, double.PositiveInfinity);
            Assert.True(constraint.Bound() >= minimum, $"{nutrientName} does not meet the minimum requirement.");
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
            solver.Dispose();
            // Verify that solver is disposed
            Assert.Throws<ObjectDisposedException>(() => solver.NumVariables());
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