using UVBake.Core.Geometry;
using UVBake.Core.Meshes;
using UVBake.IO;
using UVBake.Tests.Corpus;
using Xunit;

namespace UVBake.Tests;

/// <summary>OBJ reader/writer round-trip — the P1 acceptance: positions + per-wedge UVs survive intact.</summary>
public class IoTests
{
    [Fact]
    public void RoundTrip_PreservesPerWedgeUvSeam()
    {
        // A quad split into two triangles where the shared positions (p0, p2) carry DIFFERENT texcoords
        // on each triangle — i.e. a UV seam expressed as per-wedge UVs over shared positions.
        var positions = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(1, 1, 0),
            new Vector3(0, 1, 0),
        };
        var texCoords = new[]
        {
            new Vector2(0.00, 0.0), // t0 for p0 on triangle A
            new Vector2(0.50, 0.0), // t1 for p1
            new Vector2(0.50, 0.5), // t2 for p2 on triangle A
            new Vector2(0.99, 0.5), // t3 for p0 on triangle B (seam: different UV at same position)
            new Vector2(0.60, 0.9), // t4 for p2 on triangle B (seam)
            new Vector2(0.10, 0.9), // t5 for p3
        };
        var faces = new List<Face>
        {
            new([new Corner(0, 0, Corner.None), new Corner(1, 1, Corner.None), new Corner(2, 2, Corner.None)]),
            new([new Corner(0, 3, Corner.None), new Corner(2, 4, Corner.None), new Corner(3, 5, Corner.None)]),
        };
        var mesh = new Mesh(positions, texCoords, Array.Empty<Vector3>(), faces);

        Mesh reloaded = WriteThenRead(mesh);

        TestSupport.AssertMeshEqual(mesh, reloaded);

        // The seam is intact: position 0 is referenced by two corners with different texcoord indices.
        Assert.Equal(0, reloaded.Faces[0][0].Position);
        Assert.Equal(0, reloaded.Faces[1][0].Position);
        Assert.NotEqual(reloaded.Faces[0][0].TexCoord, reloaded.Faces[1][0].TexCoord);
    }

    [Fact]
    public void RoundTrip_PreservesNormalsAndMixedCornerForms()
    {
        var positions = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0) };
        var normals = new[] { new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1) };
        var faces = new List<Face>
        {
            // v//vn form: normals present, no texcoords.
            new([new Corner(0, Corner.None, 0), new Corner(1, Corner.None, 1), new Corner(2, Corner.None, 2)]),
        };
        var mesh = new Mesh(positions, Array.Empty<Vector2>(), normals, faces);

        TestSupport.AssertMeshEqual(mesh, WriteThenRead(mesh));
    }

    [Fact]
    public void RoundTrip_ProceduralCorpus_PreservesPositionsAndConnectivity()
    {
        foreach ((string name, Mesh mesh) in Primitives.All())
        {
            Mesh reloaded = WriteThenRead(mesh);
            TestSupport.AssertMeshEqual(mesh, reloaded);
            Assert.True(reloaded.Positions.Count > 0, $"{name} lost positions");
        }
    }

    [Fact]
    public void Reader_HandlesNegativeIndices()
    {
        const string obj = "v 0 0 0\nv 1 0 0\nv 0 1 0\nf -3 -2 -1\n";
        using var reader = new StringReader(obj);
        Mesh mesh = ObjReader.Read(reader);

        Assert.Single(mesh.Faces);
        Assert.Equal(0, mesh.Faces[0][0].Position);
        Assert.Equal(1, mesh.Faces[0][1].Position);
        Assert.Equal(2, mesh.Faces[0][2].Position);
    }

    [Fact]
    public void Reader_DerivesCreasesFromSmoothingGroups()
    {
        // Two triangles forming a quad, in different smoothing groups → the shared edge is a crease.
        const string obj =
            "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\n" +
            "s 1\nf 1 2 3\n" +
            "s 2\nf 1 3 4\n";
        using var reader = new StringReader(obj);
        Mesh mesh = ObjReader.Read(reader);

        Assert.Contains(new EdgeKey(0, 2), mesh.Creases); // shared diagonal edge (positions 0 and 2)
        Assert.DoesNotContain(new EdgeKey(0, 1), mesh.Creases); // interior to one group only
    }

    private static Mesh WriteThenRead(Mesh mesh)
    {
        var buffer = new StringWriter();
        ObjWriter.Write(mesh, buffer);
        using var reader = new StringReader(buffer.ToString());
        return ObjReader.Read(reader);
    }
}
