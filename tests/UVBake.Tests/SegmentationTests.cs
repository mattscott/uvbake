using UVBake.Core;
using UVBake.Core.Geometry;
using UVBake.Core.Meshes;
using UVBake.Core.Segmentation;
using UVBake.Tests.Corpus;
using Xunit;

namespace UVBake.Tests;

/// <summary>P2 acceptance: disk-topology charts, hard edges as seams, full coverage, determinism.</summary>
public class SegmentationTests
{
    private static readonly IReadOnlySet<EdgeKey> NoCreases = new HashSet<EdgeKey>();

    [Fact]
    public void Cube_SegmentsIntoSixPlanarCharts()
    {
        (HalfEdgeMesh mesh, SegmentationResult result) = Segment(Primitives.Cube(), hardAngleDeg: 45, devAngleRad: 0.4);

        Assert.Equal(6, result.Charts.Count);
        foreach (Chart chart in result.Charts)
        {
            Assert.Equal(2, chart.Faces.Count);     // two triangles per cube face
            Assert.Equal(1, Chi(mesh, chart.Faces)); // disk
        }
    }

    [Fact]
    public void FlatGrid_MergesIntoSingleChart()
    {
        (HalfEdgeMesh mesh, SegmentationResult result) = Segment(FlatGrid(4), hardAngleDeg: 45, devAngleRad: 0.1);

        Assert.Single(result.Charts);
        Assert.Equal(mesh.TriangleCount, result.Charts[0].Faces.Count);
        Assert.Equal(1, Chi(mesh, result.Charts[0].Faces));
    }

    [Fact]
    public void Corpus_EveryChartIsADisk_AndCoversEveryFaceOnce()
    {
        foreach ((string name, Mesh source) in Primitives.All())
        {
            (HalfEdgeMesh mesh, SegmentationResult result) = Segment(source, hardAngleDeg: 45, devAngleRad: 0.3);

            // Coverage: face→chart labels and the charts' face lists agree and partition all triangles.
            var seen = new HashSet<int>();
            foreach (Chart chart in result.Charts)
            {
                Assert.Equal(1, Chi(mesh, chart.Faces)); // disk by construction
                foreach (int face in chart.Faces)
                {
                    Assert.True(seen.Add(face), $"{name}: face {face} in two charts");
                    Assert.Equal(chart.Id, result.FaceChart[face]);
                }
            }

            Assert.Equal(mesh.TriangleCount, seen.Count);
        }
    }

    [Fact]
    public void Cube_AllHardEdgesAreSeams()
    {
        (HalfEdgeMesh mesh, SegmentationResult result) = Segment(Primitives.Cube(), hardAngleDeg: 45, devAngleRad: 0.4);

        var seams = new HashSet<EdgeKey>(result.SeamEdges());
        foreach (EdgeKey hard in mesh.HardEdgeKeys())
        {
            Assert.Contains(hard, seams);
        }
    }

    [Fact]
    public void Torus_IsCoveredByMultipleDisks()
    {
        // Genus 1: no single chart can be a disk, so the torus must split into several disk charts.
        (HalfEdgeMesh mesh, SegmentationResult result) = Segment(Primitives.Torus(), hardAngleDeg: 45, devAngleRad: 0.3);

        Assert.True(result.Charts.Count > 1);
        foreach (Chart chart in result.Charts)
        {
            Assert.Equal(1, Chi(mesh, chart.Faces));
        }
    }

    [Fact]
    public void Segmentation_IsDeterministic()
    {
        Mesh sphere = Primitives.Sphere();
        SegmentationResult a = Segment(sphere, 45, 0.3).Result;
        SegmentationResult b = Segment(sphere, 45, 0.3).Result;

        Assert.Equal(a.Charts.Count, b.Charts.Count);
        Assert.Equal(a.FaceChart, b.FaceChart);
    }

    private static (HalfEdgeMesh Mesh, SegmentationResult Result) Segment(
        Mesh source, double hardAngleDeg, double devAngleRad)
    {
        HalfEdgeMesh mesh = HalfEdgeMesh.Build(Triangulator.Triangulate(source));
        HardEdgeDetector.Detect(mesh, source.Creases, hardAngleDeg, SeamSource.DihedralAngle);
        return (mesh, ChartSegmenter.Segment(mesh, devAngleRad));
    }

    /// <summary>Euler characteristic of a triangle set; equals 1 for a topological disk.</summary>
    private static int Chi(HalfEdgeMesh mesh, IReadOnlyList<int> faces)
    {
        var vertices = new HashSet<int>();
        var edges = new HashSet<EdgeKey>();
        foreach (int f in faces)
        {
            for (int i = 0; i < 3; i++)
            {
                int h = (3 * f) + i;
                vertices.Add(mesh.Origin(h));
                edges.Add(mesh.EdgeKeyOf(h));
            }
        }

        return vertices.Count - edges.Count + faces.Count;
    }

    private static Mesh FlatGrid(int n)
    {
        var positions = new List<Vector3>();
        for (int j = 0; j <= n; j++)
        {
            for (int i = 0; i <= n; i++)
            {
                positions.Add(new Vector3(i, j, 0));
            }
        }

        int Idx(int i, int j) => (j * (n + 1)) + i;

        var faces = new List<Face>();
        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++)
            {
                faces.Add(new Face([
                    new Corner(Idx(i, j), Corner.None, Corner.None),
                    new Corner(Idx(i + 1, j), Corner.None, Corner.None),
                    new Corner(Idx(i + 1, j + 1), Corner.None, Corner.None),
                    new Corner(Idx(i, j + 1), Corner.None, Corner.None),
                ]));
            }
        }

        return new Mesh(positions, Array.Empty<Vector2>(), Array.Empty<Vector3>(), faces);
    }
}
