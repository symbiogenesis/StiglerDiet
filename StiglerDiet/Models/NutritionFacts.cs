namespace StiglerDiet.Models;

public record struct NutritionFacts(string Name, string Unit, double Price, double[] Nutrients);