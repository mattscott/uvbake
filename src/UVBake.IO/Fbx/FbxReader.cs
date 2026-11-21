using UVBake.Core.Geometry;
using UVBake.Core.Meshes;
using NMatrix = System.Numerics.Matrix4x4;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;

namespace UVBake.IO;

/// <summary>
/// Reads a binary FBX into a <see cref="Mesh"/> — positions (control points) and polygon faces, plus
/// per-wedge normals and UVs when the file carries those layers. The parser is clean-room
/// (<see cref="FbxBinaryParser"/>); mesh-to-world transforms are applied and all mesh parts are merged.
/// Hard edges are left to UVBake's dihedral detection (no crease layer is read).
/// </summary>
public static class FbxReader
{
    /// <summary>Reads and merges all FBX mesh parts in a file into one <see cref="Mesh"/>.</summary>
    public static Mesh Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using FileStream stream = File.OpenRead(path);
        return Read(stream);
    }

    /// <summary>Reads and merges all FBX mesh parts in a stream into one <see cref="Mesh"/>.</summary>
    public static Mesh Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        FbxNode root = FbxBinaryParser.Parse(stream);
        var scene = new FbxScene(root);

        var positions = new List<Vector3>();
        var texCoords = new List<Vector2>();
        var normals = new List<Vector3>();
        var faces = new List<Face>();
        foreach (FbxNode objects in root.ChildrenNamed("Objects"))
        {
            foreach (FbxNode geo in objects.ChildrenNamed("Geometry"))
            {
                AppendGeometry(geo, scene.MeshToWorld(geo.AsLong(0)), positions, texCoords, normals, faces);
            }
        }

        if (positions.Count == 0)
        {
            throw new InvalidDataException("FBX has no mesh geometry.");
        }

        return new Mesh(positions, texCoords, normals, faces);
    }

    private static void AppendGeometry(
        FbxNode geo,
        NMatrix meshToWorld,
        List<Vector3> positions,
        List<Vector2> texCoords,
        List<Vector3> normals,
        List<Face> faces)
    {
        double[]? vertices = geo.Child("Vertices")?.AsDoubleArray(0);
        int[]? polygonVertexIndex = geo.Child("PolygonVertexIndex")?.AsIntArray(0);
        if (vertices is null || polygonVertexIndex is null)
        {
            return;
        }

        int basePos = positions.Count;
        int controlPointCount = vertices.Length / 3;
        for (int i = 0; i < controlPointCount; i++)
        {
            var raw = new NVector3((float)vertices[i * 3], (float)vertices[(i * 3) + 1], (float)vertices[(i * 3) + 2]);
            NVector3 w = NVector3.Transform(raw, meshToWorld);
            positions.Add(new Vector3(w.X, w.Y, w.Z));
        }

        FbxLayer? normalLayer = FbxLayer.From(geo, "LayerElementNormal", "Normals", "NormalsIndex", 3);
        FbxLayer? uvLayer = FbxLayer.From(geo, "LayerElementUV", "UV", "UVIndex", 2);

        var corners = new List<Corner>();
        for (int pv = 0; pv < polygonVertexIndex.Length; pv++)
        {
            int index = polygonVertexIndex[pv];
            int cp = index < 0 ? ~index : index;

            int tex = Corner.None;
            if (uvLayer is not null)
            {
                NVector2 uv = uvLayer.Get2(pv, cp);
                texCoords.Add(new Vector2(uv.X, uv.Y));
                tex = texCoords.Count - 1;
            }

            int nrm = Corner.None;
            if (normalLayer is not null)
            {
                NVector3 n = normalLayer.Get3(pv, cp);
                normals.Add(new Vector3(n.X, n.Y, n.Z));
                nrm = normals.Count - 1;
            }

            corners.Add(new Corner(basePos + cp, tex, nrm));

            if (index < 0)
            {
                Corner[] faceCorners = corners.ToArray();
                if (!IsDegenerate(positions, faceCorners))
                {
                    faces.Add(new Face(faceCorners));
                }

                corners.Clear();
            }
        }
    }

    /// <summary>
    /// True when a polygon has fewer than three corners or (near-)zero area. Degenerate faces — e.g. a
    /// remesher's collapsed quads — are dropped on read so they don't become zero-Jacobian "flips" during
    /// unwrap. The area threshold is scale-invariant (compared against the longest edge).
    /// </summary>
    private static bool IsDegenerate(IReadOnlyList<Vector3> positions, Corner[] corners)
    {
        if (corners.Length < 3)
        {
            return true;
        }

        double maxEdgeSquared = 0;
        for (int k = 0; k < corners.Length; k++)
        {
            Vector3 a = positions[corners[k].Position];
            Vector3 b = positions[corners[(k + 1) % corners.Length].Position];
            double edge = (b - a).LengthSquared;
            if (edge > maxEdgeSquared)
            {
                maxEdgeSquared = edge;
            }
        }

        if (maxEdgeSquared <= double.Epsilon)
        {
            return true; // all corners coincident
        }

        double doubleArea = 0;
        Vector3 origin = positions[corners[0].Position];
        for (int t = 1; t <= corners.Length - 2; t++)
        {
            Vector3 u = positions[corners[t].Position] - origin;
            Vector3 v = positions[corners[t + 1].Position] - origin;
            doubleArea += u.Cross(v).Length;
        }

        return doubleArea < 1e-9 * maxEdgeSquared;
    }
}
