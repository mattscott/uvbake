namespace UVBake.IO;

/// <summary>
/// Entry point for mesh I/O. The OBJ reader/writer (with per-wedge UVs so seams round-trip) is
/// implemented in Phase 1+; see <c>Agents/Plans/02-phase-roadmap.md</c>. This placeholder anchors the
/// namespace and records the format the I/O layer starts with.
/// </summary>
public static class MeshIo
{
    /// <summary>The mesh format read and written first (Wavefront OBJ; UVs as <c>vt</c>, per-wedge <c>f v/vt/vn</c>).</summary>
    public const string PrimaryMeshExtension = ".obj";
}
