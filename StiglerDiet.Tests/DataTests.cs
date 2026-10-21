namespace StiglerDiet.Tests;

[CollectionDefinition("CSV working directory", DisableParallelization = true)]
public class CsvWorkingDirectoryCollection;

[Collection("CSV working directory")]
public class DataTests
{
    [Fact]
    public void CannedGreenBeanEnergyMatchesTheSourceTable()
    {
        var greenBeans = Assert.Single(CsvParser.LoadFoodItems(), food => food.Name == "Green Beans (can)");

        // Google's source has 1.0 thousand dietary Calories per dollar.
        Assert.Equal(1000, greenBeans.NutritionFacts.Calories);
    }

    [Fact]
    public void BundledDataLoadsOutsideTheApplicationDirectory()
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        var temporaryDirectory = Directory.CreateTempSubdirectory("StiglerDiet-data-tests-");
        try
        {
            Directory.SetCurrentDirectory(temporaryDirectory.FullName);

            var foods = CsvParser.LoadFoodItems();
            var allowance = CsvParser.LoadMinimumDailyAllowance();

            Assert.Equal(77, foods.Count);
            Assert.Equal("Wheat Flour (Enriched)", foods[0].Name);
            Assert.Equal(3000, allowance.Calories);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            temporaryDirectory.Delete();
        }
    }
}
