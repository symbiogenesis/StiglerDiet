namespace StiglerDiet.Models;

using StiglerDiet.Solvers;

public class OptimalDailyDiet(ResultStatus resultStatus) : List<OptimalDailyDietItem>
{   
    public NutritionFacts NutritionFacts { get; set; } = new();

    public ResultStatus ResultStatus { get; set; } = resultStatus;
}