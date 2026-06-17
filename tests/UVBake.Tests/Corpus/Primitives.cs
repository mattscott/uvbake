using UVBake.Core.Geometry;
using UVBake.Core.Meshes;

namespace UVBake.Tests.Corpus;

/// <summary>
/// Deterministic, license-free procedural meshes — the tier-A corpus (Documents/10 §A). They cover hard
/// edges (cube, beveled box), curvature (sphere), a developable body with caps (cylinder), and non-trivial
/// topology (torus, genus 1). Every primitive is welded (shared position indices) and closed, so the
/// half-edge build is manifold with no boundary. Winding is consistent and outward.
/// </summary>
public static class Primitives
{
    /// <summary>Unit cube centered at the origin: 8 vertices, 6 quad faces. All 12 edges are hard (90°).</summary>
    public static Mesh Cube(double half = 1.0)
    {
        var positions = new[]
        {
            new Vector3(-half, -half, -half), // 0
            new Vector3(half, -half, -half),  // 1
            new Vector3(half, half, -half),   // 2
            new Vector3(-half, half, -half),  // 3
            new Vector3(-half, -half, half),  // 4
            new Vector3(half, -half, half),   // 5
            new Vector3(half, half, half),    // 6
            new Vector3(-half, half, half),   // 7
        };

        var faces = new List<int[]>
        {
            new[] { 1, 2, 6, 5 }, // +X
            new[] { 0, 4, 7, 3 }, // -X
            new[] { 3, 7, 6, 2 }, // +Y
            new[] { 0, 1, 5, 4 }, // -Y
            new[] { 4, 5, 6, 7 }, // +Z
            new[] { 0, 3, 2, 1 }, // -Z
        };

        return FromPolygons(positions, faces);
    }

    /// <summary>
    /// A beveled box: a chamfered-square cross-section extruded along Z and capped. The four axis faces
    /// meet the four 45° chamfer faces along edges that deviate exactly 45°, so they are a clean
    /// threshold test; the side↔cap edges are 90°.
    /// </summary>
    public static Mesh BeveledBox(double half = 1.0, double chamfer = 0.3, double depth = 1.0)
    {
        // Chamfered-square cross-section, CCW in XY (8 vertices).
        var section = new (double X, double Y)[]
        {
            (half, -half + chamfer),
            (half, half - chamfer),
            (half - chamfer, half),
            (-half + chamfer, half),
            (-half, half - chamfer),
            (-half, -half + chamfer),
            (-half + chamfer, -half),
            (half - chamfer, -half),
        };
        int n = section.Length;

        var positions = new List<Vector3>(n * 2);
        for (int j = 0; j < n; j++)
        {
            positions.Add(new Vector3(section[j].X, section[j].Y, -depth)); // bottom ring: 0..n-1
        }

        for (int j = 0; j < n; j++)
        {
            positions.Add(new Vector3(section[j].X, section[j].Y, depth)); // top ring: n..2n-1
        }

        var faces = new List<int[]>();
        for (int j = 0; j < n; j++)
        {
            int jn = (j + 1) % n;
            faces.Add([j, jn, jn + n, j + n]); // side quad, outward radial
        }

        faces.Add(Reversed(Ring(0, n)));   // bottom cap (-Z)
        faces.Add(Ring(n, n));             // top cap (+Z)
        return FromPolygons(positions, faces);
    }

    /// <summary>UV sphere (latitude/longitude). Closed and smooth — no hard edges; needs a cut-to-disk later.</summary>
    public static Mesh Sphere(int slices = 24, int stacks = 16, double radius = 1.0)
    {
        var positions = new List<Vector3> { new(0, radius, 0) }; // north pole = 0
        for (int i = 1; i < stacks; i++)
        {
            double phi = Math.PI * i / stacks;
            double y = Math.Cos(phi) * radius;
            double ringRadius = Math.Sin(phi) * radius;
            for (int j = 0; j < slices; j++)
            {
                double theta = 2 * Math.PI * j / slices;
                positions.Add(new Vector3(ringRadius * Math.Cos(theta), y, ringRadius * Math.Sin(theta)));
            }
        }

        int south = positions.Count;
        positions.Add(new Vector3(0, -radius, 0));

        int Idx(int ring, int j) => 1 + ((ring - 1) * slices) + (((j % slices) + slices) % slices);

        var faces = new List<int[]>();
        for (int j = 0; j < slices; j++)
        {
            faces.Add([0, Idx(1, j + 1), Idx(1, j)]); // top cap
        }

        for (int i = 1; i < stacks - 1; i++)
        {
            for (int j = 0; j < slices; j++)
            {
                faces.Add([Idx(i, j), Idx(i, j + 1), Idx(i + 1, j + 1), Idx(i + 1, j)]);
            }
        }

        for (int j = 0; j < slices; j++)
        {
            faces.Add([south, Idx(stacks - 1, j), Idx(stacks - 1, j + 1)]); // bottom cap
        }

        return FromPolygons(positions, faces);
    }

