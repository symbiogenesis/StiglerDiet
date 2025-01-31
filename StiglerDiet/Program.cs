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
        
        var optimalDailyDiet = FindOptimalDiet(solver, minimumDailyAllowance, foodItems);

        LogResults(solver, optimalDailyDiet, minimumDailyAllowance);
    }

    public static OptimalDailyDiet FindOptimalDiet(Solver solver, NutritionFacts minimumDailyAllowance, List<FoodItem> foodItems)
    {
        Dictionary<FoodItem, Variable> foodVariables = [];

        foreach (FoodItem foodItem in foodItems)
        {
            var variable = solver.MakeNumVar(0.0, double.PositiveInfinity, foodItem.Name);
            foodVariables.Add(foodItem, variable);
        }

        // Add nutrient constraints
        for (int i = 0; i < NutritionFacts.Properties.Length; ++i)
        {
            double lowerBound = minimumDailyAllowance[i];

            var constraint = solver.MakeConstraint(lowerBound, double.PositiveInfinity, NutritionFacts.Properties[i].Name);

            foreach (var foodItem in foodItems)
            {
                constraint.SetCoefficient(foodVariables[foodItem], foodItem.NutritionFacts[i]);
            }
        }

        // Set objective function (minimize cost)
        Objective objective = solver.Objective();
    
        foreach (var (foodItem, variable) in foodVariables)
        {
            objective.SetCoefficient(variable, 1);
        }
    
        objective.SetMinimization();

        var resultStatus = solver.Solve();

        return BuildDailyDietResult(resultStatus, foodVariables);
    }

    public static void LogResults(Solver solver, OptimalDailyDiet optimalDailyDiet, NutritionFacts minimumDailyAllowance)
    {
        Console.WriteLine($"Number of variables = {solver.NumVariables()}");
        Console.WriteLine($"Number of constraints = {solver.NumConstraints()}");

        switch (optimalDailyDiet.ResultStatus)
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

        if (optimalDailyDiet.Count == 0)
        {
            return;
        }

        Console.WriteLine();

        DisplayFoodResults(optimalDailyDiet, Period.Daily);

        DisplayFoodResults(optimalDailyDiet, Period.Annual);

        DisplayNutritionFacts(minimumDailyAllowance, optimalDailyDiet.NutritionFacts);

        Console.WriteLine("\nAdvanced usage:");
        Console.WriteLine($"Problem solved in {solver.WallTime()} milliseconds");
        Console.WriteLine($"Problem solved in {solver.Iterations()} iterations");
    }

    private static OptimalDailyDiet BuildDailyDietResult(ResultStatus resultStatus, Dictionary<FoodItem, Variable> foodVariables)
    {
        OptimalDailyDiet optimalDailyDiet = new(resultStatus);

        if (resultStatus is ResultStatus.OPTIMAL or ResultStatus.FEASIBLE)
        {
            // Process results
            foreach (var (foodItem, variable) in foodVariables)
            {
                double dailyPrice = variable.SolutionValue();
                if (dailyPrice > 0.0)
                {
                    for (int j = 0; j < NutritionFacts.Properties.Length; ++j)
                    {
                        optimalDailyDiet.NutritionFacts[j] += foodItem.NutritionFacts[j] * dailyPrice;
                    }

                    OptimalDailyDietItem optimalDailyDietItem = (foodItem, dailyPrice, dailyPrice / foodItem.Price);

                    optimalDailyDiet.Add(optimalDailyDietItem);
                }
            }
        }

        return optimalDailyDiet;
    }

    private static void DisplayNutritionFacts(NutritionFacts minimumDailyAllowance, NutritionFacts dailyNutritionFacts)
    {
        var nutrientsTable = new ConsoleTable("Nutrient", "Amount", "% of RDA")
            .Configure(o => o.EnableCount = false);

        for (int i = 0; i < NutritionFacts.Properties.Length; ++i)
        {
            var propertyInfo = NutritionFacts.Properties[i];
            var name = GetNutrientName(propertyInfo);
            var amount = dailyNutritionFacts[i].ToString("N2");
            var percentage = dailyNutritionFacts[i] / minimumDailyAllowance[i] * 100;
            nutrientsTable.AddRow(name, amount, $"{percentage:N2}%");
        }

        nutrientsTable.Write();
    }

    private static void DisplayFoodResults(IEnumerable<OptimalDailyDietItem> dailyFoodPrices, Period period)
    {
        var annualTable = new ConsoleTable("Food", $"{period} Quantity", $"{period} Price")
            .Configure(o => o.EnableCount = false);

        double totalCost = 0.0;
        foreach (var (foodItem, dailyPrice, dailyQuantity) in dailyFoodPrices)
        {
            double annualPrice = (int)period * dailyPrice;
            double annualQuantity = (int)period * dailyQuantity;
            annualTable.AddRow(foodItem.Name, $"{annualQuantity:N2} ({foodItem.Unit})", annualPrice.ToString("C2"));
            totalCost += annualPrice;
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
