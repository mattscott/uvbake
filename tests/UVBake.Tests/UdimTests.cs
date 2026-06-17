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

/// <summary>UDIM multi-tile spill when a fixed texel density overflows one 0–1 tile.</summary>
public class UdimTests
{
    [Fact]
    public void HighDensity_SpillsAcrossTilesWithoutOverlap()
    {
        // A high fixed density cannot fit the sphere's charts in one tile, forcing a UDIM spill.
        var options = new UVBakeOptions { Resolution = 128, TexelsPerUnit = 120 };

        (HalfEdgeMesh mesh, SegmentationResult seg) = Segment(Primitives.Sphere());
        var charts = new List<ChartUv>();
        foreach (Chart chart in seg.Charts)
        {
            charts.Add(ChartParameterizer.Parameterize(mesh, chart.Faces));
        }

        TexelDensityNormalizer.Normalize(charts);
        PackResult pack = AtlasPacker.PackUdim(charts, options);

        Assert.True(pack.TileCount > 1, $"expected spill, got {pack.TileCount} tile(s)");
        Assert.Equal(0, OverlapValidator.CountInterChartOverlaps(charts)); // UDIM-safe: tiles never collide

        // Charts landed in more than one tile (some UVs beyond the first 0–1 tile).
        bool anyBeyondFirstTile = false;
        var occupiedTiles = new HashSet<(int, int)>();
        foreach (ChartUv chart in charts)
        {
            foreach (Vector2 uv in chart.Uv)
            {
                int tu = (int)Math.Floor(uv.X);
                int tv = (int)Math.Floor(uv.Y);
                occupiedTiles.Add((tu, tv));
                if (tu > 0 || tv > 0)
                {
                    anyBeyondFirstTile = true;
                }
            }
        }

        Assert.True(anyBeyondFirstTile);
        Assert.Equal(pack.TileCount, occupiedTiles.Count);
    }

    [Fact]
    public void UniformDensity_AcrossAllTiles()
    {
        // Fixed density ⇒ every chart shares exactly that texels-per-unit, in any tile.
        var options = new UVBakeOptions { Resolution = 256, TexelsPerUnit = 80 };

        (HalfEdgeMesh mesh, SegmentationResult seg) = Segment(Primitives.Sphere());
        var charts = new List<ChartUv>();
        foreach (Chart chart in seg.Charts)
        {
            charts.Add(ChartParameterizer.Parameterize(mesh, chart.Faces));
        }

        TexelDensityNormalizer.Normalize(charts);
        AtlasPacker.PackUdim(charts, options);

        // UV here spans multiple tiles, so measure density on tile-local coordinates.
        foreach (ChartUv chart in charts)
        {
            double density = Math.Sqrt(TileLocalUvArea(chart) / SurfaceArea(chart)) * options.Resolution;
            Assert.Equal(options.TexelsPerUnit!.Value, density, precision: 3);
        }
    }

    [Fact]
    public void Generate_WithFixedDensity_ProducesValidUdimAtlas()
    {
        UvMesh result = UVBaker.Generate(Primitives.Sphere(), new UVBakeOptions { Resolution = 256, TexelsPerUnit = 90 });

        Assert.True(result.Validation.IsValid);
        Assert.True(result.TileCount >= 1);
    }

    private static double TileLocalUvArea(ChartUv chart)
    {
        // Strip the tile offset (integer part of the first vertex) before measuring area within a tile.
        double tu = Math.Floor(chart.Uv[0].X);
        double tv = Math.Floor(chart.Uv[0].Y);
        double area = 0;
        int[] t = chart.Triangles;
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector2 a = chart.Uv[t[i]] - new Vector2(tu, tv);
            Vector2 b = chart.Uv[t[i + 1]] - new Vector2(tu, tv);
            Vector2 c = chart.Uv[t[i + 2]] - new Vector2(tu, tv);
            area += 0.5 * Math.Abs((b - a).Cross(c - a));
        }

        return area;
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

    private static (HalfEdgeMesh Mesh, SegmentationResult Result) Segment(Mesh source)
    {
        HalfEdgeMesh mesh = HalfEdgeMesh.Build(Triangulator.Triangulate(source));
        HardEdgeDetector.Detect(mesh, source.Creases, 45.0, SeamSource.DihedralAngle);
        return (mesh, ChartSegmenter.Segment(mesh, 0.3));
    }
}
