using UVBake.Core;
using UVBake.Core.Geometry;
using UVBake.Core.Meshes;
using UVBake.Core.Parameterization;
using UVBake.Core.Segmentation;
using UVBake.Tests.Corpus;
using Xunit;

namespace UVBake.Tests;

/// <summary>SLIM polish: correct analytic gradient, flip-free invariant, and distortion reduction.</summary>
public class SlimTests
{
    [Fact]
    public void EnergyGradient_MatchesCentralFiniteDifferences()
    {
        // A non-planar two-triangle chart with a flip-free (positive-area) UV layout.
        var positions = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(1, 1, 0.2),
            new Vector3(0, 1, 0),
        };
        int[] tris = [0, 1, 2, 0, 2, 3];
        var frames = new TriangleFrame[2];
        frames[0] = TriangleFrame.From(positions[0], positions[1], positions[2]);
        frames[1] = TriangleFrame.From(positions[0], positions[2], positions[3]);

        var uv = new[]
        {
            new Vector2(0.0, 0.0),
            new Vector2(1.1, 0.05),
            new Vector2(0.9, 1.0),
            new Vector2(0.1, 0.95),
        };

        var gradU = new double[4];
        var gradV = new double[4];
        double e0 = SlimParameterizer.EvaluateEnergyAndGradient(frames, tris, uv, gradU, gradV);
        Assert.False(double.IsInfinity(e0));

        const double h = 1e-6;
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(CentralDifference(frames, tris, uv, i, dx: true, h), gradU[i], precision: 5);
            Assert.Equal(CentralDifference(frames, tris, uv, i, dx: false, h), gradV[i], precision: 5);
        }
    }

    [Fact]
    public void Slim_LowersDistortionAndStaysFlipFree()
    {
        // Sphere bands are curved, so Tutte's forced-circle boundary is distorted; SLIM should reduce it.
        (HalfEdgeMesh mesh, SegmentationResult seg) = Segment(Primitives.Sphere());
        Chart chart = LargestChart(seg);

        ChartUv tutte = TutteParameterizer.Parameterize(mesh, chart.Faces);
        ChartUv slim = SlimParameterizer.Polish(tutte, iterations: 40);

        ChartDistortion before = DistortionMetrics.ForChart(tutte);
        ChartDistortion after = DistortionMetrics.ForChart(slim);

        Assert.Equal(0, after.FlipCount);
        Assert.False(after.HasInvalidValues);
        Assert.True(after.AreaWeightedL2 < before.AreaWeightedL2,
            $"SLIM L²={after.AreaWeightedL2} not below Tutte L²={before.AreaWeightedL2}");
    }

    [Fact]
    public void Pipeline_ProducesZeroFlipsOnEveryCorpusChart()
    {
        foreach ((string name, Mesh source) in Primitives.All())
        {
            (HalfEdgeMesh mesh, SegmentationResult seg) = Segment(source);
            foreach (Chart chart in seg.Charts)
            {
                ChartUv uv = ChartParameterizer.Parameterize(mesh, chart.Faces);
                ChartDistortion dist = DistortionMetrics.ForChart(uv);

                Assert.False(dist.HasInvalidValues, $"{name} chart {chart.Id}: NaN/Inf");
                Assert.Equal(0, dist.FlipCount);
            }
        }
    }

    [Fact]
    public void Pipeline_IsDeterministic()
    {
        (HalfEdgeMesh mesh, SegmentationResult seg) = Segment(Primitives.Sphere());
        Chart chart = LargestChart(seg);

        ChartUv a = ChartParameterizer.Parameterize(mesh, chart.Faces);
        ChartUv b = ChartParameterizer.Parameterize(mesh, chart.Faces);

        Assert.Equal(a.Uv, b.Uv);
    }

    private static double CentralDifference(TriangleFrame[] frames, int[] tris, Vector2[] uv, int vertex, bool dx, double h)
    {
        Vector2 original = uv[vertex];
        uv[vertex] = dx ? new Vector2(original.X + h, original.Y) : new Vector2(original.X, original.Y + h);
        double plus = SlimParameterizer.EvaluateEnergy(frames, tris, uv);
        uv[vertex] = dx ? new Vector2(original.X - h, original.Y) : new Vector2(original.X, original.Y - h);
        double minus = SlimParameterizer.EvaluateEnergy(frames, tris, uv);
        uv[vertex] = original;
        return (plus - minus) / (2 * h);
    }

    private static Chart LargestChart(SegmentationResult seg)
    {
        Chart largest = seg.Charts[0];
        foreach (Chart chart in seg.Charts)
        {
            if (chart.Faces.Count > largest.Faces.Count)
            {
                largest = chart;
            }
        }

        return largest;
    }

    private static (HalfEdgeMesh Mesh, SegmentationResult Result) Segment(Mesh source)
    {
        HalfEdgeMesh mesh = HalfEdgeMesh.Build(Triangulator.Triangulate(source));
        HardEdgeDetector.Detect(mesh, source.Creases, 45.0, SeamSource.DihedralAngle);
        return (mesh, ChartSegmenter.Segment(mesh, 0.3));
    }
}
