namespace StiglerDiet.Solvers;

using MathNet.Numerics.LinearAlgebra;
using StiglerDiet.Solvers.Interfaces;

/// <summary>
/// Glop-style barrier method solver
/// </summary>
public class LinearProgrammingSolver : ISolver
{
    private double _wallTime;
    private int _iterations;

    private const double Tolerance = 1e-7;
    private const double LowerBoundThreshold = 1e-9;
    private const double MinStepSize = 1e-12;

    public List<Variable> Variables { get; } = [];
    public List<Constraint> Constraints { get; } = [];
    public Objective Objective { get; } = new();

    public Variable MakeNumVar(double lb, double ub, string name)
    {
        var v = new Variable(name, lb, ub);
        Variables.Add(v);
        return v;
    }

    public Constraint MakeConstraint(double lb, double ub, string name)
    {
        var c = new Constraint(name, lb, ub);
        Constraints.Add(c);
        return c;
    }

    public ResultStatus Solve()
    {
        var startTime = DateTime.UtcNow;
        if (Constraints.Count == 0 || Variables.Count == 0)
        {
            _wallTime = (DateTime.UtcNow - startTime).TotalSeconds;
            return ResultStatus.INFEASIBLE;
        }

        int n = Variables.Count;

        // Map each variable to its index.
        var varIndices = new Dictionary<Variable, int>(Variables.Count);
        for (int i = 0; i < Variables.Count; i++)
            varIndices[Variables[i]] = i;

        var rows = new List<double[]>();
        var rhs = new List<double>();

        // Helper to add a row and its right-hand side value.
        void AddRow(double[] row, double bound)
        {
            rows.Add(row);
            rhs.Add(bound);
        }

            // User-defined constraints.
            foreach (var constr in Constraints)
            {
                var rowLower = new double[n];
                foreach (var (varKey, coef) in constr.Coefficients)
                    rowLower[varIndices[varKey]] = coef;
                AddRow(rowLower, double.IsNegativeInfinity(constr.LowerBound) ? 0.0 : constr.LowerBound);

                // Constraint upper bounds.
                if (!double.IsPositiveInfinity(constr.UpperBound))
                {
                    var rowUpper = new double[n];
                    foreach (var (v, coef) in constr.Coefficients)
                        rowUpper[varIndices[v]] = -coef;
                    AddRow(rowUpper, -constr.UpperBound);
                }
            }

            // Variable upper bounds.
            foreach (var v in Variables)
            {
                if (!double.IsPositiveInfinity(v.UpperBound))
                {
                    var row = new double[n];
                    row[varIndices[v]] = -1.0;
                    AddRow(row, -v.UpperBound);
                }
            }

        var Adata = Matrix<double>.Build.DenseOfRowArrays(rows);
        var bvals = Vector<double>.Build.DenseOfEnumerable(rhs);
        int m = Adata.RowCount;

        // Build objective vector.
        double sign = Objective.IsMinimization ? 1.0 : -1.0;
        var cVec = Vector<double>.Build.Dense(n, i => 0.0);
        foreach (var (variable, coeff) in Objective.Coefficients)
            cVec[varIndices[variable]] = sign * coeff;

        // Define barrier method parameters.
        double t = 1.0, mu = 2.0;
        const int maxIter = 1000, innerIter = 50;
        var x = Vector<double>.Build.Dense(n, 1.0);
        _iterations = 0;

        // Reuse a single vector to hold intermediate calculations
        var res = Vector<double>.Build.Dense(m);

        double BarrierValue(Vector<double> xVal)
        {
            Adata.Multiply(xVal, res);
            res.Subtract(bvals, res);
            for (int i = 0; i < m; i++)
                if (res[i] <= 0.0) return double.PositiveInfinity;

            double sumLog = 0.0;
            for (int i = 0; i < m; i++)
                sumLog += Math.Log(res[i]);

            return t * cVec.DotProduct(xVal) - sumLog;
        }

        Vector<double>? ComputeGradient(Vector<double> xVal)
        {
            Adata.Multiply(xVal, res);
            res.Subtract(bvals, res);
            for (int i = 0; i < m; i++)
                if (res[i] <= 0.0) return null;

            var grad = cVec.Clone();
            grad.Multiply(t, grad);
            for (int i = 0; i < m; i++)
            {
                double inv = -1.0 / res[i];
                for (int j = 0; j < n; j++)
                    grad[j] += Adata[i, j] * inv;
            }
            return grad;
        }

        // Main barrier method loop.
        while (_iterations < maxIter && m / t >= Tolerance)
        {
            for (int i = 0; i < innerIter; i++)
            {
                _iterations++;
                var grad = ComputeGradient(x);
                if (grad == null || grad.L2Norm() < Tolerance)
                    break;

                double step = 1.0;
                double currVal = BarrierValue(x);
                while (step >= MinStepSize)
                {
                    var candidate = (x - step * grad)
                        .PointwiseMaximum(Vector<double>.Build.Dense(n, LowerBoundThreshold));
                    if (BarrierValue(candidate) <= currVal + 0.25 * grad.DotProduct(candidate - x))
                    {
                        x = candidate;
                        break;
                    }
                    step *= 0.5;
                }
            }
            t *= mu;
        }

        // Assign solution to each variable.
        for (int j = 0; j < n; j++)
            Variables[j].Solution = Math.Abs(x[j]) < Tolerance ? 0.0 : x[j];

        _wallTime = (DateTime.UtcNow - startTime).TotalSeconds;

        return ResultStatus.OPTIMAL;
    }

    public double WallTime() => _wallTime;
    public long Iterations() => _iterations;
    public int NumVariables() => Variables.Count;
    public int NumConstraints() => Constraints.Count;

    public void Dispose()
    {
        // Do nothing.
    }
}
