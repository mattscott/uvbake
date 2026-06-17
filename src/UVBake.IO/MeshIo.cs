using UVBake.Core.Meshes;

namespace UVBake.IO;

/// <summary>
/// Entry point for mesh I/O, dispatching by file extension. OBJ (with per-wedge UVs so seams
/// round-trip) is the format read and written first; glTF arrives in a later phase
/// (see <c>Agents/Plans/02-phase-roadmap.md</c>).
/// </summary>
public static class MeshIo
{
    /// <summary>The mesh format read and written first (Wavefront OBJ; UVs as <c>vt</c>, per-wedge <c>f v/vt/vn</c>).</summary>
    public const string PrimaryMeshExtension = ".obj";

    /// <summary>Reads a mesh, choosing the reader by <paramref name="path"/>'s extension.</summary>
    public static Mesh Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string extension = Path.GetExtension(path);
        return extension.Equals(PrimaryMeshExtension, StringComparison.OrdinalIgnoreCase)
            ? ObjReader.Read(path)
            : throw new NotSupportedException($"Unsupported mesh format '{extension}'. Supported: {PrimaryMeshExtension}.");
    }

    /// <summary>Writes a mesh, choosing the writer by <paramref name="path"/>'s extension.</summary>
    public static void Write(Mesh mesh, string path)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(path);
        string extension = Path.GetExtension(path);
        if (!extension.Equals(PrimaryMeshExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Unsupported mesh format '{extension}'. Supported: {PrimaryMeshExtension}.");
        }

        ObjWriter.Write(mesh, path);
    }
}
