﻿using Xunit;
using System;
using System.Collections.Generic;
using Google.OrTools.LinearSolver;
using StiglerDiet.Models;

namespace StiglerDiet.Tests
{
    public class StiglerDietTests
    {
        private readonly NutritionFacts recommendedDailyAllowance;
        private readonly List<FoodItem> foodItems;

        public StiglerDietTests()
        {
            recommendedDailyAllowance = StiglerDietProgram.RecommendedDailyAllowance;
            foodItems = StiglerDietProgram.FoodItems;
        }

        [Fact]
        public void Solver_ReturnsOptimalSolution()
        {
            using var solver = new Solver("test", Solver.OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);
            var (foodsResult, nutrientsResult) = StiglerDietProgram.FindOptimalDiet(solver, recommendedDailyAllowance, foodItems);
            Assert.NotNull(foodsResult);
            Assert.NotNull(nutrientsResult);
        }

        [Theory]
        [InlineData(77)]
        public void NumberOfVariables_IsCorrect(int expected)
        {
            using var solver = new Solver("test", Solver.OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);
            StiglerDietProgram.FindOptimalDiet(solver, recommendedDailyAllowance, foodItems);
            Assert.Equal(expected, solver.NumVariables());
        }

        [Theory]
        [InlineData(9)]
        public void NumberOfConstraints_IsCorrect(int expected)
        {
            using var solver = new Solver("test", Solver.OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);
            StiglerDietProgram.FindOptimalDiet(solver, recommendedDailyAllowance, foodItems);
            Assert.Equal(expected, solver.NumConstraints());
        }

        [Theory]
        [InlineData(39.66, 365)]
        public void OptimalAnnualPrice_IsAsExpected(double expectedPrice, double days)
        {
            using var solver = new Solver("test", Solver.OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);
            var (foodsResult, nutrientsResult) = StiglerDietProgram.FindOptimalDiet(solver, recommendedDailyAllowance, foodItems);
            var objectiveValue = solver.Objective().Value();
            Assert.Equal(expectedPrice, Math.Round(objectiveValue * days, 2));
        }

        [Theory]
        [InlineData("Calcium", 0)]
        [InlineData("Iron", 10)]
        public void NutrientRequirements_AreMet(string nutrientName, double minimum)
        {
            using var solver = new Solver("test", Solver.OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);
            StiglerDietProgram.FindOptimalDiet(solver, recommendedDailyAllowance, foodItems);
            var constraint = solver.constraints().First(c => c.Name().StartsWith(nutrientName));
            Assert.True(constraint.Lb() >= minimum, $"{nutrientName} value of {constraint.Lb()} does not meet the minimum requirement of {minimum}.");
        }

        [Fact]
        public void Diet_InitializesCorrectly()
        {
            using var solver = new Solver("test", Solver.OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);
            var (foodsResult, nutrientsResult) = StiglerDietProgram.FindOptimalDiet(solver, recommendedDailyAllowance, foodItems);
            Assert.NotNull(foodsResult);
            Assert.NotNull(nutrientsResult);
        }

        [Fact]
        public void Solver_DisposesCorrectly()
        {
            using var solver = new Solver("test", Solver.OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);
            solver.Dispose();
            using var newSolver = new Solver("test2", Solver.OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);
            Assert.NotNull(newSolver);
        }

        [Fact]
        public void ObjectiveValue_IsPositive()
        {
            using var solver = new Solver("test", Solver.OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);
            StiglerDietProgram.FindOptimalDiet(solver, recommendedDailyAllowance, foodItems);
            var objectiveValue = solver.Objective().Value();
            Assert.True(objectiveValue > 0, "Objective value should be positive.");
        }
    }
}