using UVBake.Core.Geometry;
using UVBake.Core.Meshes;
using UVBake.IO;
using UVBake.Tests.Corpus;
using Xunit;

namespace UVBake.Tests;

/// <summary>
/// Binary FBX writer -> reader round trip. Positions are stored as float by the reader, so values are
/// compared within single-precision tolerance; per-wedge UVs (seams) must survive by value.
/// </summary>
public class FbxTests
{
    private const double Tolerance = 1e-5;

    [Fact]
    public void RoundTrip_PreservesPerWedgeUvSeam()
    {
        var positions = new[]
        {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0),
        };
        var texCoords = new[]
        {
            new Vector2(0.00, 0.0), new Vector2(0.50, 0.0), new Vector2(0.50, 0.5),
            new Vector2(0.99, 0.5), new Vector2(0.60, 0.9), new Vector2(0.10, 0.9),
        };
        var faces = new List<Face>
        {
            new([new Corner(0, 0, Corner.None), new Corner(1, 1, Corner.None), new Corner(2, 2, Corner.None)]),
            new([new Corner(0, 3, Corner.None), new Corner(2, 4, Corner.None), new Corner(3, 5, Corner.None)]),
        };
        var mesh = new Mesh(positions, texCoords, Array.Empty<Vector3>(), faces);

        Mesh reloaded = WriteThenRead(mesh);

        AssertSameGeometryAndUvs(mesh, reloaded);

        // The seam survives: position 0 carries a different UV on each triangle.
        Vector2 uvA = reloaded.TexCoords[reloaded.Faces[0][0].TexCoord];
        Vector2 uvB = reloaded.TexCoords[reloaded.Faces[1][0].TexCoord];
        Assert.True((uvA - uvB).Length > 0.1);
    }

    [Fact]
    public void RoundTrip_WritesUnitSmoothNormals()
    {
        foreach ((string name, Mesh mesh) in Primitives.All())
        {
            Mesh reloaded = WriteThenRead(mesh);
            Assert.True(reloaded.HasNormals, $"{name}: writer should emit a normal layer");
            foreach (Vector3 n in reloaded.Normals)
            {
                Assert.True(System.Math.Abs(n.Length - 1.0) < 1e-3, $"{name}: normal not unit length");
            }
        }
    }

    [Fact]
    public void RoundTrip_ProceduralCorpus_PreservesGeometry()
    {
        foreach ((string name, Mesh mesh) in Primitives.All())
        {
            Mesh reloaded = WriteThenRead(mesh);
            Assert.True(reloaded.Positions.Count > 0, $"{name} lost positions");
            AssertSameGeometryAndUvs(mesh, reloaded);
        }
    }

    [Fact]
    public void MeshIo_DispatchesFbxByExtension()
    {
        string path = Path.Combine(Path.GetTempPath(), $"uvbake-{Guid.NewGuid():N}.fbx");
        try
        {
            (_, Mesh mesh) = Primitives.All().First();
            MeshIo.Write(mesh, path);
            Mesh reloaded = MeshIo.Read(path);
            Assert.Equal(mesh.Faces.Count, reloaded.Faces.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void AssertSameGeometryAndUvs(Mesh expected, Mesh actual)
    {
        Assert.Equal(expected.Positions.Count, actual.Positions.Count);
        for (int i = 0; i < expected.Positions.Count; i++)
        {
            Assert.True((expected.Positions[i] - actual.Positions[i]).Length < Tolerance, $"position {i} moved");
        }

        Assert.Equal(expected.Faces.Count, actual.Faces.Count);
        for (int f = 0; f < expected.Faces.Count; f++)
        {
            Assert.Equal(expected.Faces[f].Count, actual.Faces[f].Count);
            for (int c = 0; c < expected.Faces[f].Count; c++)
            {
                Corner e = expected.Faces[f][c];
                Corner a = actual.Faces[f][c];
                Assert.Equal(e.Position, a.Position);
                if (e.HasTexCoord)
                {
                    Assert.True(a.HasTexCoord, $"face {f} corner {c} lost its UV");
                    Vector2 d = expected.TexCoords[e.TexCoord] - actual.TexCoords[a.TexCoord];
                    Assert.True(d.Length < Tolerance, $"face {f} corner {c} UV changed");
                }
            }
        }
    }

    private static Mesh WriteThenRead(Mesh mesh)
    {
        using var buffer = new MemoryStream();
        FbxWriter.Write(mesh, buffer);
        buffer.Position = 0;
        return FbxReader.Read(buffer);
    }
}
