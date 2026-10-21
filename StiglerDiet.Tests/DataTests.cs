namespace StiglerDiet.Tests;

[CollectionDefinition("CSV working directory", DisableParallelization = true)]
public class CsvWorkingDirectoryCollection;

[Collection("CSV working directory")]
public class DataTests
{
    [Theory]
    [InlineData("Peaches (can)", "No. 2 1/2 can")]
    [InlineData("Pears (can)", "No. 2 1/2 can")]
    [InlineData("Pineapple (can)", "No. 2 1/2 can")]
    [InlineData("Asparagus (can)", "No. 2 can")]
    [InlineData("Green Beans (can)", "No. 2 can")]
    [InlineData("Corn (can)", "No. 2 can")]
    [InlineData("Peas (can)", "No. 2 can")]
    [InlineData("Tomatoes (can)", "No. 2 can")]
    public void CanSizeDoesNotMultiplyTheNumberOfCans(string name, string expectedUnit)
    {
        var food = Assert.Single(CsvParser.LoadFoodItems(), food => food.Name == name);

        Assert.Equal(1, food.Quantity);
        Assert.Equal(expectedUnit, food.Unit);
    }

    [Theory]
    [InlineData("Corn Meal", 30900)]
    [InlineData("Liver (Beef)", 169200)]
    [InlineData("Spinach", 918400)]
    public void VitaminAUsesInternationalUnits(string name, double expectedVitaminA)
    {
        var food = Assert.Single(CsvParser.LoadFoodItems(), food => food.Name == name);

        Assert.Equal(expectedVitaminA, food.NutritionFacts.VitaminA);
        Assert.Equal(5000, CsvParser.LoadMinimumDailyAllowance().VitaminA);
    }

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
