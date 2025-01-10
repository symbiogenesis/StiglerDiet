# Stigler Diet

## Overview

This is a C# implementation of the Stigler diet problem, which is a famous optimization problem in linear programming. The goal is to find the most cost-effective diet that satisfies all specified nutritional requirements, given a list of foods and their nutritional content.

This is a more fleshed out version of [Google's implementation](https://developers.google.com/optimization/lp/stigler_diet)

The following improvements have been made:

- Add unit tests
- Use strongly-typed objects
- Refactored to be idempotent and segregate console logging from computation
- Enable easily swapping in your own data set
- Standardized the 1939 data from cents to dollars
- Corrected the 1939 data's erroneous use of kcals by multiplying everything by a thousand, since calories and kcals are really the same.
- Correctly report the quantities and units
- Compute % of RDA
- Show daily totals
- Pretty printed the results as tables

## Sample Output

```console
C:\Users\user\source\repos\StiglerDiet\StiglerDiet [main ≡]> dotnet run 
Number of variables = 77
Number of constraints = 9

 -------------------------------------------------------- 
 | Food                   | Daily Quantity | Daily Cost |
 -------------------------------------------------------- 
 | Wheat Flour (Enriched) | 0.82 (lb.)     | $0.03      |
 -------------------------------------------------------- 
 | Liver (Beef)           | 0.01 (lb.)     | $0.00      |
 -------------------------------------------------------- 
 | Cabbage                | 0.30 (lb.)     | $0.01      |
 -------------------------------------------------------- 
 | Spinach                | 0.06 (lb.)     | $0.01      |
 --------------------------------------------------------
 | Navy Beans, Dried      | 1.03 (lb.)     | $0.06      |
 --------------------------------------------------------
 | ---                    | ---            | ---        |
 --------------------------------------------------------
 | Total                  |                | $0.11      |
 --------------------------------------------------------

 ----------------------------------------------------------
 | Food                   | Annual Quantity | Annual Cost |
 ----------------------------------------------------------
 | Wheat Flour (Enriched) | 299.29 (lb.)    | $10.77      |
 ----------------------------------------------------------
 | Liver (Beef)           | 2.58 (lb.)      | $0.69       |
 ----------------------------------------------------------
 | Cabbage                | 110.63 (lb.)    | $4.09       |
 ----------------------------------------------------------
 | Spinach                | 22.57 (lb.)     | $1.83       |
 ----------------------------------------------------------
 | Navy Beans, Dried      | 377.55 (lb.)    | $22.28      |
 ----------------------------------------------------------
 | ---                    | ---             | ---         |
 ----------------------------------------------------------
 | Total                  |                 | $39.66      |
 ----------------------------------------------------------

 -----------------------------------------
 | Nutrient        | Amount   | % of RDA |
 -----------------------------------------
 | Calories        | 3,000.00 | 100.00%  |
 -----------------------------------------
 | Protein (g)     | 147.41   | 210.59%  |
 -----------------------------------------
 | Calcium (g)     | 0.80     | 100.00%  |
 -----------------------------------------
 | Iron (mg)       | 60.47    | 503.89%  |
 -----------------------------------------
 | Vitamin A (IU)  | 5.00     | 100.00%  |
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
Problem solved in 43 milliseconds
Problem solved in 14 iterations
```

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
