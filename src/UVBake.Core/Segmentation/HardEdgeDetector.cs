using UVBake.Core.Meshes;

namespace UVBake.Core.Segmentation;

/// <summary>
/// Flags the edges that must become UV seams before any distortion-driven chart growth. A downstream
/// tangent-space bake requires a seam on every hard edge, so these are forced first (Documents/03 §2).
/// Two sources, combined per <see cref="SeamSource"/>:
/// <list type="bullet">
/// <item><b>Geometry</b> — an interior edge whose dihedral angle exceeds the threshold (default 45°).</item>
/// <item><b>Tags</b> — edges the source mesh marked as creases / sharp (e.g. OBJ smoothing-group boundaries).</item>
/// </list>
/// </summary>
public static class HardEdgeDetector
{
    /// <summary>
    /// Marks hard edges on <paramref name="mesh"/> using the source mesh's crease tags and the options'
    /// angle threshold and <see cref="SeamSource"/>.
    /// </summary>
    public static void Detect(HalfEdgeMesh mesh, Mesh source, UVBakeOptions options)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        Detect(mesh, source.Creases, options.HardEdgeAngleDegrees, options.Seams);
    }

    /// <summary>
    /// Marks hard edges on <paramref name="mesh"/> from an explicit crease set, angle threshold (degrees),
    /// and seam source. Edges already marked stay marked (the two sources union).
    /// </summary>
    public static void Detect(
        HalfEdgeMesh mesh,
        IReadOnlySet<EdgeKey> creases,
        double angleThresholdDegrees,
        SeamSource source)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(creases);

        bool useDihedral = source is SeamSource.DihedralAngle or SeamSource.Both;
        bool useTags = source is SeamSource.ProvidedTags or SeamSource.Both;
        double thresholdRadians = angleThresholdDegrees * Math.PI / 180.0;

        foreach (int h in mesh.InteriorEdges())
        {
            bool hard =
                (useDihedral && mesh.Dihedral(h) > thresholdRadians) ||
                (useTags && creases.Count > 0 && creases.Contains(mesh.EdgeKeyOf(h)));

            if (hard)
            {
                mesh.MarkHard(h);
            }
        }
    }
}
