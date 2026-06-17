using System.Buffers.Binary;
using System.Text.Json;
using UVBake.Core.Geometry;
using UVBake.Core.Meshes;

namespace UVBake.IO;

/// <summary>
/// Writes a mesh as self-contained glTF 2.0 (JSON with an embedded base64 buffer). glTF stores UVs as a
/// <b>per-vertex</b> attribute (<c>TEXCOORD_0</c>) under one shared index buffer, so a seam — one
/// position carrying two UVs — forces vertex duplication (Documents/01 §7, 02 §5). Each unique
/// (position, texcoord) wedge therefore becomes one glTF vertex. Emits POSITION + TEXCOORD_0 + triangle
/// indices; numbers and layout are deterministic (little-endian buffer, compact JSON).
/// </summary>
public static class GltfWriter
{
    private const int ComponentFloat = 5126;
    private const int ComponentUInt = 5125;
    private const int TargetArrayBuffer = 34962;
    private const int TargetElementArrayBuffer = 34963;
    private const int ModeTriangles = 4;

    /// <summary>Writes <paramref name="mesh"/> to a glTF file at <paramref name="path"/>.</summary>
    public static void Write(Mesh mesh, string path)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(path);
        File.WriteAllText(path, ToGltfJson(mesh));
    }

    /// <summary>Writes <paramref name="mesh"/> as glTF text to <paramref name="writer"/>.</summary>
    public static void Write(Mesh mesh, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(ToGltfJson(mesh));
    }

    /// <summary>Produces the glTF JSON document for <paramref name="mesh"/> as a string.</summary>
    public static string ToGltfJson(Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        // Dedup wedges: each unique (position, texcoord) becomes one glTF vertex (seams split here).
        var vertexIndex = new Dictionary<(int Position, int TexCoord), int>();
        var positions = new List<Vector3>();
        var texCoords = new List<Vector2>();
        var indices = new List<int>();

        foreach (Face face in mesh.Faces)
        {
            foreach (Corner corner in face.Corners)
            {
                var key = (corner.Position, corner.TexCoord);
                if (!vertexIndex.TryGetValue(key, out int index))
                {
                    index = positions.Count;
                    vertexIndex[key] = index;
                    positions.Add(mesh.Positions[corner.Position]);
                    texCoords.Add(corner.HasTexCoord ? mesh.TexCoords[corner.TexCoord] : Vector2.Zero);
                }

                indices.Add(index);
            }
        }

        byte[] buffer = BuildBuffer(positions, texCoords, indices, out int posBytes, out int uvBytes, out int idxBytes);
        (Vector3 Min, Vector3 Max) bounds = Bounds(positions);

        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();

            json.WriteStartObject("asset");
            json.WriteString("version", "2.0");
            json.WriteString("generator", "UVBake");
            json.WriteEndObject();

            json.WriteNumber("scene", 0);
            json.WriteStartArray("scenes");
            json.WriteStartObject();
            json.WriteStartArray("nodes");
            json.WriteNumberValue(0);
            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndArray();

            json.WriteStartArray("nodes");
            json.WriteStartObject();
            json.WriteNumber("mesh", 0);
            json.WriteEndObject();
            json.WriteEndArray();

            WriteMeshes(json);
            WriteBuffer(json, buffer);
            WriteBufferViews(json, posBytes, uvBytes, idxBytes);
            WriteAccessors(json, positions.Count, indices.Count, bounds);

            json.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteMeshes(Utf8JsonWriter json)
    {
        json.WriteStartArray("meshes");
        json.WriteStartObject();
        json.WriteStartArray("primitives");
        json.WriteStartObject();
        json.WriteStartObject("attributes");
        json.WriteNumber("POSITION", 0);
        json.WriteNumber("TEXCOORD_0", 1);
        json.WriteEndObject();
        json.WriteNumber("indices", 2);
        json.WriteNumber("mode", ModeTriangles);
        json.WriteEndObject();
        json.WriteEndArray();
        json.WriteEndObject();
        json.WriteEndArray();
    }

    private static void WriteBuffer(Utf8JsonWriter json, byte[] buffer)
    {
        json.WriteStartArray("buffers");
        json.WriteStartObject();
        json.WriteNumber("byteLength", buffer.Length);
        json.WriteString("uri", "data:application/octet-stream;base64," + Convert.ToBase64String(buffer));
        json.WriteEndObject();
        json.WriteEndArray();
    }

    private static void WriteBufferViews(Utf8JsonWriter json, int posBytes, int uvBytes, int idxBytes)
    {
        json.WriteStartArray("bufferViews");
        WriteBufferView(json, 0, posBytes, TargetArrayBuffer);
        WriteBufferView(json, posBytes, uvBytes, TargetArrayBuffer);
        WriteBufferView(json, posBytes + uvBytes, idxBytes, TargetElementArrayBuffer);
        json.WriteEndArray();
    }

    private static void WriteBufferView(Utf8JsonWriter json, int byteOffset, int byteLength, int target)
    {
        json.WriteStartObject();
        json.WriteNumber("buffer", 0);
        json.WriteNumber("byteOffset", byteOffset);
        json.WriteNumber("byteLength", byteLength);
        json.WriteNumber("target", target);
        json.WriteEndObject();
    }

    private static void WriteAccessors(Utf8JsonWriter json, int vertexCount, int indexCount, (Vector3 Min, Vector3 Max) bounds)
    {
        json.WriteStartArray("accessors");

        // 0: POSITION (VEC3 float) — min/max required by the spec.
        json.WriteStartObject();
        json.WriteNumber("bufferView", 0);
        json.WriteNumber("componentType", ComponentFloat);
        json.WriteNumber("count", vertexCount);
        json.WriteString("type", "VEC3");
        WriteVec3(json, "min", bounds.Min);
        WriteVec3(json, "max", bounds.Max);
        json.WriteEndObject();

        // 1: TEXCOORD_0 (VEC2 float).
        json.WriteStartObject();
        json.WriteNumber("bufferView", 1);
        json.WriteNumber("componentType", ComponentFloat);
        json.WriteNumber("count", vertexCount);
        json.WriteString("type", "VEC2");
        json.WriteEndObject();

        // 2: indices (SCALAR uint).
        json.WriteStartObject();
        json.WriteNumber("bufferView", 2);
        json.WriteNumber("componentType", ComponentUInt);
        json.WriteNumber("count", indexCount);
        json.WriteString("type", "SCALAR");
        json.WriteEndObject();

        json.WriteEndArray();
    }

    private static void WriteVec3(Utf8JsonWriter json, string name, Vector3 v)
    {
        json.WriteStartArray(name);
        json.WriteNumberValue((float)v.X);
        json.WriteNumberValue((float)v.Y);
        json.WriteNumberValue((float)v.Z);
        json.WriteEndArray();
    }

    private static byte[] BuildBuffer(
        List<Vector3> positions, List<Vector2> texCoords, List<int> indices,
        out int posBytes, out int uvBytes, out int idxBytes)
    {
        posBytes = positions.Count * 3 * sizeof(float);
        uvBytes = texCoords.Count * 2 * sizeof(float);
        idxBytes = indices.Count * sizeof(uint);

        var buffer = new byte[posBytes + uvBytes + idxBytes];
        var span = buffer.AsSpan();
        int offset = 0;

        foreach (Vector3 p in positions)
        {
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], (float)p.X);
            BinaryPrimitives.WriteSingleLittleEndian(span[(offset + 4)..], (float)p.Y);
            BinaryPrimitives.WriteSingleLittleEndian(span[(offset + 8)..], (float)p.Z);
            offset += 12;
        }

        foreach (Vector2 uv in texCoords)
        {
            BinaryPrimitives.WriteSingleLittleEndian(span[offset..], (float)uv.X);
            BinaryPrimitives.WriteSingleLittleEndian(span[(offset + 4)..], (float)uv.Y);
            offset += 8;
        }

        foreach (int index in indices)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], (uint)index);
            offset += 4;
        }

        return buffer;
    }

    private static (Vector3 Min, Vector3 Max) Bounds(List<Vector3> positions)
    {
        if (positions.Count == 0)
        {
            return (Vector3.Zero, Vector3.Zero);
        }

        var min = new Vector3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        var max = new Vector3(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);
        foreach (Vector3 p in positions)
        {
            min = new Vector3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
            max = new Vector3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
        }

        return (min, max);
    }
}
