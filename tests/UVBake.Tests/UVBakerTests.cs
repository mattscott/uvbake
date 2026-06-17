using UVBake.Core;
using UVBake.Core.Geometry;
using UVBake.Core.Meshes;
using UVBake.IO;
using UVBake.Tests.Corpus;
using Xunit;

namespace UVBake.Tests;

/// <summary>P4 / M5 acceptance: end-to-end mesh → unique bake-ready atlas, hard gates pass on the corpus.</summary>
public class UVBakerTests
{
    [Fact]
    public void Generate_ProducesValidAtlasForEveryCorpusMesh()
    {
        foreach ((string name, Mesh source) in Primitives.All())
        {
            UvMesh result = UVBaker.Generate(source, new UVBakeOptions());

            Assert.True(result.Validation.OverlapFree, $"{name}: {result.Validation.OverlapCount} overlaps");
            Assert.Equal(0, result.Validation.FlipCount);
            Assert.True(result.Validation.FullCoverage, $"{name}: coverage failed");
            Assert.True(result.Validation.IsValid, $"{name}: atlas invalid");

            // Every UV lands inside the [0,1] atlas tile.
            foreach (Vector2 uv in result.Mesh.TexCoords)
            {
                Assert.InRange(uv.X, -1e-9, 1 + 1e-9);
                Assert.InRange(uv.Y, -1e-9, 1 + 1e-9);
            }
        }
    }

    [Fact]
    public void Generate_PreservesPositionsAndProducesSeamWedges()
    {
        Mesh cube = Primitives.Cube();
        UvMesh result = UVBaker.Generate(cube, new UVBakeOptions());

        // Positions are shared with the (triangulated) input; the cube has 8.
        Assert.Equal(8, result.Mesh.Positions.Count);

        // Seams duplicate UVs: the closed cube must carry more texcoords than positions.
        Assert.True(result.Mesh.TexCoords.Count > result.Mesh.Positions.Count);
        Assert.True(result.Mesh.HasTexCoords);
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        Mesh sphere = Primitives.Sphere();
        UvMesh a = UVBaker.Generate(sphere, new UVBakeOptions());
        UvMesh b = UVBaker.Generate(sphere, new UVBakeOptions());

        Assert.Equal(a.Mesh.TexCoords.Count, b.Mesh.TexCoords.Count);
        for (int i = 0; i < a.Mesh.TexCoords.Count; i++)
        {
            Assert.Equal(a.Mesh.TexCoords[i], b.Mesh.TexCoords[i]);
        }
    }

    [Fact]
    public void Generate_OutputRoundTripsThroughObj()
    {
        UvMesh result = UVBaker.Generate(Primitives.Cylinder(), new UVBakeOptions());

        var buffer = new StringWriter();
        ObjWriter.Write(result.Mesh, buffer);
        using var reader = new StringReader(buffer.ToString());
        Mesh reloaded = ObjReader.Read(reader);

        TestSupport.AssertMeshEqual(result.Mesh, reloaded);
    }
}
