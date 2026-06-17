using UVBake.Core.Meshes;

namespace UVBake.Core.Segmentation;

/// <summary>
/// The output of segmentation: a partition of the mesh's triangles into disk-topology <see cref="Chart"/>s,
/// plus the seam set. A seam is any edge where the two sides belong to different charts (or a mesh
/// boundary). Because growth never crosses a hard edge, every hard edge separates two charts and is
/// therefore a seam — the "hard edges must be seams" requirement holds by construction.
/// </summary>
public sealed class SegmentationResult
{
    private readonly HalfEdgeMesh _mesh;

    public SegmentationResult(HalfEdgeMesh mesh, int[] faceChart, IReadOnlyList<Chart> charts)
    {
        _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        FaceChart = faceChart ?? throw new ArgumentNullException(nameof(faceChart));
        Charts = charts ?? throw new ArgumentNullException(nameof(charts));
    }

    /// <summary>Chart id of each triangle (indexed by triangle).</summary>
    public int[] FaceChart { get; }

    /// <summary>The charts, numbered by lowest member face index.</summary>
    public IReadOnlyList<Chart> Charts { get; }

    /// <summary>True if half-edge <paramref name="h"/> lies on a seam (a chart boundary or a mesh boundary).</summary>
    public bool IsSeam(int h)
    {
        if (_mesh.IsBoundary(h))
        {
            return true;
        }

        return FaceChart[_mesh.TriangleOf(h)] != FaceChart[_mesh.TriangleOf(_mesh.Twin(h))];
    }

    /// <summary>The undirected interior edges that are seams (each yielded once).</summary>
    public IEnumerable<EdgeKey> SeamEdges()
    {
        foreach (int h in _mesh.InteriorEdges())
        {
            if (FaceChart[_mesh.TriangleOf(h)] != FaceChart[_mesh.TriangleOf(_mesh.Twin(h))])
            {
                yield return _mesh.EdgeKeyOf(h);
            }
        }
    }
}
