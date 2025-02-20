namespace StiglerDiet.Solvers;

using System;

// A revised Glop-style barrier method solver.
public static class GlopStyleSolver
{
    public static double[] Solve(
        double[] c,
        double[,] A,
        double[] b,
        out int iterations,
        bool isMinimize = true,
        int maxIter = 1000
    )
    {
        int m = A.GetLength(0);
        int n = A.GetLength(1);
        iterations = 0;

        // If maximizing, flip c (we minimize).
        if (!isMinimize)
        {
            for (int j = 0; j < n; j++)
                c[j] = -c[j];
        }

        // Initialize x with 1's.
        var x = new double[n];
        for (int j = 0; j < n; j++) 
            x[j] = 1.0;

        // Phase 1: Find feasible starting point.
        bool allFeasible = false;
        int feasibilityIter = 0;
        int maxFeasibilityIter = 200;
        while (!allFeasible && feasibilityIter < maxFeasibilityIter)
        {
            allFeasible = true;
            double totalDiff = 0.0;
            int violationCount = 0;
            for (int i = 0; i < m; i++)
            {
                double Ax = 0.0;
                for (int j = 0; j < n; j++)
                    Ax += A[i, j] * x[j];
                if (Ax <= b[i] + 1e-8)
                {
                    double diff = b[i] - Ax + 1.0;
                    totalDiff += diff;
                    violationCount++;
                    allFeasible = false;
                }
            }
            if (!allFeasible && violationCount > 0)
            {
                double correction = totalDiff / (n * violationCount);
                for (int j = 0; j < n; j++)
                    x[j] += correction;
            }
            feasibilityIter++;
        }
        if (!allFeasible)
        {
            throw new Exception("Failed to find a feasible starting point.");
        }

        // Barrier method parameters.
        double t = 1.0;         // initial scaling parameter
        double mu = 2.0;        // factor to increase t
        double tol = 1e-7;      // tolerance for convergence
        int innerIter = 50;     // increased number of inner iterations

        // Helper: compute barrier function value.
        double BarrierValue(double[] xVec)
        {
            double sum = 0.0;
            for (int i = 0; i < m; i++)
            {
                double Ax_minus_b = 0.0;
                for (int j = 0; j < n; j++)
                    Ax_minus_b += A[i, j] * xVec[j];
                double diff = Ax_minus_b - b[i];
                if(diff <= 0)
                    return double.PositiveInfinity;
                sum -= Math.Log(diff);
            }
            double obj = 0.0;
            for (int j = 0; j < n; j++)
                obj += c[j] * xVec[j];
            return t * obj + sum;
        }

        // Optimize the barrier function with backtracking line search.
        for (int outer = 0; outer < maxIter / innerIter; outer++)
        {
            for (int inner = 0; inner < innerIter; inner++)
            {
                iterations++;
                var grad = new double[n];
                bool feasible = true;

                // Compute gradient: for each constraint, add -A[i,j] / ((A*x)[i] - b[i]).
                for (int i = 0; i < m; i++)
                {
                    double Ax_minus_b = 0.0;
                    for (int j = 0; j < n; j++)
                        Ax_minus_b += A[i, j] * x[j];
                    double diff = Ax_minus_b - b[i];
                    if (diff <= 0)
                    {
                        feasible = false;
                        break;
                    }
                    for (int j = 0; j < n; j++)
                        grad[j] += -A[i, j] / diff;
                }
                if (!feasible) break;

                // Add contribution from the objective.
                for (int j = 0; j < n; j++)
                    grad[j] += t * c[j];

                // Compute gradient norm.
                double norm = 0;
                for (int j = 0; j < n; j++)
                    norm += grad[j] * grad[j];
                norm = Math.Sqrt(norm);
                if (norm < tol) break;

                // Backtracking line search parameters.
                double stepSize = 1.0;
                double alpha = 0.25;
                double beta = 0.5;
                double currentVal = BarrierValue(x);

                // Copy current x to test a candidate update.
                var xCandidate = new double[n];

                // Get descent direction.
                var descent = new double[n];
                for (int j = 0; j < n; j++)
                    descent[j] = -grad[j];

                // Determine step size that gives sufficient decrease.
                while (true)
                {
                    for (int j = 0; j < n; j++)
                    {
                        xCandidate[j] = x[j] + stepSize * descent[j];
                        // Ensure positivity.
                        if (xCandidate[j] < 1e-9)
                            xCandidate[j] = 1e-9;
                    }
                    double candidateVal = BarrierValue(xCandidate);
                    // Armijo condition.
                    double improvement = 0.0;
                    for (int j = 0; j < n; j++)
                        improvement += grad[j] * (xCandidate[j] - x[j]);
                    if (candidateVal <= currentVal + alpha * stepSize * improvement)
                        break;
                    stepSize *= beta;
                    if (stepSize < 1e-12)
                        break; // prevent too small steps.
                }
                // Update x.
                Array.Copy(xCandidate, x, n);
            }
            t *= mu; // Increase barrier weight.
            if (m / t < tol) break;
        }

        // Fix near-zero entries.
        for (int j = 0; j < n; j++)
        {
            if (Math.Abs(x[j]) < tol)
                x[j] = 0.0;
        }

        return x;
    }
}