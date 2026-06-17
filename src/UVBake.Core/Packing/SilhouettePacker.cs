using UVBake.Core.Geometry;
using UVBake.Core.Parameterization;

namespace UVBake.Core.Packing;

/// <summary>
/// Silhouette ("Tetris") packing (Documents/05 §1.2): packs each chart's actual rasterized shape rather
/// than its bounding box, so concave gaps around irregular islands get reused. Implemented as a
/// silhouette-aware skyline — each chart's per-column <b>bottom contour</b> rests on the atlas's upper
/// envelope and the envelope is raised by its <b>top contour</b>. Resting on the envelope keeps the chart
/// entirely above all placed charts, so the result is overlap-free by construction; the silhouette is
/// dilated by the padding <b>before</b> placement, so the gutter is reserved by construction too. This
/// strictly generalizes the bounding-box skyline (a box is the flat-contour case), so it is never looser.
/// </summary>
public static class SilhouettePacker
{
    public static PackResult Pack(IReadOnlyList<ChartUv> charts, UVBakeOptions options)
    {
        ArgumentNullException.ThrowIfNull(charts);
        ArgumentNullException.ThrowIfNull(options);

        int resolution = options.PowerOfTwo ? NextPowerOfTwo(options.Resolution) : Math.Max(1, options.Resolution);
        int pad = Math.Max(1, options.GutterPixels / 2); // dilation radius; two skirts meet ⇒ gutter ≈ 2·pad
        if (charts.Count == 0)
        {
            return new PackResult(resolution, 0, 0);
        }

        double totalUvArea = 0;
        for (int i = 0; i < charts.Count; i++)
        {
            (double w, double h) = Extent(charts[i]);
            if (options.AllowRotation && w > h)
            {
                Rotate90(charts[i], w); // portrait orientation packs better, parity with the bbox packer
            }

            totalUvArea += SurfaceArea(charts[i]); // == UV area after normalization
        }

        if (totalUvArea <= 0)
        {
            return new PackResult(resolution, 0, charts.Count);
        }

        double scaleHi = Math.Sqrt((double)resolution * resolution / totalUvArea);
        double scaleLo = 0;
        (int X, int Y)[]? best = null;
        double bestScale = 0;
        for (int iter = 0; iter < 40; iter++)
        {
            double scale = (scaleLo + scaleHi) / 2;
            (int X, int Y)[]? placements = TryPack(charts, scale, resolution, pad);
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
            throw new InvalidOperationException("Silhouette packing failed: no feasible scale found.");
        }

        for (int i = 0; i < charts.Count; i++)
        {
            // Content origin sits at (X+pad, Y+pad) within the dilated bitmap placed at (X,Y).
            double offsetX = best[i].X + pad;
            double offsetY = best[i].Y + pad;
            Vector2[] uv = charts[i].Uv;
            for (int v = 0; v < uv.Length; v++)
            {
                double x = ((uv[v].X * bestScale) + offsetX) / resolution;
                double y = ((uv[v].Y * bestScale) + offsetY) / resolution;
                uv[v] = new Vector2(x, y);
            }
        }

        double efficiency = bestScale * bestScale * totalUvArea / ((double)resolution * resolution);
        return new PackResult(resolution, efficiency, charts.Count);
    }

    private static (int X, int Y)[]? TryPack(IReadOnlyList<ChartUv> charts, double scale, int grid, int pad)
    {
        var rasters = new ChartRaster[charts.Count];
        for (int i = 0; i < charts.Count; i++)
        {
            rasters[i] = ChartRaster.Build(charts[i], scale, pad);
            if (rasters[i].Width > grid || rasters[i].Height > grid)
            {
                return null;
            }
        }

        // Tallest first (skyline packs better); deterministic tie-break.
        var order = new int[charts.Count];
        for (int i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (a, b) =>
            rasters[a].Height != rasters[b].Height ? rasters[b].Height.CompareTo(rasters[a].Height)
            : rasters[a].Width != rasters[b].Width ? rasters[b].Width.CompareTo(rasters[a].Width)
            : a.CompareTo(b));

        var skyline = new int[grid];
        var placements = new (int X, int Y)[charts.Count];
        foreach (int i in order)
        {
            (int X, int Y)? placed = Place(rasters[i], skyline, grid);
            if (placed is null)
            {
                return null;
            }

            placements[i] = placed.Value;
            Commit(rasters[i], skyline, placed.Value.X, placed.Value.Y);
        }

        return placements;
    }

    private static (int X, int Y)? Place(ChartRaster raster, int[] skyline, int grid)
    {
        int bestX = -1, bestScore = int.MaxValue, bestY = 0;
        for (int x = 0; x + raster.Width <= grid; x++)
        {
            int y = 0;
            for (int col = 0; col < raster.Width; col++)
            {
                if (raster.Occupied[col])
                {
                    y = Math.Max(y, skyline[x + col] - raster.Bottom[col]);
                }
            }

            int topRow = y + raster.MaxTop;
            if (topRow >= grid)
            {
                continue;
            }

            if (topRow < bestScore || (topRow == bestScore && x < bestX))
            {
                bestScore = topRow;
                bestX = x;
                bestY = y;
            }
        }

        return bestX < 0 ? null : (bestX, bestY);
    }

