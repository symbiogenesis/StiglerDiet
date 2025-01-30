namespace StiglerDiet.Models;

public record struct OptimalDailyDietItem(FoodItem Food, double Price, double Quantity)
{
    public static implicit operator (FoodItem Food, double Price, double Quantity)(OptimalDailyDietItem value)
    {
        return (value.Food, value.Price, value.Quantity);
    }

    public static implicit operator OptimalDailyDietItem((FoodItem Food, double Price, double Quantity) value)
    {
        return new OptimalDailyDietItem(value.Food, value.Price, value.Quantity);
    }
}