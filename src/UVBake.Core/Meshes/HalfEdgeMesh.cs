using UVBake.Core.Geometry;

namespace UVBake.Core.Meshes;

/// <summary>
/// A triangle-indexed half-edge mesh: the working connectivity structure for segmentation,
/// parameterization, and packing. Triangle <c>t</c> owns half-edges <c>3t, 3t+1, 3t+2</c>; half-edge
/// <c>3t+i</c> runs from corner <c>i</c> to corner <c>(i+1)%3</c>. Storage is index-based SoA. The build
/// is non-manifold tolerant: an edge with only one incident triangle is a boundary (twin = -1), and an
/// edge with more than two is paired greedily with the extras left as boundary and counted.
/// <para>
/// Per-face normal and area, and per-edge dihedral angle, are precomputed. Hard-edge flags start clear
/// and are populated by hard-edge detection (Segmentation).
/// </para>
/// </summary>
public sealed class HalfEdgeMesh
{
    private readonly int[] _origin;       // position index at the start of each half-edge
    private readonly int[] _twin;         // opposite half-edge, or -1 on a boundary
    private readonly Vector3[] _faceNormal; // per triangle, unit length (zero for a degenerate triangle)
    private readonly double[] _faceArea;  // per triangle, 3D area
    private readonly double[] _dihedral;  // per half-edge, angle between adjacent faces (rad); NaN on a boundary
    private readonly bool[] _isHard;      // per half-edge hard-edge flag

    private HalfEdgeMesh(
        IReadOnlyList<Vector3> positions,
        int[] origin,
        int[] twin,
        Vector3[] faceNormal,
        double[] faceArea,
        double[] dihedral,
        int boundaryEdgeCount,
        int nonManifoldEdgeCount)
    {
        Positions = positions;
        _origin = origin;
        _twin = twin;
        _faceNormal = faceNormal;
        _faceArea = faceArea;
        _dihedral = dihedral;
        _isHard = new bool[origin.Length];
        BoundaryEdgeCount = boundaryEdgeCount;
        NonManifoldEdgeCount = nonManifoldEdgeCount;
    }

    /// <summary>Vertex positions (shared with the source mesh).</summary>
    public IReadOnlyList<Vector3> Positions { get; }

    /// <summary>Number of triangles.</summary>
    public int TriangleCount => _faceNormal.Length;

    /// <summary>Number of half-edges (<c>3 × TriangleCount</c>).</summary>
    public int HalfEdgeCount => _origin.Length;

    /// <summary>Number of boundary (unpaired) half-edges.</summary>
    public int BoundaryEdgeCount { get; }

    /// <summary>Number of edges that could not be cleanly paired (non-manifold input).</summary>
    public int NonManifoldEdgeCount { get; }

    /// <summary>The triangle that owns half-edge <paramref name="h"/>.</summary>
    public int TriangleOf(int h) => h / 3;

    /// <summary>The next half-edge around the same triangle (counter-clockwise).</summary>
    public int Next(int h) => (h - (h % 3)) + ((h + 1) % 3);

    /// <summary>The previous half-edge around the same triangle.</summary>
    public int Prev(int h) => (h - (h % 3)) + ((h + 2) % 3);

    /// <summary>The opposite half-edge, or -1 if <paramref name="h"/> is on a boundary.</summary>
    public int Twin(int h) => _twin[h];

    /// <summary>The position index at the start of half-edge <paramref name="h"/>.</summary>
    public int Origin(int h) => _origin[h];

    /// <summary>The position index at the end of half-edge <paramref name="h"/>.</summary>
    public int Destination(int h) => _origin[Next(h)];

    /// <summary>True if <paramref name="h"/> has no twin (mesh boundary).</summary>
    public bool IsBoundary(int h) => _twin[h] < 0;

    /// <summary>The undirected edge key (by position index) of half-edge <paramref name="h"/>.</summary>
    public EdgeKey EdgeKeyOf(int h) => new(_origin[h], Destination(h));

    /// <summary>Unit normal of triangle <paramref name="t"/> (zero vector if the triangle is degenerate).</summary>
    public Vector3 FaceNormal(int t) => _faceNormal[t];

    /// <summary>3D area of triangle <paramref name="t"/>.</summary>
    public double FaceArea(int t) => _faceArea[t];

    /// <summary>
    /// Angle (radians, in <c>[0, π]</c>) between the two faces sharing this edge — 0 when coplanar,
    /// π/2 at a right angle. <see cref="double.NaN"/> on a boundary edge.
    /// </summary>
    public double Dihedral(int h) => _dihedral[h];

    /// <summary>Whether this edge is flagged as a hard edge / forced seam.</summary>
    public bool IsHard(int h) => _isHard[h];

    /// <summary>Flags this edge (and its twin) as hard.</summary>
    public void MarkHard(int h)
    {
        _isHard[h] = true;
        int twin = _twin[h];
        if (twin >= 0)
        {
            _isHard[twin] = true;
        }
    }

    /// <summary>
    /// Enumerates one canonical half-edge per interior (twinned) edge — the lower of the twin pair — so
    /// callers visit each undirected interior edge exactly once.
    /// </summary>
    public IEnumerable<int> InteriorEdges()
    {
        for (int h = 0; h < _twin.Length; h++)
        {
            int twin = _twin[h];
            if (twin > h)
            {
                yield return h;
            }
        }
    }

    /// <summary>The undirected edge keys currently flagged hard (each yielded once).</summary>
    public IEnumerable<EdgeKey> HardEdgeKeys()
    {
        for (int h = 0; h < _twin.Length; h++)
        {
            int twin = _twin[h];
            // Interior edge: emit from the canonical (lower) half-edge. Boundary edge: emit if flagged.
            bool canonical = twin < 0 || h < twin;
            if (canonical && _isHard[h])
            {
                yield return EdgeKeyOf(h);
            }
        }
    }

