using UVBake.Core.Solvers;
using Xunit;

namespace UVBake.Tests;

/// <summary>The sparse CG solver, validated in isolation: known SPD solves and factor-reuse.</summary>
public class SolverTests
{
    [Fact]
    public void Cg_SolvesTridiagonalPoissonSystem()
    {
        // 1D Poisson-style SPD matrix (2 on diagonal, -1 off). b = A·xTrue, then recover xTrue.
        const int n = 12;
        SparseMatrix a = Poisson1D(n);
        var xTrue = new double[n];
        for (int i = 0; i < n; i++)
        {
            xTrue[i] = Math.Sin(i + 1) + (0.5 * i); // arbitrary but fixed
        }

        double[] b = MatVec(a, xTrue);
        double[] x = new ConjugateGradientSolver().Factor(a).Solve(b);

        for (int i = 0; i < n; i++)
        {
            Assert.Equal(xTrue[i], x[i], precision: 8);
        }
    }

    [Fact]
    public void Cg_FactorReuse_SolvesMultipleRightHandSides()
    {
        SparseMatrix a = Poisson1D(8);
        IFactorization factorization = new ConjugateGradientSolver().Factor(a);

        var x1 = new double[8];
        var x2 = new double[8];
        for (int i = 0; i < 8; i++)
        {
            x1[i] = i + 1;
            x2[i] = (i % 2 == 0) ? 1.0 : -2.0;
        }

        double[] solved1 = factorization.Solve(MatVec(a, x1));
        double[] solved2 = factorization.Solve(MatVec(a, x2));

        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(x1[i], solved1[i], precision: 8);
            Assert.Equal(x2[i], solved2[i], precision: 8);
        }
    }

    [Fact]
    public void Cg_ZeroRightHandSide_ReturnsZero()
    {
        SparseMatrix a = Poisson1D(5);
        double[] x = new ConjugateGradientSolver().Factor(a).Solve(new double[5]);
        Assert.All(x, value => Assert.Equal(0.0, value));
    }

    private static SparseMatrix Poisson1D(int n)
    {
        var builder = new SparseMatrixBuilder(n);
        for (int i = 0; i < n; i++)
        {
            builder.Add(i, i, 2.0);
            if (i > 0)
            {
                builder.Add(i, i - 1, -1.0);
            }

            if (i < n - 1)
            {
                builder.Add(i, i + 1, -1.0);
            }
        }

        return builder.Build();
    }

    private static double[] MatVec(SparseMatrix a, double[] x)
    {
        var y = new double[x.Length];
        a.Multiply(x, y);
        return y;
    }
}
