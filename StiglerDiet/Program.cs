namespace StiglerDiet;

using ConsoleTables;
using Google.OrTools.LinearSolver;
using System.Collections.Generic;
using StiglerDiet.Models;

public class StiglerDietProgram
{
    public Solver Solver { get; } = Solver.CreateSolver("GLOP");

    public StiglerDietProgram()
    {
        Initialize(Solver);
    }

    static void Main()
    {
        Initialize(new Solver("StiglerDietSolver", Solver.OptimizationProblemType.GLOP_LINEAR_PROGRAMMING));
    }

    static void Initialize(Solver solver)
    {
        // Nutrient minimums.
        NutritionFacts recommendedDailyAllowance = new()
        {
            Calories = 3.0,
            Protein = 70.0,
            Calcium = 0.8,
            Iron = 12.0,
            VitaminA = 5.0,
            VitaminB1 = 1.8,
            VitaminB2 = 2.7,
            Niacin = 18.0,
            VitaminC = 75.0
        };

        List<FoodItem> foodItems =
        [
            new("Wheat Flour (Enriched)", "10 lb.", 36, new(44.7, 1411, 2, 365, 0, 55.4, 33.3, 441, 0)),
            new("Macaroni", "1 lb.", 14.1, new(11.6, 418, 0.7, 54, 0, 3.2, 1.9, 68, 0)),
            new("Wheat Cereal (Enriched)", "28 oz.", 24.2, new(11.8, 377, 14.4, 175, 0, 14.4, 8.8, 114, 0)),
            new("Corn Flakes", "8 oz.", 7.1, new(11.4, 252, 0.1, 56, 0, 13.5, 2.3, 68, 0)),
            new("Corn Meal", "1 lb.", 4.6, new(36.0, 897, 1.7, 99, 30.9, 17.4, 7.9, 106, 0)),
            new("Hominy Grits", "24 oz.", 8.5, new(28.6, 680, 0.8, 80, 0, 10.6, 1.6, 110, 0)),
            new("Rice", "1 lb.", 7.5, new(21.2, 460, 0.6, 41, 0, 2, 4.8, 60, 0)),
            new("Rolled Oats", "1 lb.", 7.1, new(25.3, 907, 5.1, 341, 0, 37.1, 8.9, 64, 0)),
            new("White Bread (Enriched)", "1 lb.", 7.9, new(15.0, 488, 2.5, 115, 0, 13.8, 8.5, 126, 0)),
            new("Whole Wheat Bread", "1 lb.", 9.1, new(12.2, 484, 2.7, 125, 0, 13.9, 6.4, 160, 0)),
            new("Rye Bread", "1 lb.", 9.1, new(12.4, 439, 1.1, 82, 0, 9.9, 3, 66, 0)),
            new("Pound Cake", "1 lb.", 24.8, new(8.0, 130, 0.4, 31, 18.9, 2.8, 3, 17, 0)),
            new("Soda Crackers", "1 lb.", 15.1, new(12.5, 288, 0.5, 50, 0, 0, 0, 0, 0)),
            new("Milk", "1 qt.", 11, new(6.1, 310, 10.5, 18, 16.8, 4, 16, 7, 177)),
            new("Evaporated Milk (can)", "14.5 oz.", 6.7, new(8.4, 422, 15.1, 9, 26, 3, 23.5, 11, 60)),
            new("Butter", "1 lb.", 30.8, new(10.8, 9, 0.2, 3, 44.2, 0, 0.2, 2, 0)),
            new("Oleomargarine", "1 lb.", 16.1, new(20.6, 17, 0.6, 6, 55.8, 0.2, 0, 0, 0)),
            new("Eggs", "1 doz.", 32.6, new(2.9, 238, 1.0, 52, 18.6, 2.8, 6.5, 1, 0)),
            new("Cheese (Cheddar)", "1 lb.", 24.2, new(7.4, 448, 16.4, 19, 28.1, 0.8, 10.3, 4, 0)),
            new("Cream", "1/2 pt.", 14.1, new(3.5, 49, 1.7, 3, 16.9, 0.6, 2.5, 0, 17)),
            new("Peanut Butter", "1 lb.", 17.9, new(15.7, 661, 1.0, 48, 0, 9.6, 8.1, 471, 0)),
            new("Mayonnaise", "1/2 pt.", 16.7, new(8.6, 18, 0.2, 8, 2.7, 0.4, 0.5, 0, 0)),
            new("Crisco", "1 lb.", 20.3, new(20.1, 0, 0, 0, 0, 0, 0, 0, 0)),
            new("Lard", "1 lb.", 9.8, new(41.7, 0, 0, 0, 0.2, 0, 0.5, 5, 0)),
            new("Sirloin Steak", "1 lb.", 39.6, new(2.9, 166, 0.1, 34, 0.2, 2.1, 2.9, 69, 0)),
            new("Round Steak", "1 lb.", 36.4, new(2.2, 214, 0.1, 32, 0.4, 2.5, 2.4, 87, 0)),
            new("Rib Roast", "1 lb.", 29.2, new(3.4, 213, 0.1, 33, 0, 0, 2, 0, 0)),
            new("Chuck Roast", "1 lb.", 22.6, new(3.6, 309, 0.2, 46, 0.4, 1, 4, 120, 0)),
            new("Plate", "1 lb.", 14.6, new(8.5, 404, 0.2, 62, 0, 0.9, 0, 0, 0)),
            new("Liver (Beef)", "1 lb.", 26.8, new(2.2, 333, 0.2, 139, 169.2, 6.4, 50.8, 316, 525)),
            new("Leg of Lamb", "1 lb.", 27.6, new(3.1, 245, 0.1, 20, 0, 2.8, 3.9, 86, 0)),
            new("Lamb Chops (Rib)", "1 lb.", 36.6, new(3.3, 140, 0.1, 15, 0, 1.7, 2.7, 54, 0)),
            new("Pork Chops", "1 lb.", 30.7, new(3.5, 196, 0.2, 30, 0, 17.4, 2.7, 60, 0)),
            new("Pork Loin Roast", "1 lb.", 24.2, new(4.4, 249, 0.3, 37, 0, 18.2, 3.6, 79, 0)),
            new("Bacon", "1 lb.", 25.6, new(10.4, 152, 0.2, 23, 0, 1.8, 1.8, 71, 0)),
            new("Ham, smoked", "1 lb.", 27.4, new(6.7, 212, 0.2, 31, 0, 9.9, 3.3, 50, 0)),
            new("Salt Pork", "1 lb.", 16, new(18.8, 164, 0.1, 26, 0, 1.4, 1.8, 0, 0)),
            new("Roasting Chicken", "1 lb.", 30.3, new(1.8, 184, 0.1, 30, 0.1, 0.9, 1.8, 68, 46)),
            new("Veal Cutlets", "1 lb.", 42.3, new(1.7, 156, 0.1, 24, 0, 1.4, 2.4, 57, 0)),
            new("Salmon, Pink (can)", "16 oz.", 13, new(5.8, 705, 6.8, 45, 3.5, 1, 4.9, 209, 0)),
            new("Apples", "1 lb.", 4.4, new(5.8, 27, 0.5, 36, 7.3, 3.6, 2.7, 5, 544)),
            new("Bananas", "1 lb.", 6.1, new(4.9, 60, 0.4, 30, 17.4, 2.5, 3.5, 28, 498)),
            new("Lemons", "1 doz.", 26, new(1.0, 21, 0.5, 14, 0, 0.5, 0, 4, 952)),
            new("Oranges", "1 doz.", 30.9, new(2.2, 40, 1.1, 18, 11.1, 3.6, 1.3, 10, 1998)),
            new("Green Beans", "1 lb.", 7.1, new(2.4, 138, 3.7, 80, 69, 4.3, 5.8, 37, 862)),
            new("Cabbage", "1 lb.", 3.7, new(2.6, 125, 4.0, 36, 7.2, 9, 4.5, 26, 5369)),
            new("Carrots", "1 bunch", 4.7, new(2.7, 73, 2.8, 43, 188.5, 6.1, 4.3, 89, 608)),
            new("Celery", "1 stalk", 7.3, new(0.9, 51, 3.0, 23, 0.9, 1.4, 1.4, 9, 313)),
            new("Lettuce", "1 head", 8.2, new(0.4, 27, 1.1, 22, 112.4, 1.8, 3.4, 11, 449)),
            new("Onions", "1 lb.", 3.6, new(5.8, 166, 3.8, 59, 16.6, 4.7, 5.9, 21, 1184)),
            new("Potatoes", "15 lb.", 34, new(14.3, 336, 1.8, 118, 6.7, 29.4, 7.1, 198, 2522)),
            new("Spinach", "1 lb.", 8.1, new(1.1, 106, 0, 138, 918.4, 5.7, 13.8, 33, 2755)),
            new("Sweet Potatoes", "1 lb.", 5.1, new(9.6, 138, 2.7, 54, 290.7, 8.4, 5.4, 83, 1912)),
            new("Peaches (can)", "No. 2 1/2", 16.8, new(3.7, 20, 0.4, 10, 21.5, 0.5, 1, 31, 196)),
            new("Pears (can)", "No. 2 1/2", 20.4, new(3.0, 8, 0.3, 8, 0.8, 0.8, 0.8, 5, 81)),
            new("Pineapple (can)", "No. 2 1/2", 21.3, new(2.4, 16, 0.4, 8, 2, 2.8, 0.8, 7, 399)),
            new("Asparagus (can)", "No. 2", 27.7, new(0.4, 33, 0.3, 12, 16.3, 1.4, 2.1, 17, 272)),
            new("Green Beans (can)", "No. 2", 10, new(1.0, 54, 2, 65, 53.9, 1.6, 4.3, 32, 431)),
            new("Pork and Beans (can)", "16 oz.", 7.1, new(7.5, 364, 4, 134, 3.5, 8.3, 7.7, 56, 0)),
            new("Corn (can)", "No. 2", 10.4, new(5.2, 136, 0.2, 16, 12, 1.6, 2.7, 42, 218)),
            new("Peas (can)", "No. 2", 13.8, new(2.3, 136, 0.6, 45, 34.9, 4.9, 2.5, 37, 370)),
            new("Tomatoes (can)", "No. 2", 8.6, new(1.3, 63, 0.7, 38, 53.2, 3.4, 2.5, 36, 1253)),
            new("Tomato Soup (can)", "10 1/2 oz.", 7.6, new(1.6, 71, 0.6, 43, 57.9, 3.5, 2.4, 67, 862)),
            new("Peaches, Dried", "1 lb.", 15.7, new(8.5, 87, 1.7, 173, 86.8, 1.2, 4.3, 55, 57)),
            new("Prunes, Dried", "1 lb.", 9, new(12.8, 99, 2.5, 154, 85.7, 3.9, 4.3, 65, 257)),
            new("Raisins, Dried", "15 oz.", 9.4, new(13.5, 104, 2.5, 136, 4.5, 6.3, 1.4, 24, 136)),
            new("Peas, Dried", "1 lb.", 7.9, new(20.0, 1367, 4.2, 345, 2.9, 28.7, 18.4, 162, 0)),
            new("Lima Beans, Dried", "1 lb.", 8.9, new(17.4, 1055, 3.7, 459, 5.1, 26.9, 38.2, 93, 0)),
            new("Navy Beans, Dried", "1 lb.", 5.9, new(26.9, 1691, 11.4, 792, 0, 38.4, 24.6, 217, 0)),
            new("Coffee", "1 lb.", 22.4, new(0, 0, 0, 0, 0, 4, 5.1, 50, 0)),
            new("Tea", "1/4 lb.", 17.4, new(0, 0, 0, 0, 0, 0, 2.3, 42, 0)),
            new("Cocoa", "8 oz.", 8.6, new(8.7, 237, 3, 72, 0, 2, 11.9, 40, 0)),
            new("Chocolate", "8 oz.", 16.2, new(8.0, 77, 1.3, 39, 0, 0.9, 3.4, 14, 0)),
            new("Sugar", "10 lb.", 51.7, new(34.9, 0, 0, 0, 0, 0, 0, 0, 0)),
            new("Corn Syrup", "24 oz.", 13.7, new(14.7, 0, 0.5, 74, 0, 0, 0, 5, 0)),
            new("Molasses", "18 oz.", 13.6, new(9.0, 0, 10.3, 244, 0, 1.9, 7.5, 146, 0)),
            new("Strawberry Preserves", "1 lb.", 20.5, new(6.4, 11, 0.4, 7, 0.2, 0.2, 0.4, 3, 0))
        ];

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
                return;
            }
        }

        Console.WriteLine();

        // Display the amounts (in dollars) to purchase of each food.
        NutritionFacts nutrientsResult = new();
        DisplayAnnualFoods(foods, foodItems, nutrientsResult);

        Console.WriteLine($"\nOptimal annual price: ${365 * objective.Value():N2}");

        Console.WriteLine();

        DisplayNutrients(recommendedDailyAllowance, nutrientsResult);

        Console.WriteLine("\nAdvanced usage:");
        Console.WriteLine($"Problem solved in {solver.WallTime()} milliseconds");
        Console.WriteLine($"Problem solved in {solver.Iterations()} iterations");
    }

    public static void DisplayAnnualFoods(List<Variable> foods, List<FoodItem> nutritionFacts, NutritionFacts nutrientsResult)
    {
        var annualTable = new ConsoleTable("Food", "Annual Cost ($)");
        for (int i = 0; i < foods.Count; ++i)
        {
            if (foods[i].SolutionValue() > 0.0)
            {
                annualTable.AddRow(nutritionFacts[i].Name, (365 * foods[i].SolutionValue()).ToString("N2"));
                for (int j = 0; j < NutritionFacts.Properties.Value.Length; ++j)
                {
                    nutrientsResult[j] += nutritionFacts[i].NutritionFacts[j] * foods[i].SolutionValue();
                }
            }
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
