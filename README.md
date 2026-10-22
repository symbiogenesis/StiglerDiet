# Stigler Diet

Requires the .NET 10 SDK to build and run.

## Overview

This is a C# implementation of the Stigler diet problem, which is a famous optimization problem in linear programming. The goal is to find the most cost-effective diet that satisfies all specified nutritional requirements, given a list of foods and their nutritional content.

This is a more fleshed out version of [Google's implementation](https://developers.google.com/optimization/lp/stigler_diet)

Key features:

- Native .NET solver with an optional Google OR-Tools backend
- Includes unit tests
- Uses strongly typed objects
- Keeps computation idempotent and separate from console logging
- Easily replaceable CSV datasets
- Standardizes historical prices from cents to dollars
- Expresses energy in kcal and vitamin A in IU
- Correctly reports food quantities and units
- Computes % of RDA
- Shows daily and annual totals
- Prints results as readable tables

## Data conventions

The bundled CSV files reproduce the historical data in [Google's Stigler example](https://developers.google.com/optimization/lp/stigler_diet). `Price` is the dollar price of `Quantity` in `Unit`; every nutrient coefficient is per dollar spent, not per listed package. The solver's variables therefore represent daily dollars spent. Divide spending by `Price` to obtain packages, then multiply by `Quantity` for the displayed physical amount. Custom datasets must use the same conventions.

Prices are in 1939 dollars, energy is in kcal, and vitamin A is in IU. Historical can sizes such as No. 2 and No. 2 1/2 are preserved as package units. The historical minimum-cost diet is about $39.66 per year.

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
Problem solved in 4.0852 milliseconds
Problem solved in 80 iterations
```

This sample uses the native solver. Timing and iteration counts depend on the backend and machine.

## License

This project is licensed under the Apache 2.0 License. See the [LICENSE](LICENSE) file for details.
