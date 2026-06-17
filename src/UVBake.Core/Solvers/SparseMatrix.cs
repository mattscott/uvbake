namespace UVBake.Core.Solvers;

/// <summary>
/// A square sparse matrix in compressed-sparse-row (CSR) form. Built via <see cref="SparseMatrixBuilder"/>.
/// The parameterization systems (Tutte Laplacian, LSCM normal equations, SLIM proxy) are all symmetric
/// positive-definite and solved through the <see cref="ISparseSolver"/> seam.
/// </summary>
public sealed class SparseMatrix
{
    private readonly int[] _rowPtr;
    private readonly int[] _colIndices;
    private readonly double[] _values;

    internal SparseMatrix(int size, int[] rowPtr, int[] colIndices, double[] values)
    {
        Size = size;
        _rowPtr = rowPtr;
        _colIndices = colIndices;
        _values = values;
    }

    /// <summary>Matrix dimension (it is <c>Size × Size</c>).</summary>
    public int Size { get; }

    /// <summary>Computes <c>result = A · x</c>. Row order is fixed, so the result is deterministic.</summary>
    public void Multiply(double[] x, double[] result)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(result);
        for (int row = 0; row < Size; row++)
        {
            double sum = 0;
            int end = _rowPtr[row + 1];
            for (int k = _rowPtr[row]; k < end; k++)
            {
                sum += _values[k] * x[_colIndices[k]];
            }

            result[row] = sum;
        }
    }

    /// <summary>The diagonal entry of <paramref name="row"/> (0 if absent), used for Jacobi preconditioning.</summary>
    public double Diagonal(int row)
    {
        int end = _rowPtr[row + 1];
        for (int k = _rowPtr[row]; k < end; k++)
        {
            if (_colIndices[k] == row)
            {
                return _values[k];
            }
        }

        return 0;
    }
}
