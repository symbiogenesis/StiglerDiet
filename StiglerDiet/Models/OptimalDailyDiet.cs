namespace StiglerDiet.Models;

using static Google.OrTools.LinearSolver.Solver;

public class OptimalDailyDiet(ResultStatus resultStatus) : List<OptimalDailyDietItem>
{   
    public NutritionFacts NutritionFacts { get; set; } = new();

    public ResultStatus ResultStatus { get; set; } = resultStatus;
}