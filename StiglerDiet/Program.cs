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
        
        var (foodsResult, nutrientsResult, resultStatus, optimalPrice) = FindOptimalDiet(solver, Constants.RecommendedDailyAllowance, Constants.FoodItems);

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

        if (foodsResult is null || nutrientsResult is null)
        {
            return;
        }

        Console.WriteLine();

        LogAnnualFoods(foodsResult);

        Console.WriteLine($"\nOptimal annual price: ${optimalPrice:N2}");

        Console.WriteLine();

        DisplayNutrients(Constants.RecommendedDailyAllowance, nutrientsResult);

        Console.WriteLine("\nAdvanced usage:");
        Console.WriteLine($"Problem solved in {solver.WallTime()} milliseconds");
        Console.WriteLine($"Problem solved in {solver.Iterations()} iterations");
    }

    public static (IEnumerable<(FoodItem Food, double Quantity)>?, NutritionFacts?, Solver.ResultStatus, double) FindOptimalDiet(Solver solver, NutritionFacts recommendedDailyAllowance, List<FoodItem> foodItems)
    {
        List<Variable> foods = [];

        for (int i = 0; i < foodItems.Count; ++i)
        {
            foods.Add(solver.MakeNumVar(0.0, double.PositiveInfinity, foodItems[i].Name));
        }

        Console.WriteLine($"Number of variables = {solver.NumVariables()}");

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

        Console.WriteLine($"Number of constraints = {solver.NumConstraints()}");

        Objective objective = solver.Objective();
        for (int i = 0; i < foodItems.Count; ++i)
        {
            objective.SetCoefficient(foods[i], 1);
        }
        objective.SetMinimization();

        Solver.ResultStatus resultStatus = solver.Solve();

        NutritionFacts nutrientsResult = new();

        var foodsResult = CalculateAnnualFoods(foods, foodItems, ref nutrientsResult);

        var optimalPrice = 365 * objective.Value();

        return (foodsResult, nutrientsResult, resultStatus, optimalPrice);
    }

    public static List<(FoodItem Food, double Quantity)> CalculateAnnualFoods(List<Variable> foods, List<FoodItem> foodItems, ref NutritionFacts nutrientsResult)
    {
        var result = new List<(FoodItem, double)>();

        for (int i = 0; i < foods.Count; ++i)
        {
            double quantity = foods[i].SolutionValue();
            if (quantity > 0.0)
            {
                for (int j = 0; j < NutritionFacts.Properties.Value.Length; ++j)
                {
                    nutrientsResult[j] += foodItems[i].NutritionFacts[j] * quantity;
                }

                result.Add((foodItems[i], quantity));
            }
        }

        return result;
    }

    public static void LogAnnualFoods(IEnumerable<(FoodItem Food, double Quantity)> foods)
    {
        var annualTable = new ConsoleTable("Food", "Annual Cost ($)");

        foreach (var (food, quantity) in foods)
        {
            annualTable.AddRow(food.Name, (365 * quantity).ToString("N2"));
        }
        annualTable.Write();
    }

    public static void DisplayNutrients(NutritionFacts recommendedDailyAllowance, NutritionFacts nutrientsResult)
    {
        var nutrientsTable = new ConsoleTable("Nutrient", "Amount", "Minimum Required");

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