using UVBake.Core.Geometry;

namespace UVBake.Core.Meshes;

/// <summary>
/// Splits polygon faces into triangles with a <b>deterministic, reproducible</b> rule, because a
/// downstream tangent-space baker triangulates the same mesh to build its tangent frames — if the two
/// disagree on a quad's diagonal, the baked normals are subtly wrong (Documents/02 §3).
/// <para>
/// Rule: triangles pass through unchanged; a quad splits along its <b>shortest diagonal</b> (vertex
/// positions), ties broken by the lexicographically smaller endpoint index pair (orientation- and
/// mirror-independent); n-gons split by a deterministic fan from corner 0. Per-corner attributes
/// (texcoord/normal) are preserved on every emitted triangle.
/// </para>
/// </summary>
public static class Triangulator
{
    /// <summary>
    /// Returns a copy of <paramref name="mesh"/> with every face triangulated. Positions, texcoords,
    /// normals, and creases are shared with the source (they are indexed, not duplicated).
    /// </summary>
    public static Mesh Triangulate(Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var triangles = new List<Face>(mesh.Faces.Count);
        foreach (var face in mesh.Faces)
        {
            TriangulateFace(face, mesh.Positions, triangles);
        }

        return new Mesh(mesh.Positions, mesh.TexCoords, mesh.Normals, triangles, mesh.Creases);
    }

    private static void TriangulateFace(Face face, IReadOnlyList<Vector3> positions, List<Face> output)
    {
        int n = face.Count;
        switch (n)
        {
            case < 3:
                // Degenerate (point/edge) face: contributes no surface, drop it.
                return;
            case 3:
                output.Add(face);
                return;
            case 4:
                TriangulateQuad(face, positions, output);
                return;
            default:
                // Deterministic fan from corner 0. (The corpus is tris + quads; n-gons are rare and the
                // fan keeps the result reproducible. A shortest-diagonal n-gon split is a later refinement.)
                for (int i = 1; i + 1 < n; i++)
                {
                    output.Add(new Face([face[0], face[i], face[i + 1]]));
                }

                return;
        }
    }

    private static void TriangulateQuad(Face quad, IReadOnlyList<Vector3> positions, List<Face> output)
    {
        Corner a = quad[0], b = quad[1], c = quad[2], d = quad[3];

        double diagAc = (positions[a.Position] - positions[c.Position]).LengthSquared;
        double diagBd = (positions[b.Position] - positions[d.Position]).LengthSquared;

        // Squared lengths are compared with a relative tolerance so a true geometric tie is resolved by
        // the index tiebreak rather than by floating-point noise.
        double tol = 1e-10 * Math.Max(Math.Max(diagAc, diagBd), 1.0);
        bool useAc = Math.Abs(diagAc - diagBd) <= tol
            ? IndexPairLess(a.Position, c.Position, b.Position, d.Position)
            : diagAc < diagBd;

        if (useAc)
        {
            output.Add(new Face([a, b, c]));
            output.Add(new Face([a, c, d]));
        }
        else
        {
            output.Add(new Face([a, b, d]));
            output.Add(new Face([b, c, d]));
        }
    }

    /// <summary>
    /// True if the normalized index pair (min,max) of the first diagonal sorts before that of the
    /// second. Deterministic and independent of how the quad's corners were listed.
    /// </summary>
    private static bool IndexPairLess(int p, int q, int r, int s)
    {
        (int lo1, int hi1) = p <= q ? (p, q) : (q, p);
        (int lo2, int hi2) = r <= s ? (r, s) : (s, r);
        return lo1 != lo2 ? lo1 < lo2 : hi1 < hi2;
    }
}
