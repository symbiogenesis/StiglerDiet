namespace StiglerDiet;

using System.Globalization;
using CsvHelper;
using StiglerDiet.Models;

public static class CsvParser
{
    public static NutritionFacts LoadMinimumDailyAllowance()
    {
        const string filePath = "Data/MinimumDailyAllowance.csv";
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        return csv.GetRecords<NutritionFacts>().First();
    }

    public static List<FoodItem> LoadFoodItems()
    {
        const string filePath = "Data/FoodItems.csv";
        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        return [.. csv.GetRecords<FoodItem>()];
    }
}