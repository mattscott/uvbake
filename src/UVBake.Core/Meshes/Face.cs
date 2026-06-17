namespace UVBake.Core.Meshes;

/// <summary>
/// A single polygon face as an ordered list of <see cref="Corner"/>s (3 = triangle, 4 = quad, more =
/// n-gon). Winding is preserved from the source; the corner order defines the face's orientation.
/// </summary>
public readonly struct Face
{
    private readonly Corner[] _corners;

    /// <summary>Wraps an ordered corner array (the face takes ownership; it is not copied).</summary>
    public Face(Corner[] corners) =>
        _corners = corners ?? throw new ArgumentNullException(nameof(corners));

    /// <summary>Number of corners (vertices) in the face.</summary>
    public int Count => _corners.Length;

    /// <summary>The corner at <paramref name="index"/> in winding order.</summary>
    public Corner this[int index] => _corners[index];

    /// <summary>The face's corners in winding order.</summary>
    public ReadOnlySpan<Corner> Corners => _corners;

    /// <summary>True when the face is a triangle.</summary>
    public bool IsTriangle => _corners.Length == 3;
}
