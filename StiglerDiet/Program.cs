namespace StiglerDiet;

using ConsoleTables;
using Google.OrTools.LinearSolver;
using System.Collections.Generic;
using StiglerDiet.Models;

public class StiglerDietProgram
{
    static void Main()
    {
        using var solver = new Solver("StiglerDietSolver", Solver.OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);
        
        var (foodResults, nutrientsResult, resultStatus) = FindOptimalDiet(solver, Constants.RecommendedDailyAllowance, Constants.FoodItems);

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

        LogAnnualFoods(foodResults);

        double optimalAnnualPrice = foodResults.Sum(food => food.DailyPrice) * 365;

        Console.WriteLine($"\nOptimal annual price: ${optimalAnnualPrice:N2}");

        Console.WriteLine();

        DisplayNutrients(Constants.RecommendedDailyAllowance, nutrientsResult);

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

    public static void LogAnnualFoods(IEnumerable<FoodResult> foods)
    {
        var annualTable = new ConsoleTable("Food", "Annual Cost ($)")
            .Configure(o => o.EnableCount = false);

        foreach (var (food, dailyPrice) in foods)
        {
            annualTable.AddRow(food.Name, (365 * dailyPrice).ToString("N2"));
        }

        annualTable.Write();
    }

    public static void DisplayNutrients(NutritionFacts recommendedDailyAllowance, NutritionFacts nutrientsResult)
    {
        var nutrientsTable = new ConsoleTable("Nutrient", "Amount", "Minimum Required")
            .Configure(o => o.EnableCount = false);

        for (int i = 0; i < NutritionFacts.Properties.Value.Length; ++i)
        {
            var property = NutritionFacts.Properties.Value[i];
            var name = property.Name;
            var minimumRequired = property.GetValue(recommendedDailyAllowance) ?? throw new ArgumentException($"Property value is null for index: {i}");
            var amount = nutrientsResult[i].ToString("N2");

            nutrientsTable.AddRow(name, amount, (double)minimumRequired);
        }

        nutrientsTable.Write();
    }
}
