namespace UVBake.Core.Meshes;

/// <summary>
/// An undirected mesh edge identified by its two endpoint <em>position</em> indices, normalized so the
/// smaller index is always <see cref="A"/>. Two edges with the same endpoints compare equal regardless
/// of orientation, which is what crease/seam bookkeeping needs.
/// </summary>
public readonly struct EdgeKey : IEquatable<EdgeKey>
{
    /// <summary>The smaller endpoint index.</summary>
    public int A { get; }

    /// <summary>The larger endpoint index.</summary>
    public int B { get; }

    /// <summary>Builds a normalized edge key; endpoint order does not matter.</summary>
    public EdgeKey(int v0, int v1)
    {
        if (v0 <= v1)
        {
            A = v0;
            B = v1;
        }
        else
        {
            A = v1;
            B = v0;
        }
    }

    public bool Equals(EdgeKey other) => A == other.A && B == other.B;

    public override bool Equals(object? obj) => obj is EdgeKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(A, B);

    public static bool operator ==(EdgeKey left, EdgeKey right) => left.Equals(right);

    public static bool operator !=(EdgeKey left, EdgeKey right) => !left.Equals(right);

    public override string ToString() => $"({A}, {B})";
}
