namespace StiglerDiet;

using System.Globalization;
using CsvHelper;
using StiglerDiet.Models;

public static class CsvParser
{
    public static NutritionFacts LoadMinimumDailyAllowance()
    {
        var filePath = BuildFilePath("MinimumDailyAllowance.csv");
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        return csv.GetRecords<NutritionFacts>().First();
    }

    public static List<FoodItem> LoadFoodItems()
    {
        var filePath = BuildFilePath("FoodItems.csv");
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        return [.. csv.GetRecords<FoodItem>()];
    }

    private static string BuildFilePath(string fileName) => Path.Combine("Data", fileName);
}