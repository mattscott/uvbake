using UVBake.Core.Geometry;
using UVBake.Core.Meshes;
using UVBake.Tests.Corpus;
using Xunit;

namespace UVBake.Tests;

/// <summary>Deterministic shortest-diagonal triangulation — reproducible, orientation-independent.</summary>
public class TriangulationTests
{
    [Fact]
    public void Quad_SplitsAlongShorterDiagonal()
    {
        // Diagonal 1-3 (len² 10) is shorter than 0-2 (len² 26), so the split uses 1-3.
        Mesh quad = Quad(
            new Vector3(0, 0, 0),
            new Vector3(4, 0, 0),
            new Vector3(5, 1, 0),
            new Vector3(1, 1, 0));

        Mesh tris = Triangulator.Triangulate(quad);

        Assert.Equal(2, tris.Faces.Count);
        var triples = new HashSet<(int, int, int)>
        {
            TestSupport.SortedTriple(tris.Faces[0]),
            TestSupport.SortedTriple(tris.Faces[1]),
        };
        Assert.Contains((0, 1, 3), triples);
        Assert.Contains((1, 2, 3), triples);
    }

    [Fact]
    public void Quad_TieBrokenByIndexPair()
    {
        // Unit square: both diagonals equal → tiebreak picks the (0,2) diagonal over (1,3).
        Mesh square = Quad(
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(1, 1, 0),
            new Vector3(0, 1, 0));

        Mesh tris = Triangulator.Triangulate(square);

        var triples = new HashSet<(int, int, int)>
        {
            TestSupport.SortedTriple(tris.Faces[0]),
            TestSupport.SortedTriple(tris.Faces[1]),
        };
        Assert.Contains((0, 1, 2), triples);
        Assert.Contains((0, 2, 3), triples);
    }

    [Fact]
    public void Quad_TriangulationIsRotationInvariant()
    {
        var p0 = new Vector3(0, 0, 0);
        var p1 = new Vector3(4, 0, 0);
        var p2 = new Vector3(5, 1, 0);
        var p3 = new Vector3(1, 1, 0);

        // Listing the same quad's corners rotated by one must pick the same geometric diagonal, so the
        // resulting triangles (compared as sets of positions) are identical.
        var original = GeometricTriangles(Triangulator.Triangulate(Quad(p0, p1, p2, p3)));
        var rotated = GeometricTriangles(Triangulator.Triangulate(Quad(p1, p2, p3, p0)));

        Assert.Equal(original, rotated);
    }

    [Fact]
    public void Triangulation_IsDeterministic()
    {
        Mesh cube = Primitives.Cube();
        Mesh a = Triangulator.Triangulate(cube);
        Mesh b = Triangulator.Triangulate(cube);
        TestSupport.AssertMeshEqual(a, b);
    }

    [Fact]
    public void Ngon_SplitsByDeterministicFan()
    {
        // Pentagon → 3 triangles, fanned from corner 0.
        var positions = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(2, 0, 0),
            new Vector3(3, 2, 0),
            new Vector3(1, 3, 0),
            new Vector3(-1, 2, 0),
        };
        var faces = new List<Face>
        {
            new([
                new Corner(0, Corner.None, Corner.None), new Corner(1, Corner.None, Corner.None),
                new Corner(2, Corner.None, Corner.None), new Corner(3, Corner.None, Corner.None),
                new Corner(4, Corner.None, Corner.None),
            ]),
        };
        Mesh mesh = new(positions, Array.Empty<Vector2>(), Array.Empty<Vector3>(), faces);

        Mesh tris = Triangulator.Triangulate(mesh);

        Assert.Equal(3, tris.Faces.Count);
        Assert.Equal((0, 1, 2), TestSupport.SortedTriple(tris.Faces[0]));
        Assert.Equal((0, 2, 3), TestSupport.SortedTriple(tris.Faces[1]));
        Assert.Equal((0, 3, 4), TestSupport.SortedTriple(tris.Faces[2]));
    }

    /// <summary>The triangles as a set of position triples (each triple sorted), independent of indexing.</summary>
    private static HashSet<(Vector3, Vector3, Vector3)> GeometricTriangles(Mesh tris)
    {
        var result = new HashSet<(Vector3, Vector3, Vector3)>();
        foreach (Face f in tris.Faces)
        {
            var v = new[] { tris.Positions[f[0].Position], tris.Positions[f[1].Position], tris.Positions[f[2].Position] };
            Array.Sort(v, static (a, b) =>
                a.X != b.X ? a.X.CompareTo(b.X) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Z.CompareTo(b.Z));
            result.Add((v[0], v[1], v[2]));
        }

        return result;
    }

    private static Mesh Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        var faces = new List<Face>
        {
            new([
                new Corner(0, Corner.None, Corner.None), new Corner(1, Corner.None, Corner.None),
                new Corner(2, Corner.None, Corner.None), new Corner(3, Corner.None, Corner.None),
            ]),
        };
        return new Mesh([p0, p1, p2, p3], Array.Empty<Vector2>(), Array.Empty<Vector3>(), faces);
    }
}
