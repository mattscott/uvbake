using UVBake.Core.Geometry;
using UVBake.Core.Meshes;
using UVBake.Tests.Corpus;
using Xunit;

namespace UVBake.Tests;

/// <summary>Half-edge connectivity invariants on the procedural corpus.</summary>
public class HalfEdgeTests
{
    // Expected genus per primitive (torus is genus 1; the rest are genus 0).
    private static readonly Dictionary<string, int> ExpectedGenus = new()
    {
        ["cube"] = 0,
        ["beveled-box"] = 0,
        ["sphere"] = 0,
        ["cylinder"] = 0,
        ["torus"] = 1,
    };

    [Fact]
    public void Corpus_BuildsClosedManifoldWithOutwardNormals()
    {
        foreach ((string name, Mesh mesh) in Primitives.All())
        {
            HalfEdgeMesh he = HalfEdgeMesh.FromMesh(mesh);

            Assert.Equal(0, he.BoundaryEdgeCount);
            Assert.Equal(0, he.NonManifoldEdgeCount);

            for (int t = 0; t < he.TriangleCount; t++)
            {
                if (he.FaceArea(t) <= 0)
                {
                    continue;
                }

                Vector3 centroid = (mesh.Positions[he.Origin(3 * t)]
                    + mesh.Positions[he.Origin((3 * t) + 1)]
                    + mesh.Positions[he.Origin((3 * t) + 2)]) / 3.0;

                // Outward = points away from the body's interior. The star-shaped primitives reference the
                // origin; the torus (not star-shaped) references the nearest point on its central ring.
                Vector3 reference = name == "torus" ? RingProject(centroid, ringRadius: 1.0) : Vector3.Zero;
                Assert.True(he.FaceNormal(t).Dot(centroid - reference) > 0,
                    $"{name}: triangle {t} normal is not outward");
            }
        }
    }

    [Fact]
    public void Corpus_EulerCharacteristicMatchesGenus()
    {
        foreach ((string name, Mesh mesh) in Primitives.All())
        {
            HalfEdgeMesh he = HalfEdgeMesh.FromMesh(mesh);

            int v = mesh.Positions.Count;
            int f = he.TriangleCount;
            int e = he.InteriorEdges().Count();
            int genus = (2 - (v - e + f)) / 2;

            Assert.Equal(ExpectedGenus[name], genus);
        }
    }

    /// <summary>Projects a point onto the torus's central ring (radius <paramref name="ringRadius"/> in the XZ plane).</summary>
    private static Vector3 RingProject(Vector3 point, double ringRadius)
    {
        var radial = new Vector3(point.X, 0, point.Z);
        return radial.Normalized() * ringRadius;
    }

    [Fact]
    public void Cube_HasExpectedConnectivityCounts()
    {
        HalfEdgeMesh he = HalfEdgeMesh.FromMesh(Primitives.Cube());

        Assert.Equal(12, he.TriangleCount);          // 6 quads × 2
        Assert.Equal(36, he.HalfEdgeCount);          // 12 × 3
        Assert.Equal(18, he.InteriorEdges().Count()); // 12 cube edges + 6 face diagonals
    }
}
