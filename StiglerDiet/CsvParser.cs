namespace StiglerDiet;

using System.Globalization;
using CsvHelper;
using StiglerDiet.Models;

public static class CsvParser
{
    public static NutritionFacts LoadMinimumDailyAllowance() => ReadCsv<NutritionFacts>("MinimumDailyAllowance.csv").First();

    public static List<FoodItem> LoadFoodItems() => ReadCsv<FoodItem>("FoodItems.csv");

    public static List<T> ReadCsv<T>(string fileName)
    {
        var filePath = BuildFilePath(fileName);
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        return [.. csv.GetRecords<T>()];
    }

    private static string BuildFilePath(string fileName) => Path.Combine("Data", fileName);
}