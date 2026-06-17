using UVBake.Core;
using UVBake.Core.Geometry;
using UVBake.Core.Meshes;
using UVBake.Tests.Corpus;
using Xunit;

namespace UVBake.Tests;

/// <summary>P6 determinism: parallel-across-charts output is bit-identical to serial and stable across runs.</summary>
public class DeterminismTests
{
    [Fact]
    public void Generate_ParallelMatchesSingleThreaded()
    {
        foreach ((string name, Mesh source) in Primitives.All())
        {
            UvMesh serial = UVBaker.Generate(source, new UVBakeOptions { Threads = 1 });
            UvMesh parallel = UVBaker.Generate(source, new UVBakeOptions { Threads = -1 });

            AssertTexCoordsIdentical(serial, parallel, name);
        }
    }

    [Fact]
    public void Generate_IsBitIdenticalAcrossRuns()
    {
        foreach ((string name, Mesh source) in Primitives.All())
        {
            UvMesh first = UVBaker.Generate(source, new UVBakeOptions());
            UvMesh second = UVBaker.Generate(source, new UVBakeOptions());

            AssertTexCoordsIdentical(first, second, name);
        }
    }

    private static void AssertTexCoordsIdentical(UvMesh a, UvMesh b, string name)
    {
        Assert.Equal(a.Mesh.TexCoords.Count, b.Mesh.TexCoords.Count);
        for (int i = 0; i < a.Mesh.TexCoords.Count; i++)
        {
            Vector2 ua = a.Mesh.TexCoords[i];
            Vector2 ub = b.Mesh.TexCoords[i];
            Assert.True(ua.X == ub.X && ua.Y == ub.Y, $"{name}: texcoord {i} differs ({ua} vs {ub})");
        }
    }
}
