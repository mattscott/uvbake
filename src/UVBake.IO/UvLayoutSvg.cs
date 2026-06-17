using System.Globalization;
using System.Text;
using UVBake.Core.Geometry;
using UVBake.Core.Meshes;

namespace UVBake.IO;

/// <summary>
/// Renders a packed UV atlas as an SVG — a dependency-free QC/sample artifact (Documents/07 §4). Each
/// island is filled with its own colour; islands are recovered by union-find over shared texcoord indices
/// (charts pack to disjoint texcoord blocks, so a connected component = one chart). UDIM tiles render in
/// place (the UV extent may exceed the unit square).
/// </summary>
public static class UvLayoutSvg
{
    /// <summary>Writes an SVG layout of <paramref name="mesh"/>'s UVs to <paramref name="path"/>.</summary>
    public static void Write(Mesh mesh, string path, int pixelSize = 1024)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(path);
        File.WriteAllText(path, ToSvg(mesh, pixelSize));
    }

    /// <summary>Produces the SVG layout of <paramref name="mesh"/>'s UVs as a string.</summary>
    public static string ToSvg(Mesh mesh, int pixelSize = 1024)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        int[] islandColor = ColorByIsland(mesh, out int islandCount);

        double maxDim = 1.0;
        foreach (Vector2 uv in mesh.TexCoords)
        {
            maxDim = Math.Max(maxDim, Math.Max(uv.X, uv.Y));
        }

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{pixelSize}\" height=\"{pixelSize}\" ");
        sb.Append(CultureInfo.InvariantCulture, $"viewBox=\"0 0 {Fmt(maxDim)} {Fmt(maxDim)}\">\n");
        sb.Append(CultureInfo.InvariantCulture,
            $"<rect width=\"{Fmt(maxDim)}\" height=\"{Fmt(maxDim)}\" fill=\"#111\"/>\n");

        double stroke = maxDim / pixelSize; // ~1px
        foreach (Face face in mesh.Faces)
        {
            if (face.Count < 3 || face[0].TexCoord < 0)
            {
                continue;
            }

            string color = Palette(islandColor[face[0].TexCoord], islandCount);
            sb.Append("<polygon points=\"");
            for (int i = 0; i < face.Count; i++)
            {
                Vector2 uv = mesh.TexCoords[face[i].TexCoord];
                if (i > 0)
                {
                    sb.Append(' ');
                }

                // Flip Y so V points up (SVG is y-down).
                sb.Append(CultureInfo.InvariantCulture, $"{Fmt(uv.X)},{Fmt(maxDim - uv.Y)}");
            }

            sb.Append(CultureInfo.InvariantCulture,
                $"\" fill=\"{color}\" fill-opacity=\"0.85\" stroke=\"#222\" stroke-width=\"{Fmt(stroke)}\"/>\n");
        }

        sb.Append("</svg>\n");
        return sb.ToString();
    }

    private static int[] ColorByIsland(Mesh mesh, out int islandCount)
    {
        int n = mesh.TexCoords.Count;
        var parent = new int[n];
        for (int i = 0; i < n; i++)
        {
            parent[i] = i;
        }

        foreach (Face face in mesh.Faces)
        {
            for (int i = 1; i < face.Count; i++)
            {
                if (face[0].TexCoord >= 0 && face[i].TexCoord >= 0)
                {
                    Union(parent, face[0].TexCoord, face[i].TexCoord);
                }
            }
        }

        // Map each component root to a compact 0..k-1 colour index in first-seen order (deterministic).
        var rootToColor = new Dictionary<int, int>();
        var color = new int[n];
        for (int i = 0; i < n; i++)
        {
            int root = Find(parent, i);
            if (!rootToColor.TryGetValue(root, out int c))
            {
                c = rootToColor.Count;
                rootToColor[root] = c;
            }

            color[i] = c;
        }

        islandCount = rootToColor.Count;
        return color;
    }

    private static int Find(int[] parent, int x)
    {
        while (parent[x] != x)
        {
            parent[x] = parent[parent[x]];
            x = parent[x];
        }

        return x;
    }

    private static void Union(int[] parent, int a, int b)
    {
        int ra = Find(parent, a), rb = Find(parent, b);
        if (ra != rb)
        {
            parent[Math.Max(ra, rb)] = Math.Min(ra, rb);
        }
    }

    private static string Palette(int index, int count)
    {
        // Golden-ratio hue spacing for well-separated colours; deterministic.
        double hue = (index * 0.6180339887) % 1.0;
        (double r, double g, double b) = HsvToRgb(hue, 0.55, 0.95);
        return $"#{(int)(r * 255):X2}{(int)(g * 255):X2}{(int)(b * 255):X2}";
    }

    private static (double R, double G, double B) HsvToRgb(double h, double s, double v)
    {
        double i = Math.Floor(h * 6);
        double f = (h * 6) - i;
        double p = v * (1 - s);
        double q = v * (1 - (f * s));
        double t = v * (1 - ((1 - f) * s));
        return ((int)i % 6) switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
    }

    private static string Fmt(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
