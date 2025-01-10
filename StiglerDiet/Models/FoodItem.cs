namespace StiglerDiet.Models;

public record struct FoodItem(string Name, string Unit, double Price, NutritionFacts NutritionFacts);
