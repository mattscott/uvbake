using UVBake.Core.Geometry;
using UVBake.Core.Parameterization;

namespace UVBake.Core.Validation;

/// <summary>
/// Inter-chart overlap test — the injectivity gate at packing (Documents/07 §1). Broad-phase: bucket UV
/// triangles into a uniform grid by their AABB. Narrow-phase: exact 2D triangle–triangle overlap (SAT)
/// for surviving pairs from <b>different</b> charts (same-chart adjacency shares edges legitimately, and
/// intra-chart injectivity is guaranteed by the parameterizer). Touching at the gutter is not an overlap
/// (a small epsilon), so a correctly gutter-packed atlas reports zero.
/// </summary>
public static class OverlapValidator
{
    private const int GridResolution = 64;
    private const double Epsilon = 1e-9;

    /// <summary>Counts distinct pairs of triangles from different charts whose UV images overlap.</summary>
    public static int CountInterChartOverlaps(IReadOnlyList<ChartUv> charts)
    {
        ArgumentNullException.ThrowIfNull(charts);

        // Flatten all chart triangles into UV triangles tagged by chart.
        var triChart = new List<int>();
        var triA = new List<Vector2>();
        var triB = new List<Vector2>();
        var triC = new List<Vector2>();
        for (int c = 0; c < charts.Count; c++)
        {
            ChartUv chart = charts[c];
            int[] tris = chart.Triangles;
            for (int t = 0; t < tris.Length; t += 3)
            {
                triChart.Add(c);
                triA.Add(chart.Uv[tris[t]]);
                triB.Add(chart.Uv[tris[t + 1]]);
                triC.Add(chart.Uv[tris[t + 2]]);
            }
        }

        // Cells are not clamped, so UDIM tiles (UVs > 1) land in distinct cells and never falsely overlap.
        var grid = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < triChart.Count; i++)
        {
            (int x0, int y0, int x1, int y1) = CellRange(triA[i], triB[i], triC[i]);
            for (int gy = y0; gy <= y1; gy++)
            {
                for (int gx = x0; gx <= x1; gx++)
                {
                    if (!grid.TryGetValue((gx, gy), out List<int>? bucket))
                    {
                        bucket = [];
                        grid[(gx, gy)] = bucket;
                    }

                    bucket.Add(i);
                }
            }
        }

        var overlapping = new HashSet<(int, int)>();
        foreach (List<int> bucket in grid.Values)
        {
            for (int a = 0; a < bucket.Count; a++)
            {
                for (int b = a + 1; b < bucket.Count; b++)
                {
                    int i = bucket[a], j = bucket[b];
                    if (triChart[i] == triChart[j])
                    {
                        continue; // same chart: adjacency, not overlap
                    }

                    (int lo, int hi) = i < j ? (i, j) : (j, i);
                    if (overlapping.Contains((lo, hi)))
                    {
                        continue;
                    }

                    if (TrianglesOverlap(triA[i], triB[i], triC[i], triA[j], triB[j], triC[j]))
                    {
                        overlapping.Add((lo, hi));
                    }
                }
            }
        }

        return overlapping.Count;
    }

    private static (int X0, int Y0, int X1, int Y1) CellRange(Vector2 a, Vector2 b, Vector2 c)
    {
        double minX = Math.Min(a.X, Math.Min(b.X, c.X));
        double minY = Math.Min(a.Y, Math.Min(b.Y, c.Y));
        double maxX = Math.Max(a.X, Math.Max(b.X, c.X));
        double maxY = Math.Max(a.Y, Math.Max(b.Y, c.Y));
        return (Cell(minX), Cell(minY), Cell(maxX), Cell(maxY));
    }

    private static int Cell(double coordinate) => (int)Math.Floor(coordinate * GridResolution);

    /// <summary>2D triangle–triangle overlap by the separating-axis theorem (no separating edge ⇒ overlap).</summary>
    private static bool TrianglesOverlap(Vector2 a0, Vector2 a1, Vector2 a2, Vector2 b0, Vector2 b1, Vector2 b2)
    {
        return !HasSeparatingEdge(a0, a1, a2, b0, b1, b2) && !HasSeparatingEdge(b0, b1, b2, a0, a1, a2);
    }

    private static bool HasSeparatingEdge(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 q0, Vector2 q1, Vector2 q2)
    {
        return Separates(p0, p1, p0, p1, p2, q0, q1, q2)
            || Separates(p1, p2, p0, p1, p2, q0, q1, q2)
            || Separates(p2, p0, p0, p1, p2, q0, q1, q2);
    }

    private static bool Separates(
        Vector2 edgeStart, Vector2 edgeEnd,
        Vector2 a0, Vector2 a1, Vector2 a2,
        Vector2 b0, Vector2 b1, Vector2 b2)
    {
        var axis = new Vector2(-(edgeEnd.Y - edgeStart.Y), edgeEnd.X - edgeStart.X);
        Project(axis, a0, a1, a2, out double minA, out double maxA);
        Project(axis, b0, b1, b2, out double minB, out double maxB);
        return maxA < minB - Epsilon || maxB < minA - Epsilon;
    }

    private static void Project(Vector2 axis, Vector2 v0, Vector2 v1, Vector2 v2, out double min, out double max)
    {
        double d0 = axis.Dot(v0), d1 = axis.Dot(v1), d2 = axis.Dot(v2);
        min = Math.Min(d0, Math.Min(d1, d2));
        max = Math.Max(d0, Math.Max(d1, d2));
    }
}
