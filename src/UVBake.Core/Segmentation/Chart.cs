using UVBake.Core.Geometry;

namespace UVBake.Core.Segmentation;

/// <summary>
/// A connected, disk-topology patch of triangles flattened as a unit. Carries the data the later stages
/// need: its faces, total 3D surface area (for texel-density normalization in P4), and a representative
/// (area-weighted) normal used as the developability proxy during growth.
/// </summary>
public sealed class Chart
{
    public Chart(int id, IReadOnlyList<int> faces, double surfaceArea3D, Vector3 proxyNormal)
    {
        Id = id;
        Faces = faces;
        SurfaceArea3D = surfaceArea3D;
        ProxyNormal = proxyNormal;
    }

    /// <summary>Stable chart id (charts are numbered by their lowest face index, so it is deterministic).</summary>
    public int Id { get; }

    /// <summary>Triangle indices belonging to this chart.</summary>
    public IReadOnlyList<int> Faces { get; }

    /// <summary>Total 3D area of the chart's triangles.</summary>
    public double SurfaceArea3D { get; }

    /// <summary>Area-weighted unit normal — the proxy plane the chart was grown to stay near.</summary>
    public Vector3 ProxyNormal { get; }
}