    private static void Commit(ChartRaster raster, int[] skyline, int x, int y)
    {
        for (int col = 0; col < raster.Width; col++)
        {
            if (raster.Occupied[col])
            {
                skyline[x + col] = y + raster.Top[col] + 1;
            }
        }
    }

    private static (double W, double H) Extent(ChartUv chart)
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
        // (x,y) → (y, width − x): a proper 90° rotation (det +1) keeping the min corner at the origin.
        Vector2[] uv = chart.Uv;
        for (int v = 0; v < uv.Length; v++)
        {
            uv[v] = new Vector2(uv[v].Y, width - uv[v].X);
        }
    }

    private static double SurfaceArea(ChartUv chart)
    {
        double area = 0;
        int[] t = chart.Triangles;
        for (int i = 0; i < t.Length; i += 3)
        {
            area += 0.5 * Math.Abs((chart.Uv[t[i + 1]] - chart.Uv[t[i]]).Cross(chart.Uv[t[i + 2]] - chart.Uv[t[i]]));
        }

        return area;
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

    /// <summary>
    /// A chart's dilated silhouette as per-column contours. Built by filling each triangle's cell
    /// bounding box (a safe over-approximation of its shape), then dilating the contours by the padding.
    /// </summary>
    private sealed class ChartRaster
    {
        private ChartRaster(int width, int height, int[] bottom, int[] top, bool[] occupied, int maxTop)
        {
            Width = width;
            Height = height;
            Bottom = bottom;
            Top = top;
            Occupied = occupied;
            MaxTop = maxTop;
        }

        public int Width { get; }

        public int Height { get; }

        public int[] Bottom { get; }

        public int[] Top { get; }

        public bool[] Occupied { get; }

        public int MaxTop { get; }

        public static ChartRaster Build(ChartUv chart, double scale, int pad)
        {
            // Raw per-content-column contours from each triangle's scaled cell bounding box.
            int contentWidth = 1;
            int contentHeight = 1;
            (double W, double H) extent = Extent(chart);
            contentWidth = Math.Max(1, (int)Math.Ceiling(extent.W * scale) + 1);
            contentHeight = Math.Max(1, (int)Math.Ceiling(extent.H * scale) + 1);

            var bottom0 = new int[contentWidth];
            var top0 = new int[contentWidth];
            var occ0 = new bool[contentWidth];
            Array.Fill(bottom0, int.MaxValue);
            Array.Fill(top0, int.MinValue);

            int[] tris = chart.Triangles;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector2 a = chart.Uv[tris[t]] * scale;
                Vector2 b = chart.Uv[tris[t + 1]] * scale;
                Vector2 c = chart.Uv[tris[t + 2]] * scale;
                int x0 = Math.Clamp((int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X))), 0, contentWidth - 1);
                int x1 = Math.Clamp((int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))), 1, contentWidth);
                int y0 = Math.Max(0, (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))));
                int y1 = Math.Max(y0 + 1, (int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))));
                for (int col = x0; col < x1; col++)
                {
                    occ0[col] = true;
                    bottom0[col] = Math.Min(bottom0[col], y0);
                    top0[col] = Math.Max(top0[col], y1 - 1);
                }
            }

            // Dilate contours by `pad`: bitmap column j maps to content columns [j-2pad, j]; rows shift by
            // pad and grow by pad on top (a content cell at row r occupies bitmap rows [r, r+2pad]).
            int width = contentWidth + (2 * pad);
            int height = contentHeight + (2 * pad);
            var bottom = new int[width];
            var top = new int[width];
            var occupied = new bool[width];
            int maxTop = 0;
            for (int j = 0; j < width; j++)
            {
                int lo = Math.Max(0, j - (2 * pad));
                int hi = Math.Min(contentWidth - 1, j);
                int minBottom = int.MaxValue;
                int maxTopHere = int.MinValue;
                for (int c = lo; c <= hi; c++)
                {
                    if (occ0[c])
                    {
                        minBottom = Math.Min(minBottom, bottom0[c]);
                        maxTopHere = Math.Max(maxTopHere, top0[c]);
                    }
                }

                if (minBottom != int.MaxValue)
                {
                    occupied[j] = true;
                    bottom[j] = minBottom;             // shift +pad and dilate −pad cancel
                    top[j] = maxTopHere + (2 * pad);   // shift +pad and dilate +pad
                    maxTop = Math.Max(maxTop, top[j]);
                }
            }

            return new ChartRaster(width, height, bottom, top, occupied, maxTop);
        }
    }
}
