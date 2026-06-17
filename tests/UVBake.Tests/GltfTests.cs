using System.Buffers.Binary;
using System.Text.Json;
using UVBake.Core;
using UVBake.Core.Meshes;
using UVBake.IO;
using UVBake.Tests.Corpus;
using Xunit;

namespace UVBake.Tests;

/// <summary>glTF 2.0 export: valid structure, decodable buffer, per-vertex UVs with seams vertex-split.</summary>
public class GltfTests
{
    [Fact]
    public void Gltf_HasValidStructureAndDecodableBuffer()
    {
        UvMesh atlas = UVBaker.Generate(Primitives.Cube(), new UVBakeOptions());
        Mesh mesh = atlas.Mesh;

        using JsonDocument doc = JsonDocument.Parse(GltfWriter.ToGltfJson(mesh));
        JsonElement root = doc.RootElement;

        Assert.Equal("2.0", root.GetProperty("asset").GetProperty("version").GetString());
        Assert.Equal(1, root.GetProperty("meshes").GetArrayLength());

        JsonElement primitive = root.GetProperty("meshes")[0].GetProperty("primitives")[0];
        Assert.Equal(0, primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32());
        Assert.Equal(1, primitive.GetProperty("attributes").GetProperty("TEXCOORD_0").GetInt32());
        Assert.Equal(4, primitive.GetProperty("mode").GetInt32()); // TRIANGLES

        JsonElement accessors = root.GetProperty("accessors");
        int vertexCount = accessors[0].GetProperty("count").GetInt32();
        int uvCount = accessors[1].GetProperty("count").GetInt32();
        int indexCount = accessors[2].GetProperty("count").GetInt32();

        Assert.Equal(vertexCount, uvCount);
        Assert.Equal("VEC3", accessors[0].GetProperty("type").GetString());
        Assert.Equal("VEC2", accessors[1].GetProperty("type").GetString());
        Assert.Equal("SCALAR", accessors[2].GetProperty("type").GetString());
        Assert.Equal(3 * mesh.Faces.Count, indexCount);

        // Seams force vertex duplication: more glTF vertices than mesh positions.
        Assert.True(vertexCount > mesh.Positions.Count);

        // Decode the embedded buffer and confirm positions/UVs round-trip.
        byte[] buffer = DecodeBuffer(root);
        var positionSet = new HashSet<(float, float, float)>();
        foreach (var p in mesh.Positions)
        {
            positionSet.Add(((float)p.X, (float)p.Y, (float)p.Z));
        }

        for (int v = 0; v < vertexCount; v++)
        {
            int o = v * 12;
            float x = BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(o));
            float y = BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(o + 4));
            float z = BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(o + 8));
            Assert.Contains((x, y, z), positionSet);

            int uo = (vertexCount * 12) + (v * 8);
            float u = BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(uo));
            float w = BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(uo + 4));
            Assert.InRange(u, -1e-4f, 1 + 1e-4f);
            Assert.InRange(w, -1e-4f, 1 + 1e-4f);
        }
    }

    [Fact]
    public void MeshIo_DispatchesGltfByExtension()
    {
        UvMesh atlas = UVBaker.Generate(Primitives.Cube(), new UVBakeOptions());
        string path = Path.Combine(Path.GetTempPath(), $"uvbake_gltf_{Guid.NewGuid():N}.gltf");
        try
        {
            MeshIo.Write(atlas.Mesh, path);
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal("2.0", doc.RootElement.GetProperty("asset").GetProperty("version").GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] DecodeBuffer(JsonElement root)
    {
        string uri = root.GetProperty("buffers")[0].GetProperty("uri").GetString()!;
        int comma = uri.IndexOf(',', StringComparison.Ordinal);
        Assert.StartsWith("data:application/octet-stream;base64,", uri);
        return Convert.FromBase64String(uri[(comma + 1)..]);
    }
}
