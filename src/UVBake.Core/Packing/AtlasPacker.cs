using UVBake.Core.Geometry;
using UVBake.Core.Parameterization;

namespace UVBake.Core.Packing;

/// <summary>Result of packing: atlas resolution, the texel fraction the islands occupy, and UDIM tile count.</summary>
public sealed record PackResult(int Resolution, double Efficiency, int ChartCount, int TileCount = 1);

/// <summary>
/// Packs normalized charts into one square atlas with a reserved gutter (Documents/05 §1–2). Each chart's
/// bounding box is inflated by the gutter (so islands end up ≥ gutter texels apart), the global texels-per-unit
/// scale is chosen by binary search for the tightest fit, and a skyline bottom-left bin places the boxes.
/// Charts taller than wide are rotated 90° (a proper rotation, det +1, so no flip) when rotation is allowed.
/// Output UVs are written back in the [0,1] atlas tile.
/// <para>This is a bounding-box packer — correct (overlap-free, gutter-reserved) and fast. Silhouette
/// "Tetris" packing for tighter fills is the P5 efficiency refinement.</para>
/// </summary>
public static class AtlasPacker
{
    public static PackResult Pack(IReadOnlyList<ChartUv> charts, UVBakeOptions options)
    {
        ArgumentNullException.ThrowIfNull(charts);
        ArgumentNullException.ThrowIfNull(options);

        int resolution = options.PowerOfTwo ? NextPowerOfTwo(options.Resolution) : Math.Max(1, options.Resolution);
        int gutter = Math.Max(0, options.GutterPixels);
        if (charts.Count == 0)
        {
            return new PackResult(resolution, 0, 0);
        }

        var size = new (double W, double H)[charts.Count];
        double totalBoxArea = 0;
        double totalUvArea = 0;
        for (int i = 0; i < charts.Count; i++)
        {
            (double w, double h) = BoundingBox(charts[i]);
            if (options.AllowRotation && w > h)
            {
                Rotate90(charts[i], w);
                (w, h) = (h, w);
            }

            size[i] = (w, h);
            totalBoxArea += w * h;
            totalUvArea += UvArea(charts[i]);
        }

        if (totalBoxArea <= 0)
        {
            return new PackResult(resolution, 0, charts.Count);
        }

        // Binary-search the largest scale (texels per UV unit) for which every padded box still fits.
        double scaleHi = Math.Sqrt((double)resolution * resolution / totalBoxArea);
        double scaleLo = 0;
        (int X, int Y)[]? best = null;
        double bestScale = 0;
        for (int iter = 0; iter < 48; iter++)
        {
            double scale = (scaleLo + scaleHi) / 2;
            (int X, int Y)[]? placements = TryPack(size, scale, resolution, gutter);
            if (placements is not null)
            {
                best = placements;
                bestScale = scale;
                scaleLo = scale;
            }
            else
            {
                scaleHi = scale;
            }
        }

        if (best is null)
        {
            throw new InvalidOperationException("Atlas packing failed: no feasible scale found.");
        }

        double half = gutter / 2.0;
        for (int i = 0; i < charts.Count; i++)
        {
            double offsetX = best[i].X + half;
            double offsetY = best[i].Y + half;
            Vector2[] uv = charts[i].Uv;
            for (int v = 0; v < uv.Length; v++)
            {
                double x = ((uv[v].X * bestScale) + offsetX) / resolution;
                double y = ((uv[v].Y * bestScale) + offsetY) / resolution;
                uv[v] = new Vector2(x, y);
            }
        }

        // Efficiency = fraction of the atlas covered by actual island (triangle) area, not box area.
        double efficiency = bestScale * bestScale * totalUvArea / ((double)resolution * resolution);
        return new PackResult(resolution, efficiency, charts.Count);
    }

