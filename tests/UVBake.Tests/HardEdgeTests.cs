using UVBake.Core;
using UVBake.Core.Geometry;
using UVBake.Core.Meshes;
using UVBake.Core.Segmentation;
using UVBake.Tests.Corpus;
using Xunit;

namespace UVBake.Tests;

/// <summary>Hard-edge detection — dihedral threshold behavior, crease tags, and the corpus expectations.</summary>
public class HardEdgeTests
{
    private static readonly IReadOnlySet<EdgeKey> NoCreases = new HashSet<EdgeKey>();

    [Theory]
    [InlineData(30.0, 45.0, false)] // fold 30° < threshold 45° → not hard
    [InlineData(60.0, 45.0, true)]  // fold 60° > threshold 45° → hard
    [InlineData(20.0, 10.0, true)]  // fold 20° > threshold 10° → hard
    public void DihedralFold_RespectsThreshold(double foldDegrees, double thresholdDegrees, bool expectHard)
    {
        HalfEdgeMesh he = HalfEdgeMesh.Build(DihedralFold(foldDegrees));

        HardEdgeDetector.Detect(he, NoCreases, thresholdDegrees, SeamSource.DihedralAngle);

        Assert.Equal(expectHard ? 1 : 0, he.HardEdgeKeys().Count());
    }

    [Fact]
    public void Cube_HasTwelveHardEdgesAllNonDiagonal()
    {
        HalfEdgeMesh he = HalfEdgeMesh.FromMesh(Primitives.Cube(half: 1.0));

        HardEdgeDetector.Detect(he, NoCreases, 45.0, SeamSource.DihedralAngle);

        List<EdgeKey> hard = he.HardEdgeKeys().ToList();
        Assert.Equal(12, hard.Count);

        // Every hard edge is a cube edge (length 2), never a face diagonal (length 2√2).
        foreach (EdgeKey edge in hard)
        {
            double lengthSquared = (he.Positions[edge.A] - he.Positions[edge.B]).LengthSquared;
            Assert.Equal(4.0, lengthSquared, precision: 9);
        }
    }

    [Fact]
    public void BeveledBox_ThresholdSeparatesRimsFromChamfers()
    {
        // 16 rim edges deviate 90°; 8 side chamfer edges deviate 45°.
        HalfEdgeMesh atSixty = HalfEdgeMesh.FromMesh(Primitives.BeveledBox());
        HardEdgeDetector.Detect(atSixty, NoCreases, 60.0, SeamSource.DihedralAngle);
        Assert.Equal(16, atSixty.HardEdgeKeys().Count()); // only the 90° rims

        HalfEdgeMesh atThirty = HalfEdgeMesh.FromMesh(Primitives.BeveledBox());
        HardEdgeDetector.Detect(atThirty, NoCreases, 30.0, SeamSource.DihedralAngle);
        Assert.Equal(24, atThirty.HardEdgeKeys().Count()); // rims + the 45° chamfers
    }

    [Fact]
    public void ProvidedTags_MarkCreaseEdgesOnly()
    {
        // A flat two-triangle quad (no dihedral) so any hard edge must come from a crease tag. Its only
        // interior edge is the shared diagonal (0,2); a crease there is the one detectable tag.
        Mesh strip = FlatStrip();
        var creases = new HashSet<EdgeKey> { new(0, 2) };

        HalfEdgeMesh he = HalfEdgeMesh.Build(strip);
        HardEdgeDetector.Detect(he, creases, 45.0, SeamSource.ProvidedTags);

        List<EdgeKey> hard = he.HardEdgeKeys().ToList();
        Assert.Single(hard);
        Assert.Equal(new EdgeKey(0, 2), hard[0]);
    }

    [Fact]
    public void Both_UnionsDihedralAndTags()
    {
        HalfEdgeMesh he = HalfEdgeMesh.FromMesh(Primitives.Cube());

        // Add a crease on a face diagonal (which dihedral alone never flags) and require Both.
        EdgeKey diagonal = he.InteriorEdges()
            .Select(he.EdgeKeyOf)
            .First(e => (he.Positions[e.A] - he.Positions[e.B]).LengthSquared > 5.0); // a 2√2 diagonal
        var creases = new HashSet<EdgeKey> { diagonal };

        HardEdgeDetector.Detect(he, creases, 45.0, SeamSource.Both);

        Assert.Equal(13, he.HardEdgeKeys().Count()); // 12 dihedral + 1 tagged diagonal
    }

    /// <summary>
    /// Two triangles sharing edge (0,1); the second is folded so the angle between the face normals is
    /// exactly <paramref name="foldDegrees"/>.
    /// </summary>
    private static Mesh DihedralFold(double foldDegrees)
    {
        double a = foldDegrees * Math.PI / 180.0;
        var positions = new[]
        {
            new Vector3(0, 0, 0),                       // 0 — shared
            new Vector3(1, 0, 0),                       // 1 — shared
            new Vector3(0.5, 1, 0),                      // 2 — triangle 1 (normal +Z)
            new Vector3(0.5, -Math.Cos(a), Math.Sin(a)), // 3 — triangle 2 (normal tilted by `a`)
        };
        var faces = new List<Face>
        {
            Tri(0, 1, 2),
            Tri(1, 0, 3), // opposite orientation on the shared edge → consistent winding
        };
        return new Mesh(positions, Array.Empty<Vector2>(), Array.Empty<Vector3>(), faces);
    }

    private static Mesh FlatStrip()
    {
        var positions = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(1, 1, 0),
            new Vector3(0, 1, 0),
        };
        var faces = new List<Face> { Tri(0, 1, 2), Tri(0, 2, 3) };
        return new Mesh(positions, Array.Empty<Vector2>(), Array.Empty<Vector3>(), faces);
    }

    private static Face Tri(int a, int b, int c) =>
        new([new Corner(a, Corner.None, Corner.None), new Corner(b, Corner.None, Corner.None), new Corner(c, Corner.None, Corner.None)]);
}
