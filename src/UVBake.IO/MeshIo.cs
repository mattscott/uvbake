using UVBake.Core.Meshes;

namespace UVBake.IO;

/// <summary>
/// Entry point for mesh I/O, dispatching by file extension. OBJ (with per-wedge UVs so seams
/// round-trip) is the format read and written first; glTF arrives in a later phase
/// (see <c>docs/plans/02-phase-roadmap.md</c>).
/// </summary>
public static class MeshIo
{
    /// <summary>The mesh format read and written first (Wavefront OBJ; UVs as <c>vt</c>, per-wedge <c>f v/vt/vn</c>).</summary>
    public const string PrimaryMeshExtension = ".obj";

    /// <summary>glTF 2.0 output extension (write-only; per-vertex UVs, seams vertex-split).</summary>
    public const string GltfExtension = ".gltf";

    /// <summary>Binary FBX extension (read + write; per-wedge UVs + computed smooth normals).</summary>
    public const string FbxExtension = ".fbx";

    /// <summary>Reads a mesh, choosing the reader by <paramref name="path"/>'s extension.</summary>
    public static Mesh Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string extension = Path.GetExtension(path);
        if (extension.Equals(PrimaryMeshExtension, StringComparison.OrdinalIgnoreCase))
        {
            return ObjReader.Read(path);
        }

        if (extension.Equals(FbxExtension, StringComparison.OrdinalIgnoreCase))
        {
            return FbxReader.Read(path);
        }

        throw new NotSupportedException(
            $"Unsupported mesh format '{extension}'. Supported: {PrimaryMeshExtension}, {FbxExtension}.");
    }

    /// <summary>Writes a mesh, choosing the writer by <paramref name="path"/>'s extension.</summary>
    public static void Write(Mesh mesh, string path)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(path);
        string extension = Path.GetExtension(path);
        if (extension.Equals(PrimaryMeshExtension, StringComparison.OrdinalIgnoreCase))
        {
            ObjWriter.Write(mesh, path);
        }
        else if (extension.Equals(GltfExtension, StringComparison.OrdinalIgnoreCase))
        {
            GltfWriter.Write(mesh, path);
        }
        else if (extension.Equals(FbxExtension, StringComparison.OrdinalIgnoreCase))
        {
            FbxWriter.Write(mesh, path);
        }
        else
        {
            throw new NotSupportedException(
                $"Unsupported mesh format '{extension}'. Supported: {PrimaryMeshExtension}, {GltfExtension}, {FbxExtension}.");
        }
    }
}
