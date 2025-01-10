namespace StiglerDiet.Models;

public record struct FoodItem(string Name, double Quantity, string Unit, double Price, NutritionFacts NutritionFacts);
