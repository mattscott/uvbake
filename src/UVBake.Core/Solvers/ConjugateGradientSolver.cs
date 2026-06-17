namespace UVBake.Core.Solvers;

/// <summary>
/// Jacobi-preconditioned Conjugate Gradient for symmetric positive-definite systems — a clean-room,
/// zero-dependency solver (Documents/06 §2). Fully sequential with a fixed reduction order, so output is
/// deterministic across runs and platforms. Convergence is capped by a fixed iteration count plus a
/// relative residual tolerance (fixed thresholds, not wall-clock — Documents/06 §5).
/// </summary>
public sealed class ConjugateGradientSolver : ISparseSolver
{
    private readonly int _maxIterations;
    private readonly double _tolerance;

    /// <param name="maxIterations">Iteration cap; <c>0</c> picks <c>10·N + 1000</c> per system.</param>
    /// <param name="tolerance">Relative residual tolerance ‖Ax−b‖ ≤ tol·‖b‖.</param>
    public ConjugateGradientSolver(int maxIterations = 0, double tolerance = 1e-12)
    {
        _maxIterations = maxIterations;
        _tolerance = tolerance;
    }

    public IFactorization Factor(SparseMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        return new Factorization(matrix, _maxIterations, _tolerance);
    }

    private sealed class Factorization : IFactorization
    {
        private readonly SparseMatrix _a;
        private readonly double[] _invDiagonal;
        private readonly int _maxIterations;
        private readonly double _tolerance;

        public Factorization(SparseMatrix a, int maxIterations, double tolerance)
        {
            _a = a;
            _tolerance = tolerance;
            _maxIterations = maxIterations > 0 ? maxIterations : (10 * a.Size) + 1000;

            _invDiagonal = new double[a.Size];
            for (int i = 0; i < a.Size; i++)
            {
                double d = a.Diagonal(i);
                _invDiagonal[i] = d != 0 ? 1.0 / d : 1.0; // identity fallback for an absent diagonal
            }
        }

        public double[] Solve(double[] rhs)
        {
            ArgumentNullException.ThrowIfNull(rhs);
            if (rhs.Length != _a.Size)
            {
                throw new ArgumentException("Right-hand side length does not match the matrix size.", nameof(rhs));
            }

            int n = _a.Size;
            var x = new double[n];
            if (n == 0)
            {
                return x;
            }

            double bNorm = Math.Sqrt(Dot(rhs, rhs));
            if (bNorm == 0)
            {
                return x; // A·0 = 0
            }

            var r = (double[])rhs.Clone();      // r = b - A·0 = b
            var z = new double[n];               // preconditioned residual
            var p = new double[n];
            var ap = new double[n];

            ApplyPreconditioner(r, z);
            Array.Copy(z, p, n);
            double rz = Dot(r, z);

            double threshold = _tolerance * bNorm;
            for (int iter = 0; iter < _maxIterations; iter++)
            {
                _a.Multiply(p, ap);
                double pAp = Dot(p, ap);
                if (pAp <= 0)
                {
                    break; // not positive-definite along p (or exhausted) — stop with the best x so far
                }

                double alpha = rz / pAp;
                for (int i = 0; i < n; i++)
                {
                    x[i] += alpha * p[i];
                    r[i] -= alpha * ap[i];
                }

                if (Math.Sqrt(Dot(r, r)) <= threshold)
                {
                    break;
                }

                ApplyPreconditioner(r, z);
                double rzNext = Dot(r, z);
                double beta = rzNext / rz;
                for (int i = 0; i < n; i++)
                {
                    p[i] = z[i] + (beta * p[i]);
                }

                rz = rzNext;
            }

            return x;
        }

        private void ApplyPreconditioner(double[] r, double[] z)
        {
            for (int i = 0; i < r.Length; i++)
            {
                z[i] = r[i] * _invDiagonal[i];
            }
        }

        private static double Dot(double[] a, double[] b)
        {
            double sum = 0;
            for (int i = 0; i < a.Length; i++)
            {
                sum += a[i] * b[i];
            }

            return sum;
        }
    }
}
