namespace StiglerDiet.Models;

using System;
using System.ComponentModel;
using System.Reflection;

public class NutritionFacts
{
    public static PropertyInfo[] Properties { get; } = typeof(NutritionFacts).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    [Description("Calories")]
    public double Calories { get; init; }

    [Description("Protein (g)")]
    public double Protein { get; init; }

    [Description("Calcium (g)")]
    public double Calcium { get; init; }

    [Description("Iron (mg)")]
    public double Iron { get; init; }

    [Description("Vitamin A (IU)")]
    public double VitaminA { get; init; }

    [Description("Vitamin B1 (mg)")]
    public double VitaminB1 { get; init; }

    [Description("Vitamin B2 (mg)")]
    public double VitaminB2 { get; init; }

    [Description("Niacin (mg)")]
    public double Niacin { get; init; }

    [Description("Vitamin C (mg)")]
    public double VitaminC { get; init; }

    internal double this[int index]
    {
        get
        {
            var property = GetPropertyInfo(index);

            var value = property.GetValue(this) ?? throw new ArgumentException($"Property value is null for index: {index}");

            return (double)value;
        }
        set
        {
            var property = GetPropertyInfo(index);

            if (property.PropertyType != typeof(double))
            {
                throw new ArgumentException($"Property type is not double for index: {index}");
            }

            property.SetValue(this, value);
        }
    }
    
    private static PropertyInfo GetPropertyInfo(int index)
    {
        if (index < 0 || index >= Properties.Length)
        {
            throw new IndexOutOfRangeException($"Invalid index: {index}");
        }

        return Properties[index];
    }
}
