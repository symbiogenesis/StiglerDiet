namespace StiglerDiet.Tests;

using StiglerDiet.Solvers;

public class LinearProgrammingSolverTests
{
    [Theory]
    [InlineData(2.0, double.PositiveInfinity, false, 2.0)]
    [InlineData(-5.0, -2.0, false, -5.0)]
    [InlineData(-5.0, -2.0, true, -2.0)]
    [InlineData(double.NegativeInfinity, -2.0, true, -2.0)]
    [InlineData(3.0, 3.0, true, 3.0)]
    public void VariableBoundsDetermineTheOptimum(double lower, double upper, bool maximize, double expected)
    {
        using var solver = new LinearProgrammingSolver();
        var x = solver.MakeNumVar(lower, upper, "x");
        solver.Objective.SetCoefficient(x, 1.0);
        if (maximize)
            solver.Objective.SetMaximization();

        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(expected, x.SolutionValue(), 8);
    }

    [Fact]
    public void MinimizationFindsTheAnalyticalVertex()
    {
        using var solver = new LinearProgrammingSolver();
        var x = solver.MakeNumVar(0, double.PositiveInfinity, "x");
        var y = solver.MakeNumVar(0, double.PositiveInfinity, "y");
        var first = solver.MakeConstraint(4, double.PositiveInfinity, "x + y >= 4");
        first.SetCoefficient(x, 1);
        first.SetCoefficient(y, 1);
        var second = solver.MakeConstraint(5, double.PositiveInfinity, "2x + y >= 5");
        second.SetCoefficient(x, 2);
        second.SetCoefficient(y, 1);
        solver.Objective.SetCoefficient(x, 3);
        solver.Objective.SetCoefficient(y, 2);

        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(1, x.SolutionValue(), 8);
        Assert.Equal(3, y.SolutionValue(), 8);
        Assert.Equal(9, 3 * x.SolutionValue() + 2 * y.SolutionValue(), 8);
    }

    [Fact]
    public void UpperOnlyAndRangedConstraintsHaveNoImplicitLowerBound()
    {
        using var solver = new LinearProgrammingSolver();
        var x = solver.MakeNumVar(double.NegativeInfinity, double.PositiveInfinity, "x");
        var y = solver.MakeNumVar(0, double.PositiveInfinity, "y");
        solver.MakeConstraint(double.NegativeInfinity, -3, "x <= -3").SetCoefficient(x, 1);
        var range = solver.MakeConstraint(2, 4, "2 <= y - x <= 4");
        range.SetCoefficient(x, -1);
        range.SetCoefficient(y, 1);
        solver.Objective.SetCoefficient(x, 1);
        solver.Objective.SetCoefficient(y, 1);
        solver.Objective.SetMaximization();

        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(-3, x.SolutionValue(), 8);
        Assert.Equal(1, y.SolutionValue(), 8);
    }

    [Fact]
    public void EqualityCanFixAFreeVariableToANegativeValue()
    {
        using var solver = new LinearProgrammingSolver();
        var x = solver.MakeNumVar(double.NegativeInfinity, double.PositiveInfinity, "x");
        solver.MakeConstraint(-3, -3, "x = -3").SetCoefficient(x, 1);
        solver.Objective.SetCoefficient(x, 1);

        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(-3, x.SolutionValue(), 8);
    }

    [Fact]
    public void ShiftedAndReflectedVariablesPreserveConstraintOffsets()
    {
        using var solver = new LinearProgrammingSolver();
        var x = solver.MakeNumVar(2, double.PositiveInfinity, "x");
        var y = solver.MakeNumVar(double.NegativeInfinity, -1, "y");
        var equality = solver.MakeConstraint(1, 1, "x + y = 1");
        equality.SetCoefficient(x, 1);
        equality.SetCoefficient(y, 1);
        var lower = solver.MakeConstraint(6, double.PositiveInfinity, "x - y >= 6");
        lower.SetCoefficient(x, 1);
        lower.SetCoefficient(y, -1);
        solver.Objective.SetCoefficient(x, 1);
        solver.Objective.SetCoefficient(y, -1);

        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(3.5, x.SolutionValue(), 8);
        Assert.Equal(-2.5, y.SolutionValue(), 8);
    }

