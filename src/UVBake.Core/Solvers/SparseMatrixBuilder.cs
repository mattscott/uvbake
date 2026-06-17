namespace UVBake.Core.Solvers;

/// <summary>
/// Accumulates triplet entries (summing duplicates) and compresses them into a <see cref="SparseMatrix"/>.
/// Column indices within each row are sorted on build, so the matrix layout — and therefore every
/// downstream solve — is deterministic.
/// </summary>
public sealed class SparseMatrixBuilder
{
    private readonly int _size;
    private readonly Dictionary<long, double> _entries = [];

    public SparseMatrixBuilder(int size)
    {
        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        _size = size;
    }

    /// <summary>Adds <paramref name="value"/> to entry (<paramref name="row"/>, <paramref name="col"/>).</summary>
    public void Add(int row, int col, double value)
    {
        if ((uint)row >= (uint)_size || (uint)col >= (uint)_size)
        {
            throw new ArgumentOutOfRangeException(nameof(row), "Index outside matrix bounds.");
        }

        if (value == 0)
        {
            return;
        }

        long key = ((long)row * _size) + col;
        _entries[key] = _entries.TryGetValue(key, out double existing) ? existing + value : value;
    }

    /// <summary>Compresses the accumulated entries into an immutable CSR <see cref="SparseMatrix"/>.</summary>
    public SparseMatrix Build()
    {
        // Bucket entries by row, then sort each row's columns for a canonical layout.
        var perRow = new List<(int Col, double Value)>[_size];
        for (int i = 0; i < _size; i++)
        {
            perRow[i] = [];
        }

        foreach ((long key, double value) in _entries)
        {
            int row = (int)(key / _size);
            int col = (int)(key % _size);
            perRow[row].Add((col, value));
        }

        var rowPtr = new int[_size + 1];
        int nnz = _entries.Count;
        var colIndices = new int[nnz];
        var values = new double[nnz];

        int cursor = 0;
        for (int row = 0; row < _size; row++)
        {
            List<(int Col, double Value)> entries = perRow[row];
            entries.Sort(static (a, b) => a.Col.CompareTo(b.Col));
            rowPtr[row] = cursor;
            foreach ((int col, double value) in entries)
            {
                colIndices[cursor] = col;
                values[cursor] = value;
                cursor++;
            }
        }

        rowPtr[_size] = cursor;
        return new SparseMatrix(_size, rowPtr, colIndices, values);
    }
}
