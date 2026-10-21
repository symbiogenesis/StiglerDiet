namespace StiglerDiet.Tests;

using StiglerDiet.Models;
using StiglerDiet.Solvers;

public class OptimalityCertificateTests
{
    [Fact]
    public void HistoricalDietMatchesAnIndependentDualLowerBound()
    {
        var foods = CsvParser.LoadFoodItems();
        var requirements = CsvParser.LoadMinimumDailyAllowance();

        // A dual certificate for min 1'x subject to Ax >= b, x >= 0.
        // These nonnegative nutrient prices satisfy A'y <= 1 for every food.
        // Therefore b'y is a lower bound on every feasible diet's cost.
        // No solver or published Google solution is used to compute the bound.
        static double NutrientValue(NutritionFacts facts) =>
            8.765147298049495e-6 * facts.Calories +
            0.03173771344563703 * facts.Calcium +
            4.002327217253813e-7 * facts.VitaminA +
            0.016358032699276687 * facts.VitaminB2 +
            0.00014411751545899708 * facts.VitaminC;

        Assert.All(foods, food => Assert.InRange(NutrientValue(food.NutritionFacts), 0, 1 + 1e-12));
        double lowerBound = NutrientValue(requirements);

        using var solver = new LinearProgrammingSolver();
        var diet = StiglerDietProgram.FindOptimalDiet(solver, requirements, foods);

        Assert.Equal(ResultStatus.OPTIMAL, diet.ResultStatus);
        foreach (var property in NutritionFacts.Properties)
        {
            double minimum = (double)property.GetValue(requirements)!;
            double actual = diet.Sum(item => (double)property.GetValue(item.Food.NutritionFacts)! * item.Price);
            Assert.True(actual >= minimum - 1e-8 * Math.Max(1, minimum), property.Name);
        }
        Assert.InRange(Math.Abs(diet.Sum(item => item.Price) - lowerBound), 0, 1e-10);
    }
}
