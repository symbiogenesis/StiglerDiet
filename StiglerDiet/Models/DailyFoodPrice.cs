namespace StiglerDiet.Models;

public record struct DailyFoodPrice(FoodItem Food, double DailyPrice)
{
    public static implicit operator (FoodItem Food, double DailyPrice)(DailyFoodPrice value)
    {
        return (value.Food, value.DailyPrice);
    }

    public static implicit operator DailyFoodPrice((FoodItem Food, double DailyPrice) value)
    {
        return new DailyFoodPrice(value.Food, value.DailyPrice);
    }
}