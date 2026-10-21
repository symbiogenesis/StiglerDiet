# Stigler Diet

## Overview

This is a C# implementation of the Stigler diet problem, which is a famous optimization problem in linear programming. The goal is to find the most cost-effective diet that satisfies all specified nutritional requirements, given a list of foods and their nutritional content.

This is a more fleshed out version of [Google's implementation](https://developers.google.com/optimization/lp/stigler_diet)

The following improvements have been made:

- Native .NET implementation of linear optimization algorithm
- Added unit tests
- Uses strongly-typed objects
- Refactored to be idempotent and segregate console logging from computation
- Enabled easily swapping in your own dataset
- Standardized the 1939 data from cents to dollars
- Converted vitamin A from thousands of international units (KIU) to international units (IU), including its daily minimum of 5,000 IU.
- Expresses energy in kcal (dietary Calories). Google's energy values are in thousands of kcal despite their `kcal` label: its minimum of 3 means 3,000 kcal, and wheat flour's 44.7 means 44,700 kcal per dollar. Both energy coefficients and the minimum are multiplied by 1,000 here.
- Correctly reports the quantities and units, preserving No. 2 and No. 2 1/2 as can sizes rather than counts of cans.
- Computes % of RDA
- Shows daily totals
- Pretty prints the results as tables

The bundled CSV files reproduce the historical data in [Google's Stigler example](https://developers.google.com/optimization/lp/stigler_diet). `Price` is the dollar price of `Quantity` in `Unit`; every nutrient coefficient is per dollar spent, not per listed package. The solver's variables therefore represent daily dollars spent. Divide spending by `Price` to obtain packages, then multiply by `Quantity` for the displayed physical amount. Custom datasets must use the same conventions.

## Sample Output

```console
C:\Users\user\source\repos\StiglerDiet\StiglerDiet [main ≡]> dotnet run 
Number of variables = 77
Number of constraints = 9

 --------------------------------------------------------- 
 | Food                   | Daily Quantity | Daily Price |
 --------------------------------------------------------- 
 | Wheat Flour (Enriched) | 0.82 (lb.)     | $0.03       |
 --------------------------------------------------------- 
 | Liver (Beef)           | 0.01 (lb.)     | $0.00       |
 --------------------------------------------------------- 
 | Cabbage                | 0.30 (lb.)     | $0.01       |
 --------------------------------------------------------- 
 | Spinach                | 0.06 (lb.)     | $0.01       |
 ---------------------------------------------------------
 | Navy Beans, Dried      | 1.03 (lb.)     | $0.06       |
 ---------------------------------------------------------
 | ---                    | ---            | ---         |
 ---------------------------------------------------------
 | Total                  |                | $0.11       |
 ---------------------------------------------------------

 -----------------------------------------------------------
 | Food                   | Annual Quantity | Annual Price |
 -----------------------------------------------------------
 | Wheat Flour (Enriched) | 299.29 (lb.)    | $10.77       |
 -----------------------------------------------------------
 | Liver (Beef)           | 2.58 (lb.)      | $0.69        |
 -----------------------------------------------------------
 | Cabbage                | 110.63 (lb.)    | $4.09        |
 -----------------------------------------------------------
 | Spinach                | 22.57 (lb.)     | $1.83        |
 -----------------------------------------------------------
 | Navy Beans, Dried      | 377.55 (lb.)    | $22.28       |
 -----------------------------------------------------------
 | ---                    | ---             | ---          |
 -----------------------------------------------------------
 | Total                  |                 | $39.66       |
 -----------------------------------------------------------

 -----------------------------------------
 | Nutrient        | Amount   | % of RDA |
 -----------------------------------------
 | Energy (kcal)   | 3,000.00 | 100.00%  |
 -----------------------------------------
 | Protein (g)     | 147.41   | 210.59%  |
 -----------------------------------------
 | Calcium (g)     | 0.80     | 100.00%  |
 -----------------------------------------
 | Iron (mg)       | 60.47    | 503.89%  |
 -----------------------------------------
 | Vitamin A (IU)  | 5,000.00 | 100.00%  |
 -----------------------------------------
 | Vitamin B1 (mg) | 4.12     | 228.91%  |
 -----------------------------------------
 | Vitamin B2 (mg) | 2.70     | 100.00%  |
 -----------------------------------------
 | Niacin (mg)     | 27.32    | 151.76%  |
 -----------------------------------------
 | Vitamin C (mg)  | 75.00    | 100.00%  |
 -----------------------------------------


Advanced usage:
Problem solved in 92 milliseconds
Problem solved in 14 iterations
```

## License

This project is licensed under the Apache 2.0 License. See the [LICENSE](LICENSE) file for details.
