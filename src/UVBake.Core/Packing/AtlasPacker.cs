using UVBake.Core.Geometry;
using UVBake.Core.Parameterization;

namespace UVBake.Core.Packing;

/// <summary>Result of packing: the atlas resolution used and the fraction of texels the islands occupy.</summary>
public sealed record PackResult(int Resolution, double Efficiency, int ChartCount);

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
        double totalArea = 0;
        for (int i = 0; i < charts.Count; i++)
        {
            (double w, double h) = BoundingBox(charts[i]);
            if (options.AllowRotation && w > h)
            {
                Rotate90(charts[i], w);
                (w, h) = (h, w);
            }

            size[i] = (w, h);
            totalArea += w * h;
        }

        if (totalArea <= 0)
        {
            return new PackResult(resolution, 0, charts.Count);
        }

        // Binary-search the largest scale (texels per UV unit) for which every padded box still fits.
        double scaleHi = Math.Sqrt((double)resolution * resolution / totalArea);
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

        double efficiency = bestScale * bestScale * totalArea / ((double)resolution * resolution);
        return new PackResult(resolution, efficiency, charts.Count);
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
