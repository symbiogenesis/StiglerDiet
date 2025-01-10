namespace StiglerDiet;

using ConsoleTables;
using Google.OrTools.LinearSolver;
using System.Collections.Generic;
using StiglerDiet.Models;
using System.ComponentModel;
using System.Reflection;

public class StiglerDietProgram
{
    static void Main()
    {
        using var solver = new Solver("StiglerDietSolver", Solver.OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);
        
        var (foodResults, nutrientsResult, resultStatus) = FindOptimalDiet(solver, OriginalConstants.RecommendedDailyAllowance, OriginalConstants.FoodItems);

        Console.WriteLine($"Number of variables = {solver.NumVariables()}");
        Console.WriteLine($"Number of constraints = {solver.NumConstraints()}");

        switch (resultStatus)
        {
            case Solver.ResultStatus.OPTIMAL:
                break;
            case Solver.ResultStatus.FEASIBLE:
                Console.WriteLine();
                Console.WriteLine("The problem does not have an optimal solution!");
                Console.WriteLine("A potentially suboptimal solution was found.");
                break;
            default:
                Console.WriteLine();
                Console.WriteLine("The problem does not have an optimal solution!");
                Console.WriteLine("The solver could not solve the problem.");
                return;
        }

        if (foodResults is null || nutrientsResult is null)
        {
            return;
        }

        Console.WriteLine();

        DisplayDailyFoods(foodResults);

        DisplayAnnualFoods(foodResults);

        DisplayNutrients(OriginalConstants.RecommendedDailyAllowance, nutrientsResult);

        Console.WriteLine("\nAdvanced usage:");
        Console.WriteLine($"Problem solved in {solver.WallTime()} milliseconds");
        Console.WriteLine($"Problem solved in {solver.Iterations()} iterations");
    }

    public static (IEnumerable<FoodResult>?, NutritionFacts?, Solver.ResultStatus) FindOptimalDiet(Solver solver, NutritionFacts recommendedDailyAllowance, List<FoodItem> foodItems)
    {
        List<Variable> foods = [];

        for (int i = 0; i < foodItems.Count; ++i)
        {
            foods.Add(solver.MakeNumVar(0.0, double.PositiveInfinity, foodItems[i].Name));
        }

        List<Constraint> constraints = [];

        for (int i = 0; i < NutritionFacts.Properties.Value.Length; ++i)
        {
            Constraint constraint =
                solver.MakeConstraint(recommendedDailyAllowance[i], double.PositiveInfinity, NutritionFacts.Properties.Value[i].Name);
            for (int j = 0; j < foodItems.Count; ++j)
            {
                constraint.SetCoefficient(foods[j], foodItems[j].NutritionFacts[i]);
            }
            constraints.Add(constraint);
        }

        Objective objective = solver.Objective();
        for (int i = 0; i < foodItems.Count; ++i)
        {
            objective.SetCoefficient(foods[i], 1);
        }
        objective.SetMinimization();

        Solver.ResultStatus resultStatus = solver.Solve();

        if (resultStatus is not Solver.ResultStatus.OPTIMAL and not Solver.ResultStatus.FEASIBLE)
        {
            return (null, null, resultStatus);
        }

        NutritionFacts nutrientsResult = new();

        List<FoodResult> foodResults = [];

        for (int i = 0; i < foods.Count; ++i)
        {
            double dailyPrice = foods[i].SolutionValue();
            if (dailyPrice > 0.0)
            {
                for (int j = 0; j < NutritionFacts.Properties.Value.Length; ++j)
                {
                    nutrientsResult[j] += foodItems[i].NutritionFacts[j] * dailyPrice;
                }

                foodResults.Add((foodItems[i], dailyPrice));
            }
        }

        return (foodResults, nutrientsResult, resultStatus);
    }

    public static void DisplayNutritionFacts(NutritionFacts recommendedDailyAllowance, NutritionFacts nutrientsResult)
    {
        var nutrientsTable = new ConsoleTable("Nutrient", "Amount", "% of RDA")
            .Configure(o => o.EnableCount = false);

        for (int i = 0; i < NutritionFacts.Properties.Value.Length; ++i)
        {
            var propertyInfo = NutritionFacts.Properties.Value[i];
            var name = GetNutrientName(propertyInfo);
            var amount = nutrientsResult[i].ToString("N2");
            var percentage = nutrientsResult[i] / recommendedDailyAllowance[i] * 100;
            nutrientsTable.AddRow(name, amount, $"{percentage:N2}%");
        }

        nutrientsTable.Write();
    }

    public static void DisplayDailyFoods(IEnumerable<FoodResult> foods) => DisplayFoodResults(foods, "Daily", 1);

    public static void DisplayAnnualFoods(IEnumerable<FoodResult> foods) => DisplayFoodResults(foods, "Annual", 365);

    private static void DisplayFoodResults(IEnumerable<FoodResult> foods, string label, int multiplier)
    {
        var annualTable = new ConsoleTable("Food", $"{label} Quantity", $"{label} Cost ($)")
            .Configure(o => o.EnableCount = false);

        double totalCost = 0.0;
        foreach (var (food, dailyPrice) in foods)
        {
            double annualCost = multiplier * dailyPrice;
            double annualQuantity = multiplier * (dailyPrice / food.Price) * food.Quantity;
            annualTable.AddRow(food.Name, $"{annualQuantity:N2} ({food.Unit})", annualCost.ToString("N2"));
            totalCost += annualCost;
        }

        annualTable.AddRow("---", "---", "---");
        annualTable.AddRow("Total", null, totalCost.ToString("N2"));

        annualTable.Write();
    }

    private static string GetNutrientName(PropertyInfo propertyInfo)
    {
        var descriptionAttribute = propertyInfo.GetCustomAttribute<DescriptionAttribute>();
        return descriptionAttribute?.Description ?? propertyInfo.Name;
    }
}