    /// <summary>
    /// Packs at a fixed texel density (<see cref="UVBakeOptions.TexelsPerUnit"/>), spilling charts that do
    /// not fit the current 0–1 tile onto additional UDIM tiles (index 1001 + u + 10·v; UVs offset by the
    /// tile's (u,v)). Density is uniform across tiles; each tile keeps its own gutter (Documents/05 §2).
    /// </summary>
    public static PackResult PackUdim(IReadOnlyList<ChartUv> charts, UVBakeOptions options)
    {
        ArgumentNullException.ThrowIfNull(charts);
        ArgumentNullException.ThrowIfNull(options);

        double scale = options.TexelsPerUnit ?? 0;
        if (scale <= 0)
        {
            return Pack(charts, options); // no fixed density → fall back to the auto single-tile fit
        }

        int resolution = options.PowerOfTwo ? NextPowerOfTwo(options.Resolution) : Math.Max(1, options.Resolution);
        int gutter = Math.Max(0, options.GutterPixels);
        if (charts.Count == 0)
        {
            return new PackResult(resolution, 0, 0);
        }

        double totalUvArea = 0;
        var size = new (double W, double H)[charts.Count];
        for (int i = 0; i < charts.Count; i++)
        {
            (double w, double h) = BoundingBox(charts[i]);
            if (options.AllowRotation && w > h)
            {
                Rotate90(charts[i], w);
                (w, h) = (h, w);
            }

            size[i] = (w, h);
            totalUvArea += UvArea(charts[i]);
        }

        var order = new int[charts.Count];
        for (int i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (a, b) =>
        {
            double areaA = size[a].W * size[a].H, areaB = size[b].W * size[b].H;
            return areaA != areaB ? areaB.CompareTo(areaA) : a.CompareTo(b);
        });

        var skylines = new List<Skyline>();
        var placement = new (int Tile, int X, int Y)[charts.Count];
        foreach (int i in order)
        {
            int boxW = (int)Math.Ceiling(size[i].W * scale) + gutter;
            int boxH = (int)Math.Ceiling(size[i].H * scale) + gutter;
            if (boxW > resolution || boxH > resolution)
            {
                throw new InvalidOperationException(
                    "TexelsPerUnit is too high: a chart exceeds one atlas tile. Lower the density or raise the resolution.");
            }

            int tile = -1;
            (int X, int Y) spot = default;
            for (int t = 0; t < skylines.Count; t++)
            {
                (int X, int Y)? candidate = skylines[t].Place(boxW, boxH);
                if (candidate is not null)
                {
                    tile = t;
                    spot = candidate.Value;
                    break;
                }
            }

            if (tile < 0)
            {
                var skyline = new Skyline(resolution, resolution);
                spot = skyline.Place(boxW, boxH)
                    ?? throw new InvalidOperationException("Chart does not fit an empty tile.");
                skylines.Add(skyline);
                tile = skylines.Count - 1;
            }

            placement[i] = (tile, spot.X, spot.Y);
        }

        double half = gutter / 2.0;
        for (int i = 0; i < charts.Count; i++)
        {
            (int tile, int x, int y) = placement[i];
            int tileU = tile % 10;
            int tileV = tile / 10;
            Vector2[] uv = charts[i].Uv;
            for (int v = 0; v < uv.Length; v++)
            {
                double u = (((uv[v].X * scale) + x + half) / resolution) + tileU;
                double w = (((uv[v].Y * scale) + y + half) / resolution) + tileV;
                uv[v] = new Vector2(u, w);
            }
        }

        int tileCount = skylines.Count;
        double efficiency = scale * scale * totalUvArea / ((double)tileCount * resolution * resolution);
        return new PackResult(resolution, efficiency, charts.Count, tileCount);
    }

    private static (int X, int Y)[]? TryPack((double W, double H)[] size, double scale, int resolution, int gutter)
    {
        // Tallest boxes first (skyline packs better); deterministic tie-break by index.
        var order = new int[size.Length];
        for (int i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (a, b) =>
        {
            int ha = (int)Math.Ceiling(size[a].H * scale) + gutter;
            int hb = (int)Math.Ceiling(size[b].H * scale) + gutter;
            if (ha != hb)
            {
                return hb.CompareTo(ha);
            }

            int wa = (int)Math.Ceiling(size[a].W * scale) + gutter;
            int wb = (int)Math.Ceiling(size[b].W * scale) + gutter;
            return wa != wb ? wb.CompareTo(wa) : a.CompareTo(b);
        });

        var skyline = new Skyline(resolution, resolution);
        var placements = new (int X, int Y)[size.Length];
        foreach (int i in order)
        {
            int boxW = (int)Math.Ceiling(size[i].W * scale) + gutter;
            int boxH = (int)Math.Ceiling(size[i].H * scale) + gutter;
            if (boxW > resolution || boxH > resolution)
            {
                return null;
            }

            (int X, int Y)? placed = skyline.Place(boxW, boxH);
            if (placed is null)
            {
                return null;
            }

            placements[i] = placed.Value;
        }

        return placements;
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

    private static (double W, double H) BoundingBox(ChartUv chart)
    {
        double maxX = 0, maxY = 0;
        foreach (Vector2 uv in chart.Uv)
        {
            maxX = Math.Max(maxX, uv.X);
            maxY = Math.Max(maxY, uv.Y);
        }

        return (maxX, maxY);
    }

    private static void Rotate90(ChartUv chart, double width)
    {
        // (x,y) → (y, width − x): a proper 90° rotation (det +1) that keeps the min corner at the origin.
        Vector2[] uv = chart.Uv;
        for (int v = 0; v < uv.Length; v++)
        {
            uv[v] = new Vector2(uv[v].Y, width - uv[v].X);
        }
    }

    private static int NextPowerOfTwo(int value)
    {
        int result = 1;
        while (result < value)
        {
            result <<= 1;
        }

        return result;
    }
}
