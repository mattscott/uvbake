using UVBake.Core;
using UVBake.Core.Geometry;
using UVBake.Core.Meshes;
using UVBake.Core.Packing;
using UVBake.Core.Parameterization;
using UVBake.Core.Segmentation;
using UVBake.Core.Validation;
using UVBake.Tests.Corpus;
using Xunit;

namespace UVBake.Tests;

/// <summary>Texel-density normalization and atlas packing in isolation.</summary>
public class PackingTests
{
    [Fact]
    public void Normalize_MakesUvAreaEqualSurfaceArea()
    {
        (HalfEdgeMesh mesh, SegmentationResult seg) = Segment(Primitives.Sphere());
        Chart chart = seg.Charts[0];
        ChartUv uv = ChartParameterizer.Parameterize(mesh, chart.Faces);

        TexelDensityNormalizer.Normalize(uv);

        Assert.Equal(SurfaceArea(uv), UvArea(uv), precision: 6);
    }

    [Fact]
    public void Pack_GivesUniformTexelDensityAcrossCharts()
    {
        (HalfEdgeMesh mesh, SegmentationResult seg) = Segment(Primitives.Cylinder());
        var charts = new List<ChartUv>();
        foreach (Chart chart in seg.Charts)
        {
            charts.Add(ChartParameterizer.Parameterize(mesh, chart.Faces));
        }

        TexelDensityNormalizer.Normalize(charts);
        AtlasPacker.Pack(charts, new UVBakeOptions());

        // A single global scale is applied, so every chart shares one texels-per-unit.
        double first = Math.Sqrt(UvArea(charts[0]) / SurfaceArea(charts[0]));
        foreach (ChartUv chart in charts)
        {
            double density = Math.Sqrt(UvArea(chart) / SurfaceArea(chart));
            Assert.Equal(first, density, precision: 6);
        }
    }

    [Fact]
    public void Pack_ProducesNoInterChartOverlap()
    {
        (HalfEdgeMesh mesh, SegmentationResult seg) = Segment(Primitives.Torus());
        var charts = new List<ChartUv>();
        foreach (Chart chart in seg.Charts)
        {
            charts.Add(ChartParameterizer.Parameterize(mesh, chart.Faces));
        }

        TexelDensityNormalizer.Normalize(charts);
        AtlasPacker.Pack(charts, new UVBakeOptions());

        Assert.Equal(0, OverlapValidator.CountInterChartOverlaps(charts));
    }

    [Fact]
    public void Silhouette_PacksOverlapFreeWithComparableEfficiency()
    {
        // The silhouette packer is overlap-free and gutter-reserved by construction. On near-convex
        // corpus charts it is comparable to the bbox packer (its nesting advantage needs concave/overhang
        // charts); here we assert it is a correct, non-regressing packer in the same efficiency ballpark.
        var options = new UVBakeOptions { Resolution = 256 };

        double bboxEfficiency = PackEfficiency(Primitives.Sphere(), options, PackerKind.BoundingBox, out _);
        double silhouetteEfficiency = PackEfficiency(Primitives.Sphere(), options, PackerKind.Silhouette, out var silhouetteCharts);

        Assert.Equal(0, OverlapValidator.CountInterChartOverlaps(silhouetteCharts));
        Assert.InRange(silhouetteEfficiency, 0.8 * bboxEfficiency, 2.0 * bboxEfficiency);
        foreach (ChartUv chart in silhouetteCharts)
        {
            foreach (Vector2 uv in chart.Uv)
            {
                Assert.InRange(uv.X, -1e-9, 1 + 1e-9);
                Assert.InRange(uv.Y, -1e-9, 1 + 1e-9);
            }
        }
    }

    [Fact]
    public void Silhouette_EndToEndProducesValidAtlas()
    {
        UvMesh result = UVBaker.Generate(Primitives.Sphere(), new UVBakeOptions { Resolution = 512, Packer = PackerKind.Silhouette });
        Assert.True(result.Validation.IsValid);
    }

    private static double PackEfficiency(Mesh source, UVBakeOptions options, PackerKind kind, out List<ChartUv> charts)
    {
        (HalfEdgeMesh mesh, SegmentationResult seg) = Segment(source);
        charts = [];
        foreach (Chart chart in seg.Charts)
        {
            charts.Add(ChartParameterizer.Parameterize(mesh, chart.Faces));
        }

        TexelDensityNormalizer.Normalize(charts);
        return kind == PackerKind.Silhouette
            ? SilhouettePacker.Pack(charts, options).Efficiency
            : AtlasPacker.Pack(charts, options).Efficiency;
    }

    private static double SurfaceArea(ChartUv chart)
    {
        double area = 0;
        int[] t = chart.Triangles;
        for (int i = 0; i < t.Length; i += 3)
        {
            area += 0.5 * (chart.Position3D[t[i + 1]] - chart.Position3D[t[i]])
                .Cross(chart.Position3D[t[i + 2]] - chart.Position3D[t[i]]).Length;
        }

        return area;
    }

    private static double UvArea(ChartUv chart)
    {
        double area = 0;
        int[] t = chart.Triangles;
        for (int i = 0; i < t.Length; i += 3)
        {
            area += 0.5 * Math.Abs((chart.Uv[t[i + 1]] - chart.Uv[t[i]]).Cross(chart.Uv[t[i + 2]] - chart.Uv[t[i]]));
        }

        return area;
    }

    private static (HalfEdgeMesh Mesh, SegmentationResult Result) Segment(Mesh source)
    {
        HalfEdgeMesh mesh = HalfEdgeMesh.Build(Triangulator.Triangulate(source));
        HardEdgeDetector.Detect(mesh, source.Creases, 45.0, SeamSource.DihedralAngle);
        return (mesh, ChartSegmenter.Segment(mesh, 0.3));
    }
}
