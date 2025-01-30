namespace StiglerDiet;

using System.ComponentModel;
using System.Reflection;
using ConsoleTables;
using Google.OrTools.LinearSolver;
using StiglerDiet.Models;
using static Google.OrTools.LinearSolver.Solver;

public class StiglerDietProgram
{
    static void Main()
    {
        using var solver = new Solver("StiglerDietSolver", OptimizationProblemType.GLOP_LINEAR_PROGRAMMING);

        // Load data from CSV files
        var minimumDailyAllowance = CsvParser.LoadMinimumDailyAllowance();
        var foodItems = CsvParser.LoadFoodItems();
        
        var (dailyFoodPrices, nutritionFactsResult, resultStatus) = FindOptimalDiet(solver, minimumDailyAllowance, foodItems);

        LogResults(solver, dailyFoodPrices, nutritionFactsResult, minimumDailyAllowance, resultStatus);
    }

    public static (IEnumerable<DailyFoodPrice>?, NutritionFacts?, ResultStatus) FindOptimalDiet(Solver solver, NutritionFacts minimumDailyAllowance, List<FoodItem> foodItems)
    {
        List<Variable> variables = [];

        for (int i = 0; i < foodItems.Count; i++)
        {
            FoodItem foodItem = foodItems[i];
            var variable = solver.MakeNumVar(0.0, double.PositiveInfinity, foodItem.Name);
            variables.Add(variable);
        }

        // Add nutrient constraints
        for (int i = 0; i < NutritionFacts.Properties.Length; ++i)
        {
            Constraint constraint =
                solver.MakeConstraint(minimumDailyAllowance[i], double.PositiveInfinity, NutritionFacts.Properties[i].Name);
            for (int j = 0; j < foodItems.Count; ++j)
            {
                constraint.SetCoefficient(variables[j], foodItems[j].NutritionFacts[i]);
            }
        }

        // Set objective function (minimize cost)
        Objective objective = solver.Objective();
        for (int i = 0; i < foodItems.Count; ++i)
        {
            objective.SetCoefficient(variables[i], 1);
        }
        objective.SetMinimization();

        ResultStatus resultStatus = solver.Solve();

        if (resultStatus is not ResultStatus.OPTIMAL and not ResultStatus.FEASIBLE)
        {
            return (null, null, resultStatus);
        }

        // Process results
        NutritionFacts nutritionFactsResult = new();

        List<DailyFoodPrice> dailyFoodPrices = [];

        for (int i = 0; i < variables.Count; ++i)
        {
            double dailyPrice = variables[i].SolutionValue();
            if (dailyPrice > 0.0)
            {
                for (int j = 0; j < NutritionFacts.Properties.Length; ++j)
                {
                    nutritionFactsResult[j] += foodItems[i].NutritionFacts[j] * dailyPrice;
                }

                dailyFoodPrices.Add((foodItems[i], dailyPrice));
            }
        }

        return (dailyFoodPrices, nutritionFactsResult, resultStatus);
    }

    public static void LogResults(Solver solver, IEnumerable<DailyFoodPrice>? dailyFoodPrices, NutritionFacts? nutritionFactsResult, NutritionFacts minimumDailyAllowance, ResultStatus resultStatus)
    {
        Console.WriteLine($"Number of variables = {solver.NumVariables()}");
        Console.WriteLine($"Number of constraints = {solver.NumConstraints()}");

        switch (resultStatus)
        {
            case ResultStatus.OPTIMAL:
                break;
            case ResultStatus.FEASIBLE:
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

        if (dailyFoodPrices is null || nutritionFactsResult is null)
        {
            return;
        }

        Console.WriteLine();

        DisplayFoodResults(dailyFoodPrices, Period.Daily);

        DisplayFoodResults(dailyFoodPrices, Period.Annual);

        DisplayNutritionFacts(minimumDailyAllowance, nutritionFactsResult);

        Console.WriteLine("\nAdvanced usage:");
        Console.WriteLine($"Problem solved in {solver.WallTime()} milliseconds");
        Console.WriteLine($"Problem solved in {solver.Iterations()} iterations");
    }

    private static void DisplayNutritionFacts(NutritionFacts minimumDailyAllowance, NutritionFacts nutrientsResult)
    {
        var nutrientsTable = new ConsoleTable("Nutrient", "Amount", "% of RDA")
            .Configure(o => o.EnableCount = false);

        for (int i = 0; i < NutritionFacts.Properties.Length; ++i)
        {
            var propertyInfo = NutritionFacts.Properties[i];
            var name = GetNutrientName(propertyInfo);
            var amount = nutrientsResult[i].ToString("N2");
            var percentage = nutrientsResult[i] / minimumDailyAllowance[i] * 100;
            nutrientsTable.AddRow(name, amount, $"{percentage:N2}%");
        }

        nutrientsTable.Write();
    }

    private static void DisplayFoodResults(IEnumerable<DailyFoodPrice> dailyFoodPrices, Period period)
    {
        var annualTable = new ConsoleTable("Food", $"{period} Quantity", $"{period} Cost")
            .Configure(o => o.EnableCount = false);

        double totalCost = 0.0;
        foreach (var (foodItem, dailyPrice) in dailyFoodPrices)
        {
            double annualCost = (int)period * dailyPrice;
            double annualQuantity = (int)period * (dailyPrice / foodItem.Price) * foodItem.Quantity;
            annualTable.AddRow(foodItem.Name, $"{annualQuantity:N2} ({foodItem.Unit})", annualCost.ToString("C2"));
            totalCost += annualCost;
        }

        annualTable.AddRow("---", "---", "---");
        annualTable.AddRow("Total", null, totalCost.ToString("C2"));

        annualTable.Write();
    }

    private static string GetNutrientName(PropertyInfo propertyInfo)
    {
        var descriptionAttribute = propertyInfo.GetCustomAttribute<DescriptionAttribute>();
        return descriptionAttribute?.Description ?? propertyInfo.Name;
    }
}
