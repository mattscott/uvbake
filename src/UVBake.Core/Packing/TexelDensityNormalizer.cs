using UVBake.Core.Geometry;
using UVBake.Core.Parameterization;

namespace UVBake.Core.Packing;

/// <summary>
/// Texel-density normalization (Documents/05 §3): scale each chart so its UV area equals its 3D surface
/// area (scale factor √(3D area / UV area)). After this every chart shares the same texels-per-unit, so a
/// single global pack scale gives uniform detail everywhere ("normalized islands"). Each chart's UVs are
/// also shifted so the chart's bounding box starts at the origin, ready for packing.
/// </summary>
public static class TexelDensityNormalizer
{
    /// <summary>Normalizes every chart in place.</summary>
    public static void Normalize(IReadOnlyList<ChartUv> charts)
    {
        ArgumentNullException.ThrowIfNull(charts);
        foreach (ChartUv chart in charts)
        {
            Normalize(chart);
        }
    }

    /// <summary>Normalizes one chart in place.</summary>
    public static void Normalize(ChartUv chart)
    {
        ArgumentNullException.ThrowIfNull(chart);

        double area3D = 0;
        double areaUv = 0;
        int[] tris = chart.Triangles;
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3 p0 = chart.Position3D[tris[t]];
            Vector3 p1 = chart.Position3D[tris[t + 1]];
            Vector3 p2 = chart.Position3D[tris[t + 2]];
            area3D += 0.5 * (p1 - p0).Cross(p2 - p0).Length;

            Vector2 q0 = chart.Uv[tris[t]];
            Vector2 q1 = chart.Uv[tris[t + 1]];
            Vector2 q2 = chart.Uv[tris[t + 2]];
            areaUv += 0.5 * (q1 - q0).Cross(q2 - q0);
        }

        areaUv = Math.Abs(areaUv);
        double scale = areaUv > 0 ? Math.Sqrt(area3D / areaUv) : 1.0;

        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        for (int v = 0; v < chart.Uv.Length; v++)
        {
            Vector2 scaled = chart.Uv[v] * scale;
            chart.Uv[v] = scaled;
            minX = Math.Min(minX, scaled.X);
            minY = Math.Min(minY, scaled.Y);
        }

        var origin = new Vector2(minX, minY);
        for (int v = 0; v < chart.Uv.Length; v++)
        {
            chart.Uv[v] -= origin;
        }
    }
}
