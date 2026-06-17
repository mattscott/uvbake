using UVBake.Core;
using UVBake.Core.Geometry;
using UVBake.Core.Meshes;
using Xunit;

namespace UVBake.Tests;

/// <summary>P6 dirty-input robustness: non-manifold, degenerate, and open meshes bake without crashing or producing NaNs.</summary>
public class RobustnessTests
{
    [Fact]
    public void NonManifoldEdge_BuildsTolerantlyAndBakes()
    {
        // Three triangles sharing edge (0,1): a non-manifold edge.
        var positions = new[]
        {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0),
            new Vector3(0, 1, 0), new Vector3(0, -1, 0), new Vector3(0, 0, 1),
        };
        var faces = new List<Face> { Tri(0, 1, 2), Tri(0, 1, 3), Tri(0, 1, 4) };
        var mesh = new Mesh(positions, Array.Empty<Vector2>(), Array.Empty<Vector3>(), faces);

        HalfEdgeMesh he = HalfEdgeMesh.Build(Triangulator.Triangulate(mesh));
        Assert.True(he.NonManifoldEdgeCount > 0);

        UvMesh result = UVBaker.Generate(mesh, new UVBakeOptions());
        AssertFiniteUvs(result);
    }

    [Fact]
    public void DegenerateTriangle_DoesNotProduceNaN()
    {
        // A valid triangle plus a separate degenerate (collinear) triangle.
        var positions = new[]
        {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), // valid
            new Vector3(2, 0, 0), new Vector3(3, 0, 0), new Vector3(4, 0, 0), // collinear → zero area
        };
        var faces = new List<Face> { Tri(0, 1, 2), Tri(3, 4, 5) };
        var mesh = new Mesh(positions, Array.Empty<Vector2>(), Array.Empty<Vector3>(), faces);

        UvMesh result = UVBaker.Generate(mesh, new UVBakeOptions());
        AssertFiniteUvs(result);
    }

    [Fact]
    public void OpenMesh_BakesValidAtlas()
    {
        // A flat grid is an open mesh (it has a boundary); it should bake a valid atlas.
        Mesh grid = FlatGrid(3);
        UvMesh result = UVBaker.Generate(grid, new UVBakeOptions());

        AssertFiniteUvs(result);
        Assert.True(result.Validation.IsValid);
    }

    private static void AssertFiniteUvs(UvMesh result)
    {
        foreach (Vector2 uv in result.Mesh.TexCoords)
        {
            Assert.False(double.IsNaN(uv.X) || double.IsInfinity(uv.X), $"non-finite U: {uv.X}");
            Assert.False(double.IsNaN(uv.Y) || double.IsInfinity(uv.Y), $"non-finite V: {uv.Y}");
        }
    }

    private static Face Tri(int a, int b, int c) =>
        new([new Corner(a, Corner.None, Corner.None), new Corner(b, Corner.None, Corner.None), new Corner(c, Corner.None, Corner.None)]);

    private static Mesh FlatGrid(int n)
    {
        var positions = new List<Vector3>();
        for (int j = 0; j <= n; j++)
        {
            for (int i = 0; i <= n; i++)
            {
                positions.Add(new Vector3(i, j, 0));
            }
        }

        int Idx(int i, int j) => (j * (n + 1)) + i;
        var faces = new List<Face>();
        for (int j = 0; j < n; j++)
        {
            for (int i = 0; i < n; i++)
            {
                faces.Add(new Face([
                    new Corner(Idx(i, j), Corner.None, Corner.None),
                    new Corner(Idx(i + 1, j), Corner.None, Corner.None),
                    new Corner(Idx(i + 1, j + 1), Corner.None, Corner.None),
                    new Corner(Idx(i, j + 1), Corner.None, Corner.None),
                ]));
            }
        }

        return new Mesh(positions, Array.Empty<Vector2>(), Array.Empty<Vector3>(), faces);
    }
}
