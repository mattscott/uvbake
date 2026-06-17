using UVBake.Core;
using UVBake.Core.Geometry;
using UVBake.Core.Meshes;
using UVBake.Core.Parameterization;
using UVBake.Core.Segmentation;
using UVBake.Tests.Corpus;
using Xunit;

namespace UVBake.Tests;

/// <summary>
/// P3 kill-point validation in isolation: the managed solver + Tutte produce a guaranteed-injective
/// (zero-flip) parameterization of every chart on the corpus, with no NaNs, and deterministically.
/// </summary>
public class ParameterizationTests
{
    private static readonly IReadOnlySet<EdgeKey> NoCreases = new HashSet<EdgeKey>();

    [Fact]
    public void Tutte_ProducesZeroFlipsOnEveryCorpusChart()
    {
        foreach ((string name, Mesh source) in Primitives.All())
        {
            (HalfEdgeMesh mesh, SegmentationResult seg) = Segment(source);

            foreach (Chart chart in seg.Charts)
            {
                ChartUv uv = TutteParameterizer.Parameterize(mesh, chart.Faces);
                ChartDistortion dist = DistortionMetrics.ForChart(uv);

                Assert.False(dist.HasInvalidValues, $"{name} chart {chart.Id}: produced NaN/Inf");
                Assert.Equal(0, dist.FlipCount);
            }
        }
    }

    [Fact]
    public void Lscm_PlanarChartsAreConformalAndFlipFree()
    {
        // Cube faces are planar → LSCM drives conformal energy to ~0 → K ≈ 1 with no flips.
        (HalfEdgeMesh mesh, SegmentationResult seg) = Segment(Primitives.Cube());

        foreach (Chart chart in seg.Charts)
        {
            ChartUv uv = LscmParameterizer.Parameterize(mesh, chart.Faces);
            ChartDistortion dist = DistortionMetrics.ForChart(uv);

            Assert.False(dist.HasInvalidValues);
            Assert.Equal(0, dist.FlipCount);
            Assert.True(dist.MaxQuasiConformal < 1.001, $"K={dist.MaxQuasiConformal}");
        }
    }

    [Fact]
    public void Lscm_DevelopableChartsAreFlipFree()
    {
        (HalfEdgeMesh mesh, SegmentationResult seg) = Segment(Primitives.Cylinder());

        foreach (Chart chart in seg.Charts)
        {
            ChartUv uv = LscmParameterizer.Parameterize(mesh, chart.Faces);
            ChartDistortion dist = DistortionMetrics.ForChart(uv);

            Assert.False(dist.HasInvalidValues);
            Assert.Equal(0, dist.FlipCount);
        }
    }

    [Fact]
    public void Tutte_IsDeterministic()
    {
        (HalfEdgeMesh mesh, SegmentationResult seg) = Segment(Primitives.Sphere());
        Chart chart = seg.Charts[0];

        ChartUv a = TutteParameterizer.Parameterize(mesh, chart.Faces);
        ChartUv b = TutteParameterizer.Parameterize(mesh, chart.Faces);

        Assert.Equal(a.Uv, b.Uv);
    }

    [Fact]
    public void Distortion_IdentityMapHasUnitSingularValues()
    {
        TriangleDistortion d = DistortionMetrics.ForTriangle(
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0),
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1));

        Assert.Equal(1.0, d.Sigma1, precision: 9);
        Assert.Equal(1.0, d.Sigma2, precision: 9);
        Assert.False(d.IsFlipped);
        Assert.Equal(1.0, d.QuasiConformal, precision: 9);
    }

    [Fact]
    public void Distortion_UniformScaleScalesBothSingularValues()
    {
        TriangleDistortion d = DistortionMetrics.ForTriangle(
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0),
            new Vector2(0, 0), new Vector2(3, 0), new Vector2(0, 3));

        Assert.Equal(3.0, d.Sigma1, precision: 9);
        Assert.Equal(3.0, d.Sigma2, precision: 9);
        Assert.False(d.IsFlipped); // conformal (K=1), just scaled
        Assert.Equal(1.0, d.QuasiConformal, precision: 9);
    }

    [Fact]
    public void Distortion_MirroredTriangleIsFlipped()
    {
        TriangleDistortion d = DistortionMetrics.ForTriangle(
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0),
            new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0)); // swapped → reflection

        Assert.True(d.IsFlipped);
        Assert.True(d.SignedAreaRatio < 0);
    }

    [Fact]
    public void Distortion_ShearRaisesQuasiConformal()
    {
        TriangleDistortion d = DistortionMetrics.ForTriangle(
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0),
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1)); // shear

        Assert.False(d.IsFlipped);
        Assert.True(d.QuasiConformal > 1.0);
    }

    private static (HalfEdgeMesh Mesh, SegmentationResult Result) Segment(Mesh source)
    {
        HalfEdgeMesh mesh = HalfEdgeMesh.Build(Triangulator.Triangulate(source));
        HardEdgeDetector.Detect(mesh, source.Creases, 45.0, SeamSource.DihedralAngle);
        return (mesh, ChartSegmenter.Segment(mesh, 0.3));
    }
}
