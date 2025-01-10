namespace StiglerDiet;
using StiglerDiet.Models;

public record struct FoodResult(FoodItem Food, double DailyPrice)
{
    public static implicit operator (FoodItem Food, double DailyPrice)(FoodResult value)
    {
        return (value.Food, value.DailyPrice);
    }

    public static implicit operator FoodResult((FoodItem Food, double DailyPrice) value)
    {
        return new FoodResult(value.Food, value.DailyPrice);
    }
}