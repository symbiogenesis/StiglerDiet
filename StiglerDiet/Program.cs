namespace StiglerDiet;

using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using ConsoleTables;
using StiglerDiet.Models;
using StiglerDiet.Solvers;
using StiglerDiet.Solvers.Interfaces;

public class StiglerDietProgram
{
    static void Main()
    {
        using ISolver solver = GetSolver();

        // Load data from CSV files
        var minimumDailyAllowance = CsvParser.LoadMinimumDailyAllowance();
        var foodItems = CsvParser.LoadFoodItems();

        try
        {
            var optimalDailyDiet = FindOptimalDiet(solver, minimumDailyAllowance, foodItems);

            LogResults(solver, optimalDailyDiet, minimumDailyAllowance);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return;
        }
    }

    private static ISolver GetSolver()
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            return new LinearProgrammingSolver();
        }
        else
        {
            return new GoogleSolver();
        }
    }

    public static OptimalDailyDiet FindOptimalDiet(ISolver solver, NutritionFacts minimumDailyAllowance, List<FoodItem> foodItems)
    {
        ArgumentNullException.ThrowIfNull(solver);
        ArgumentNullException.ThrowIfNull(minimumDailyAllowance);
        ArgumentNullException.ThrowIfNull(foodItems);

        ValidateNutrition(minimumDailyAllowance, nameof(minimumDailyAllowance));
        foreach (var food in foodItems)
        {
            if (!double.IsFinite(food.Price) || food.Price <= 0 ||
                !double.IsFinite(food.Quantity) || food.Quantity <= 0 || food.NutritionFacts is null)
            {
                throw new ArgumentException("Each food must have a finite, positive price and quantity and nutrition facts.", nameof(foodItems));
            }
            ValidateNutrition(food.NutritionFacts, nameof(foodItems));
        }

        // This method owns the supplied model; repeated calls replace the previous diet.
        solver.Constraints.Clear();
        solver.Variables.Clear();
        solver.Objective.Coefficients.Clear();
        List<(FoodItem Food, Variable Variable)> foodVariables = [];

        foreach (FoodItem foodItem in foodItems)
        {
            var variable = solver.MakeNumVar(0.0, double.PositiveInfinity, $"{foodItem.Name} [{foodVariables.Count}]");
            foodVariables.Add((foodItem, variable));
        }

        // Add nutrient constraints
        for (int i = 0; i < NutritionFacts.Properties.Length; ++i)
        {
            double lowerBound = minimumDailyAllowance[i];

            var constraint = solver.MakeConstraint(lowerBound, double.PositiveInfinity, NutritionFacts.Properties[i].Name);

            foreach (var (foodItem, variable) in foodVariables)
            {
                constraint.SetCoefficient(variable, foodItem.NutritionFacts[i]);
            }
        }

        // Set objective function (minimize cost)
        var objective = solver.Objective;
    
        foreach (var (foodItem, variable) in foodVariables)
        {
            objective.SetCoefficient(variable, 1);
        }
    
        objective.SetMinimization();

        var resultStatus = solver.Solve();

        return BuildDailyDietResult(resultStatus, foodVariables, minimumDailyAllowance);
    }

    private static void ValidateNutrition(NutritionFacts nutrition, string parameterName)
    {
        for (int i = 0; i < NutritionFacts.Properties.Length; i++)
        {
            if (!double.IsFinite(nutrition[i]) || nutrition[i] < 0)
            {
                throw new ArgumentException("Nutrient values must be finite and nonnegative.", parameterName);
            }
        }
    }

    public static void LogResults(ISolver solver, OptimalDailyDiet optimalDailyDiet, NutritionFacts minimumDailyAllowance)
    {
        Console.WriteLine($"Number of variables = {solver.NumVariables()}");
        Console.WriteLine($"Number of constraints = {solver.NumConstraints()}");

        switch (optimalDailyDiet.ResultStatus)
        {
            case ResultStatus.OPTIMAL:
                break;
            case ResultStatus.FEASIBLE:
                Console.WriteLine("\nThe problem does not have an optimal solution!");
                Console.WriteLine("A potentially suboptimal solution was found.");
                break;
            default:
                Console.WriteLine("\nThe problem does not have an optimal solution!");
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

    private static OptimalDailyDiet BuildDailyDietResult(ResultStatus resultStatus,
        List<(FoodItem Food, Variable Variable)> foodVariables, NutritionFacts minimumDailyAllowance)
    {
        OptimalDailyDiet optimalDailyDiet = new(resultStatus);

        if (resultStatus is ResultStatus.OPTIMAL or ResultStatus.FEASIBLE)
        {
            double totalDailyPrice = 0;
            // Process results
            foreach (var (foodItem, variable) in foodVariables)
            {
                double dailyPrice = variable.SolutionValue();
                totalDailyPrice += dailyPrice;
                if (!double.IsFinite(totalDailyPrice) || dailyPrice < 0)
                {
                    return new OptimalDailyDiet(ResultStatus.ABNORMAL);
                }
                if (dailyPrice > 0.0)
                {
                    var dailyQuantity = dailyPrice / foodItem.Price;
                    if (!double.IsFinite(dailyQuantity * foodItem.Quantity))
                    {
                        return new OptimalDailyDiet(ResultStatus.ABNORMAL);
                    }

                    for (int j = 0; j < NutritionFacts.Properties.Length; ++j)
                    {
                        optimalDailyDiet.NutritionFacts[j] += foodItem.NutritionFacts[j] * dailyPrice;
                    }

                    OptimalDailyDietItem optimalDailyDietItem = (foodItem, dailyPrice, dailyQuantity);

                    optimalDailyDiet.Add(optimalDailyDietItem);
                }
            }

            // Verify the returned diet itself rather than trusting a solver status alone.
            for (int i = 0; i < NutritionFacts.Properties.Length; i++)
            {
                double actual = optimalDailyDiet.NutritionFacts[i];
                double minimum = minimumDailyAllowance[i];
                if (!double.IsFinite(actual) || actual < minimum - 1e-7 * Math.Max(1.0, minimum))
                {
                    return new OptimalDailyDiet(ResultStatus.ABNORMAL);
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
            var percentage = minimumDailyAllowance[i] > 0
                ? $"{dailyNutritionFacts[i] / minimumDailyAllowance[i] * 100:N2}%"
                : "N/A";
            nutrientsTable.AddRow(name, amount, percentage);
        }

        nutrientsTable.Write();
    }

    private static void DisplayFoodResults(IEnumerable<OptimalDailyDietItem> dailyFoodPrices, Period period)
    {
        var foodTable = new ConsoleTable("Food", $"{period} Quantity", $"{period} Price")
            .Configure(o => o.EnableCount = false);

        double totalCost = 0.0;
        foreach (var (foodItem, dailyPrice, dailyQuantity) in dailyFoodPrices)
        {
            double annualPrice = (int)period * dailyPrice;
            double annualQuantity = dailyQuantity * foodItem.Quantity * (int)period;
            foodTable.AddRow(foodItem.Name, $"{annualQuantity:N2} ({foodItem.Unit})", annualPrice.ToString("C2"));
            totalCost += annualPrice;
        }

        foodTable.AddRow("---", "---", "---");
        foodTable.AddRow("Total", null, totalCost.ToString("C2"));

        foodTable.Write();
    }

    private static string GetNutrientName(PropertyInfo propertyInfo)
    {
        var descriptionAttribute = propertyInfo.GetCustomAttribute<DescriptionAttribute>();
        return descriptionAttribute?.Description ?? propertyInfo.Name;
    }
}
