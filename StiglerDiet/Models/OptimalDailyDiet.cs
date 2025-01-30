namespace StiglerDiet.Models;

using static Google.OrTools.LinearSolver.Solver;

public class OptimalDailyDiet(ResultStatus resultStatus)
{
    public List<DailyFoodPrice> Foods { get; set; } = [];
    
    public NutritionFacts NutritionFacts { get; set; } = new();

    public ResultStatus ResultStatus { get; set; } = resultStatus;
}