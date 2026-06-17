using UVBake.Core.Geometry;

namespace UVBake.Core.Parameterization;

/// <summary>
/// A flattened chart: a 2D UV per local vertex, alongside the chart's 3D positions and local-index
/// triangles, so distortion (per-triangle Jacobian) and injectivity (flips) can be measured and the UVs
/// later written back as per-wedge attributes.
/// </summary>
public sealed class ChartUv
{
    public ChartUv(int[] localToGlobal, Vector3[] position3D, Vector2[] uv, int[] triangles)
    {
        LocalToGlobal = localToGlobal;
        Position3D = position3D;
        Uv = uv;
        Triangles = triangles;
    }

    /// <summary>Local vertex index → global position index.</summary>
    public int[] LocalToGlobal { get; }

    /// <summary>3D position of each local vertex.</summary>
    public Vector3[] Position3D { get; }

    /// <summary>UV coordinate of each local vertex.</summary>
    public Vector2[] Uv { get; }

    /// <summary>Triangles as local-vertex index triples.</summary>
    public int[] Triangles { get; }

    /// <summary>Number of triangles.</summary>
    public int FaceCount => Triangles.Length / 3;

    /// <summary>Number of local vertices.</summary>
    public int VertexCount => Uv.Length;
}
