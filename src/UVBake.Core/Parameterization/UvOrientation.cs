using UVBake.Core.Geometry;

namespace UVBake.Core.Parameterization;

/// <summary>
/// Orientation helpers. A flattener may emit a consistently mirrored layout (det J &lt; 0 everywhere);
/// reflecting it makes det J &gt; 0 so the result matches the surface orientation, which tangent-space
/// baking needs. (A consistently mirrored map is still injective, just inverted.)
/// </summary>
internal static class UvOrientation
{
    public static double TotalSignedArea(int[] triangles, Vector2[] uv)
    {
        double sum = 0;
        for (int t = 0; t < triangles.Length; t += 3)
        {
            Vector2 a = uv[triangles[t]];
            Vector2 b = uv[triangles[t + 1]];
            Vector2 c = uv[triangles[t + 2]];
            sum += (b - a).Cross(c - a);
        }

        return sum;
    }

    public static void MakePositive(int[] triangles, Vector2[] uv)
    {
        if (TotalSignedArea(triangles, uv) < 0)
        {
            for (int v = 0; v < uv.Length; v++)
            {
                uv[v] = new Vector2(-uv[v].X, uv[v].Y);
            }
        }
    }
}
