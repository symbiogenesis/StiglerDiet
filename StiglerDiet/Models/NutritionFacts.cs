using System;
using System.Linq;
using System.Reflection;

namespace StiglerDiet.Models;

public class NutritionFacts
{
    public static Lazy<PropertyInfo[]> Properties { get; } = new(() => typeof(NutritionFacts).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));

    public NutritionFacts()
    {
    }

    public NutritionFacts(double calories, double protein, double calcium, double iron, double vitaminA, double vitaminB1, double vitaminB2, double niacin, double vitaminC)
        : this()
    {
        Calories = calories;
        Protein = protein;
        Calcium = calcium;
        Iron = iron;
        VitaminA = vitaminA;
        VitaminB1 = vitaminB1;
        VitaminB2 = vitaminB2;
        Niacin = niacin;
        VitaminC = vitaminC;
    }

    public double Calories { get; init; }
    public double Protein { get; init; }
    public double Calcium { get; init; }
    public double Iron { get; init; }
    public double VitaminA { get; init; }
    public double VitaminB1 { get; init; }
    public double VitaminB2 { get; init; }
    public double Niacin { get; init; }
    public double VitaminC { get; init; }

    internal double this[int index]
    {
        get
        {
            if (index < 0 || index >= Properties.Value.Length)
            {
                throw new IndexOutOfRangeException("Invalid index");
            }

            var property = Properties.Value[index];

            var value = property.GetValue(this) ?? throw new ArgumentException($"Property value is null for index: {index}");

            return (double)value;
        }
        set
        {
            if (index < 0 || index >= Properties.Value.Length)
            {
                throw new IndexOutOfRangeException("Invalid index");
            }

            var property = Properties.Value[index];

            if (property.PropertyType != typeof(double))
            {
                throw new ArgumentException($"Property type is not double for index: {index}");
            }

            property.SetValue(this, value);
        }
    }
}