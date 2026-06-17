namespace UVBake.Core.Solvers;

/// <summary>
/// The swappable sparse-solve seam (Documents/06 §2). A solver factors a symmetric positive-definite
/// matrix once; the returned <see cref="IFactorization"/> then solves many right-hand sides against it —
/// the factor-once/solve-many pattern ARAP/SLIM and Tutte's two coordinates rely on. The default
/// implementation is an in-repo, zero-dependency <see cref="ConjugateGradientSolver"/>; a direct
/// solver (e.g. CSparse.NET, dynamic-linked) can drop in behind this interface unchanged.
/// </summary>
public interface ISparseSolver
{
    /// <summary>Prepares to solve against <paramref name="matrix"/> (assumed symmetric positive-definite).</summary>
    IFactorization Factor(SparseMatrix matrix);
}

/// <summary>A prepared solver for one matrix; solve repeatedly with different right-hand sides.</summary>
public interface IFactorization
{
    /// <summary>Solves <c>A · x = rhs</c> and returns <c>x</c>.</summary>
    double[] Solve(double[] rhs);
}
