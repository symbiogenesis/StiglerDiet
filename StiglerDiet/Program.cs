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
        
        var (foodsResult, nutrientsResult) = FindOptimalDiet(solver, Constants.RecommendedDailyAllowance, Constants.FoodItems);

        if (foodsResult is null || nutrientsResult is null)
        {
            return;
        }

        Console.WriteLine();

        DisplayNutrients(Constants.RecommendedDailyAllowance, nutrientsResult);

        Console.WriteLine("\nAdvanced usage:");
        Console.WriteLine($"Problem solved in {solver.WallTime()} milliseconds");
        Console.WriteLine($"Problem solved in {solver.Iterations()} iterations");
    }

    public static (IEnumerable<FoodItem>?, NutritionFacts?) FindOptimalDiet(Solver solver, NutritionFacts recommendedDailyAllowance, List<FoodItem> foodItems)
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

        // Check that the problem has an optimal solution.
        if (resultStatus != Solver.ResultStatus.OPTIMAL)
        {
            Console.WriteLine();

            Console.WriteLine("The problem does not have an optimal solution!");
            if (resultStatus == Solver.ResultStatus.FEASIBLE)
            {
                Console.WriteLine("A potentially suboptimal solution was found.");
            }
            else
            {
                Console.WriteLine("The solver could not solve the problem.");
                return (null, null);
            }
        }

        Console.WriteLine();

        // Display the amounts (in dollars) to purchase of each food.
        NutritionFacts nutrientsResult = new();
        var foodsResult = DisplayAnnualFoods(foods, foodItems, ref nutrientsResult);

        Console.WriteLine($"\nOptimal annual price: ${365 * objective.Value():N2}");

        return (foodsResult, nutrientsResult);
    }

    public static List<FoodItem> DisplayAnnualFoods(List<Variable> foods, List<FoodItem> nutritionFacts, ref NutritionFacts nutrientsResult)
    {
        var annualTable = new ConsoleTable("Food", "Annual Cost ($)");
        var result = new List<FoodItem>();

        for (int i = 0; i < foods.Count; ++i)
        {
            if (foods[i].SolutionValue() > 0.0)
            {
                annualTable.AddRow(nutritionFacts[i].Name, (365 * foods[i].SolutionValue()).ToString("N2"));

                for (int j = 0; j < NutritionFacts.Properties.Value.Length; ++j)
                {
                    nutrientsResult[j] += nutritionFacts[i].NutritionFacts[j] * foods[i].SolutionValue();
                }

                result.Add(nutritionFacts[i]);
            }
        }
        annualTable.Write();
        return result;
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