    /// <summary>Triangulates <paramref name="mesh"/> if needed, then builds the half-edge connectivity.</summary>
    public static HalfEdgeMesh FromMesh(Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return Build(Triangulator.Triangulate(mesh));
    }

    /// <summary>
    /// Builds the half-edge connectivity from an already-triangulated mesh. Throws if any face is not a
    /// triangle.
    /// </summary>
    public static HalfEdgeMesh Build(Mesh triangulated)
    {
        ArgumentNullException.ThrowIfNull(triangulated);

        int triCount = triangulated.Faces.Count;
        var origin = new int[triCount * 3];
        for (int t = 0; t < triCount; t++)
        {
            Face face = triangulated.Faces[t];
            if (!face.IsTriangle)
            {
                throw new ArgumentException(
                    $"Face {t} has {face.Count} corners; HalfEdgeMesh.Build requires a triangulated mesh.",
                    nameof(triangulated));
            }

            origin[(3 * t) + 0] = face[0].Position;
            origin[(3 * t) + 1] = face[1].Position;
            origin[(3 * t) + 2] = face[2].Position;
        }

        var (twin, boundaryCount, nonManifold) = BuildTwins(origin);

        IReadOnlyList<Vector3> positions = triangulated.Positions;
        var (faceNormal, faceArea) = ComputeFaceFrames(positions, origin, triCount);
        double[] dihedral = ComputeDihedrals(twin, faceNormal, faceArea);

        return new HalfEdgeMesh(positions, origin, twin, faceNormal, faceArea, dihedral, boundaryCount, nonManifold);
    }

    private static (int[] Twin, int BoundaryCount, int NonManifold) BuildTwins(int[] origin)
    {
        int count = origin.Length;
        var twin = new int[count];
        Array.Fill(twin, -1);

        // Group half-edges by undirected edge, then pair opposite directions deterministically.
        var byEdge = new Dictionary<EdgeKey, List<int>>(count);
        for (int h = 0; h < count; h++)
        {
            int from = origin[h];
            int to = origin[NextIndex(h)];
            var key = new EdgeKey(from, to);
            if (!byEdge.TryGetValue(key, out List<int>? list))
            {
                list = [];
                byEdge[key] = list;
            }

            list.Add(h);
        }

        int boundaryCount = 0;
        int nonManifold = 0;
        foreach (List<int> group in byEdge.Values)
        {
            group.Sort();

            // Greedily pair each still-unpaired half-edge with the lowest-index opposite-direction one.
            for (int i = 0; i < group.Count; i++)
            {
                int h = group[i];
                if (twin[h] >= 0)
                {
                    continue;
                }

                int partner = -1;
                for (int j = i + 1; j < group.Count; j++)
                {
                    int candidate = group[j];
                    if (twin[candidate] < 0 && origin[h] == origin[NextIndex(candidate)] &&
                        origin[candidate] == origin[NextIndex(h)])
                    {
                        partner = candidate;
                        break;
                    }
                }

                if (partner >= 0)
                {
                    twin[h] = partner;
                    twin[partner] = h;
                }
            }

            // Tally what the pairing left over.
            int unpaired = 0;
            foreach (int h in group)
            {
                if (twin[h] < 0)
                {
                    unpaired++;
                }
            }

            boundaryCount += unpaired;
            if (group.Count > 2)
            {
                nonManifold++;
            }
            else if (group.Count == 2 && unpaired == 2)
            {
                // Two same-direction half-edges on one edge: inconsistent winding / duplicate face.
                nonManifold++;
            }
        }

        return (twin, boundaryCount, nonManifold);
    }

    private static (Vector3[] Normals, double[] Areas) ComputeFaceFrames(
        IReadOnlyList<Vector3> positions, int[] origin, int triCount)
    {
        var normals = new Vector3[triCount];
        var areas = new double[triCount];
        for (int t = 0; t < triCount; t++)
        {
            Vector3 v0 = positions[origin[3 * t]];
            Vector3 v1 = positions[origin[(3 * t) + 1]];
            Vector3 v2 = positions[origin[(3 * t) + 2]];
            Vector3 cross = (v1 - v0).Cross(v2 - v0);
            double crossLen = cross.Length;
            areas[t] = 0.5 * crossLen;
            normals[t] = crossLen > 0 ? cross / crossLen : Vector3.Zero;
        }

        return (normals, areas);
    }

    private static double[] ComputeDihedrals(int[] twin, Vector3[] faceNormal, double[] faceArea)
    {
        var dihedral = new double[twin.Length];
        Array.Fill(dihedral, double.NaN);

        for (int h = 0; h < twin.Length; h++)
        {
            int t = twin[h];
            if (t <= h)
            {
                continue; // boundary (NaN), or already handled from the lower half-edge
            }

            int f0 = h / 3;
            int f1 = t / 3;
            double angle;
            if (faceArea[f0] == 0 || faceArea[f1] == 0)
            {
                angle = 0; // degenerate neighbour → treat as coplanar (never a hard edge)
            }
            else
            {
                double dot = Math.Clamp(faceNormal[f0].Dot(faceNormal[f1]), -1.0, 1.0);
                angle = Math.Acos(dot);
            }

            dihedral[h] = angle;
            dihedral[t] = angle;
        }

        return dihedral;
    }

    private static int NextIndex(int h) => (h - (h % 3)) + ((h + 1) % 3);
}