    /// <summary>Capped cylinder: a developable side (low dihedral) with two flat caps (90° rim edges).</summary>
    public static Mesh Cylinder(int slices = 32, double radius = 1.0, double halfHeight = 1.0)
    {
        var positions = new List<Vector3>(slices * 2);
        for (int j = 0; j < slices; j++)
        {
            double theta = 2 * Math.PI * j / slices;
            positions.Add(new Vector3(radius * Math.Cos(theta), -halfHeight, radius * Math.Sin(theta)));
        }

        for (int j = 0; j < slices; j++)
        {
            double theta = 2 * Math.PI * j / slices;
            positions.Add(new Vector3(radius * Math.Cos(theta), halfHeight, radius * Math.Sin(theta)));
        }

        var faces = new List<int[]>();
        for (int j = 0; j < slices; j++)
        {
            int jn = (j + 1) % slices;
            faces.Add([j, j + slices, jn + slices, jn]); // side quad, outward radial
        }

        faces.Add(Ring(0, slices));                // bottom cap (-Y)
        faces.Add(Reversed(Ring(slices, slices))); // top cap (+Y)
        return FromPolygons(positions, faces);
    }

    /// <summary>Torus (genus 1): closed, smooth, exercises handle cuts in cut-to-disk later.</summary>
    public static Mesh Torus(int major = 32, int minor = 16, double ringRadius = 1.0, double tubeRadius = 0.35)
    {
        var positions = new List<Vector3>(major * minor);
        for (int i = 0; i < major; i++)
        {
            double u = 2 * Math.PI * i / major;
            for (int j = 0; j < minor; j++)
            {
                double v = 2 * Math.PI * j / minor;
                double ring = ringRadius + (tubeRadius * Math.Cos(v));
                positions.Add(new Vector3(ring * Math.Cos(u), tubeRadius * Math.Sin(v), ring * Math.Sin(u)));
            }
        }

        int Idx(int i, int j) => ((i % major) * minor) + (j % minor);

        var faces = new List<int[]>();
        for (int i = 0; i < major; i++)
        {
            for (int j = 0; j < minor; j++)
            {
                faces.Add([Idx(i, j), Idx(i, j + 1), Idx(i + 1, j + 1), Idx(i + 1, j)]); // outward tube normal
            }
        }

        return FromPolygons(positions, faces);
    }

    /// <summary>The named procedural corpus meshes, keyed by the file stem used under tests/corpus/.</summary>
    public static IReadOnlyDictionary<string, Mesh> All() => new Dictionary<string, Mesh>
    {
        ["cube"] = Cube(),
        ["beveled-box"] = BeveledBox(),
        ["sphere"] = Sphere(),
        ["cylinder"] = Cylinder(),
        ["torus"] = Torus(),
    };

    private static int[] Ring(int start, int count)
    {
        var ring = new int[count];
        for (int i = 0; i < count; i++)
        {
            ring[i] = start + i;
        }

        return ring;
    }

    private static int[] Reversed(int[] values)
    {
        var copy = (int[])values.Clone();
        Array.Reverse(copy);
        return copy;
    }

    private static Mesh FromPolygons(IReadOnlyList<Vector3> positions, List<int[]> faces)
    {
        var meshFaces = new List<Face>(faces.Count);
        foreach (int[] face in faces)
        {
            var corners = new Corner[face.Length];
            for (int i = 0; i < face.Length; i++)
            {
                corners[i] = new Corner(face[i], Corner.None, Corner.None);
            }

            meshFaces.Add(new Face(corners));
        }

        return new Mesh(positions, Array.Empty<Vector2>(), Array.Empty<Vector3>(), meshFaces);
    }
}