    [Fact]
    public void ContradictoryConstraintsAreInfeasible()
    {
        using var solver = new LinearProgrammingSolver();
        var x = solver.MakeNumVar(0, double.PositiveInfinity, "x");
        solver.MakeConstraint(2, double.PositiveInfinity, "x >= 2").SetCoefficient(x, 1);
        solver.MakeConstraint(double.NegativeInfinity, 1, "x <= 1").SetCoefficient(x, 1);

        Assert.Equal(ResultStatus.INFEASIBLE, solver.Solve());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImprovingUnboundedDirectionsAreReported(bool addLowerConstraint)
    {
        using var solver = new LinearProgrammingSolver();
        var x = solver.MakeNumVar(double.NegativeInfinity, double.PositiveInfinity, "x");
        if (addLowerConstraint)
            solver.MakeConstraint(1, double.PositiveInfinity, "x >= 1").SetCoefficient(x, 1);
        solver.Objective.SetCoefficient(x, 1);
        solver.Objective.SetMaximization();

        Assert.Equal(ResultStatus.UNBOUNDED, solver.Solve());
    }

    [Fact]
    public void AnEmptyModelIsFeasibleButAContradictoryConstantIsNot()
    {
        using var solver = new LinearProgrammingSolver();
        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        solver.MakeConstraint(-1, 1, "satisfied constant");
        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        solver.MakeConstraint(1, 2, "impossible constant");
        Assert.Equal(ResultStatus.INFEASIBLE, solver.Solve());
    }

    [Fact]
    public void RowScalingPreservesSmallCoefficients()
    {
        using var solver = new LinearProgrammingSolver();
        var x = solver.MakeNumVar(0, double.PositiveInfinity, "x");
        solver.MakeConstraint(2e-12, double.PositiveInfinity, "tiny coefficients").SetCoefficient(x, 1e-12);
        solver.Objective.SetCoefficient(x, 1e-12);

        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(2, x.SolutionValue(), 8);
    }

    [Fact]
    public void SmallPositiveCoefficientsCannotMakeABoundedProblemLookUnbounded()
    {
        using var solver = new LinearProgrammingSolver();
        var x = solver.MakeNumVar(0, double.PositiveInfinity, "x");
        var y = solver.MakeNumVar(0, double.PositiveInfinity, "y");
        var upper = solver.MakeConstraint(double.NegativeInfinity, 1, "1e-12 x + y <= 1");
        upper.SetCoefficient(x, 1e-12);
        upper.SetCoefficient(y, 1);
        solver.Objective.SetCoefficient(x, 1);
        solver.Objective.SetMaximization();

        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(1e12, x.SolutionValue(), 1);
        Assert.Equal(0, y.SolutionValue(), 8);
    }

    [Fact]
    public void SmallPositivePhaseOneCostsCannotBeMistakenForInfeasibility()
    {
        using var solver = new LinearProgrammingSolver();
        var x = solver.MakeNumVar(0, double.PositiveInfinity, "x");
        var y = solver.MakeNumVar(0, 0, "y");
        var lower = solver.MakeConstraint(1, double.PositiveInfinity, "1e-12 x + y >= 1");
        lower.SetCoefficient(x, 1e-12);
        lower.SetCoefficient(y, 1);
        solver.Objective.SetCoefficient(x, 1);

        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(1e12, x.SolutionValue(), 1);
    }

    [Fact]
    public void SmallPositiveReducedCostsCannotBeMistakenForAnOptimum()
    {
        using var solver = new LinearProgrammingSolver();
        var x = solver.MakeNumVar(0, 0, "x");
        var y = solver.MakeNumVar(0, 1e12, "y");
        solver.Objective.SetCoefficient(x, 1);
        solver.Objective.SetCoefficient(y, 1e-12);
        solver.Objective.SetMaximization();

        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(1e12, y.SolutionValue(), 1);
    }

    [Fact]
    public void DegenerateCyclingExampleTerminatesAtTheOptimum()
    {
        using var solver = new LinearProgrammingSolver();
        var variables = Enumerable.Range(0, 4).Select(i => solver.MakeNumVar(0, double.PositiveInfinity, $"x{i}")).ToArray();
        double[][] coefficients = [[0.5, -5.5, -2.5, 9], [0.5, -1.5, -0.5, 1], [1, 0, 0, 0]];
        for (int i = 0; i < coefficients.Length; i++)
        {
            var row = solver.MakeConstraint(double.NegativeInfinity, i == 2 ? 1 : 0, $"row{i}");
            for (int j = 0; j < variables.Length; j++)
                row.SetCoefficient(variables[j], coefficients[i][j]);
        }
        double[] costs = [10, -57, -9, -24];
        for (int i = 0; i < variables.Length; i++)
            solver.Objective.SetCoefficient(variables[i], costs[i]);
        solver.Objective.SetMaximization();

        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(1, variables.Select((variable, i) => variable.SolutionValue() * costs[i]).Sum(), 8);
        Assert.InRange(solver.Iterations(), 1, 30);
    }

    [Fact]
    public void DuplicateEqualitiesDoNotPreventOptimization()
    {
        using var solver = new LinearProgrammingSolver();
        var x = solver.MakeNumVar(0, double.PositiveInfinity, "x");
        var y = solver.MakeNumVar(0, double.PositiveInfinity, "y");
        for (int i = 0; i < 2; i++)
        {
            var equality = solver.MakeConstraint(2, 2, $"equality{i}");
            equality.SetCoefficient(x, 1);
            equality.SetCoefficient(y, 1);
        }
        solver.Objective.SetCoefficient(x, 1);
        solver.Objective.SetCoefficient(y, 2);

        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(2, x.SolutionValue(), 8);
        Assert.Equal(0, y.SolutionValue(), 8);
    }

    [Fact]
    public void IterationLimitCannotClaimAnOptimum()
    {
        using var solver = new LinearProgrammingSolver(maxIterations: 0);
        var x = solver.MakeNumVar(0, 5, "x");
        solver.Objective.SetCoefficient(x, 1);
        solver.Objective.SetMaximization();

        Assert.Equal(ResultStatus.ABNORMAL, solver.Solve());
        Assert.Equal(0, solver.Iterations());
        Assert.True(double.IsNaN(x.SolutionValue()));
    }

    [Fact]
    public void FailedSolveClearsThePreviousSolutionAndIterationCount()
    {
        using var solver = new LinearProgrammingSolver();
        var x = solver.MakeNumVar(0, 5, "x");
        solver.Objective.SetCoefficient(x, 1);
        solver.Objective.SetMaximization();
        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(5, x.SolutionValue(), 8);
        Assert.True(solver.Iterations() > 0);

        solver.MakeConstraint(1, 2, "impossible constant");

        Assert.Equal(ResultStatus.INFEASIBLE, solver.Solve());
        Assert.True(double.IsNaN(x.SolutionValue()));
        Assert.Equal(0, solver.Iterations());
    }

    [Fact]
    public void UnboundedSolveDoesNotPublishAnEarlierOptimum()
    {
        using var solver = new LinearProgrammingSolver();
        var x = solver.MakeNumVar(0, double.PositiveInfinity, "x");
        solver.Objective.SetCoefficient(x, 1);
        Assert.Equal(ResultStatus.OPTIMAL, solver.Solve());
        Assert.Equal(0, x.SolutionValue(), 8);

        solver.Objective.SetMaximization();

        Assert.Equal(ResultStatus.UNBOUNDED, solver.Solve());
        Assert.True(double.IsNaN(x.SolutionValue()));
    }

    [Fact]
    public void NonFiniteCoefficientsAreRejected()
    {
        using var solver = new LinearProgrammingSolver();
        var x = solver.MakeNumVar(0, 1, "x");
        solver.Objective.SetCoefficient(x, double.NaN);

        Assert.Equal(ResultStatus.ABNORMAL, solver.Solve());
    }
}
