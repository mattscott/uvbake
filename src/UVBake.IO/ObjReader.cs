using System.Globalization;
using UVBake.Core.Geometry;
using UVBake.Core.Meshes;

namespace UVBake.IO;

/// <summary>
/// Reads Wavefront OBJ into a <see cref="Mesh"/>, preserving per-wedge attributes (a face corner keeps
/// its own <c>v</c>/<c>vt</c>/<c>vn</c> indices, so seam UVs survive). Supports positions, texcoords,
/// normals, polygonal faces with 1-based or negative (relative) indices, and smoothing groups (<c>s</c>),
/// from which crease edges are derived for <see cref="SeamSource.ProvidedTags"/>. Other directives
/// (groups, materials, comments) are ignored.
/// </summary>
public static class ObjReader
{
    /// <summary>Reads an OBJ file from <paramref name="path"/>.</summary>
    public static Mesh Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using var reader = new StreamReader(path);
        return Read(reader);
    }

    /// <summary>Reads OBJ text from <paramref name="reader"/>.</summary>
    public static Mesh Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var positions = new List<Vector3>();
        var texCoords = new List<Vector2>();
        var normals = new List<Vector3>();
        var faces = new List<Face>();

        // Smoothing-group bookkeeping: an edge between two faces in different groups is a crease.
        var faceGroups = new List<int>();
        int currentGroup = 0;
        bool sawSmoothingGroups = false;

        string? line;
        int lineNumber = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            string[] tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0 || tokens[0].StartsWith('#'))
            {
                continue;
            }

            switch (tokens[0])
            {
                case "v":
                    positions.Add(new Vector3(
                        ParseDouble(tokens, 1, lineNumber),
                        ParseDouble(tokens, 2, lineNumber),
                        ParseDouble(tokens, 3, lineNumber)));
                    break;

                case "vt":
                    texCoords.Add(new Vector2(
                        ParseDouble(tokens, 1, lineNumber),
                        tokens.Length > 2 ? ParseDouble(tokens, 2, lineNumber) : 0.0));
                    break;

                case "vn":
                    normals.Add(new Vector3(
                        ParseDouble(tokens, 1, lineNumber),
                        ParseDouble(tokens, 2, lineNumber),
                        ParseDouble(tokens, 3, lineNumber)));
                    break;

                case "f":
                    faces.Add(ParseFace(tokens, positions.Count, texCoords.Count, normals.Count, lineNumber));
                    faceGroups.Add(currentGroup);
                    break;

                case "s":
                    currentGroup = ParseSmoothingGroup(tokens);
                    if (currentGroup > 0)
                    {
                        sawSmoothingGroups = true;
                    }

                    break;

                default:
                    // o, g, mtllib, usemtl, etc. — not needed to reconstruct geometry + UVs.
                    break;
            }
        }

        IReadOnlySet<EdgeKey>? creases = sawSmoothingGroups ? DeriveCreases(faces, faceGroups) : null;
        return new Mesh(positions, texCoords, normals, faces, creases);
    }

    private static double ParseDouble(string[] tokens, int index, int lineNumber)
    {
        if (index >= tokens.Length ||
            !double.TryParse(tokens[index], NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            throw new FormatException(
                $"OBJ line {lineNumber}: expected a number at position {index} in '{string.Join(' ', tokens)}'.");
        }

        return value;
    }

    private static Face ParseFace(string[] tokens, int vCount, int vtCount, int vnCount, int lineNumber)
    {
        int cornerCount = tokens.Length - 1;
        if (cornerCount < 3)
        {
            throw new FormatException($"OBJ line {lineNumber}: a face needs at least 3 corners.");
        }

        var corners = new Corner[cornerCount];
        for (int i = 0; i < cornerCount; i++)
        {
            corners[i] = ParseCorner(tokens[i + 1], vCount, vtCount, vnCount, lineNumber);
        }

        return new Face(corners);
    }

    private static Corner ParseCorner(string token, int vCount, int vtCount, int vnCount, int lineNumber)
    {
        // Forms: v | v/vt | v//vn | v/vt/vn
        string[] parts = token.Split('/');
        int position = Resolve(ParseIndex(parts[0], lineNumber), vCount, lineNumber);
        int texCoord = parts.Length > 1 && parts[1].Length > 0
            ? Resolve(ParseIndex(parts[1], lineNumber), vtCount, lineNumber)
            : Corner.None;
        int normal = parts.Length > 2 && parts[2].Length > 0
            ? Resolve(ParseIndex(parts[2], lineNumber), vnCount, lineNumber)
            : Corner.None;
        return new Corner(position, texCoord, normal);
    }

    private static int ParseIndex(string text, int lineNumber)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int raw) || raw == 0)
        {
            throw new FormatException($"OBJ line {lineNumber}: invalid index '{text}'.");
        }

        return raw;
    }

    /// <summary>Converts a 1-based (positive) or relative (negative) OBJ index to a 0-based index.</summary>
    private static int Resolve(int raw, int count, int lineNumber)
    {
        int resolved = raw > 0 ? raw - 1 : count + raw;
        if (resolved < 0 || resolved >= count)
        {
            throw new FormatException($"OBJ line {lineNumber}: index {raw} is out of range (have {count}).");
        }

        return resolved;
    }

    private static int ParseSmoothingGroup(string[] tokens)
    {
        if (tokens.Length < 2)
        {
            return 0;
        }

        string value = tokens[1];
        if (value.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int group) ? group : 0;
    }

    /// <summary>
    /// An edge shared by two faces in different smoothing groups is a crease. Faces in the same nonzero
    /// group share a smooth edge; transitions to/from a different group (including ungrouped) are hard.
    /// </summary>
    private static IReadOnlySet<EdgeKey> DeriveCreases(List<Face> faces, List<int> faceGroups)
    {
        var firstGroup = new Dictionary<EdgeKey, int>();
        var creases = new HashSet<EdgeKey>();

        for (int f = 0; f < faces.Count; f++)
        {
            Face face = faces[f];
            int group = faceGroups[f];
            int n = face.Count;
            for (int i = 0; i < n; i++)
            {
                var key = new EdgeKey(face[i].Position, face[(i + 1) % n].Position);
                if (firstGroup.TryGetValue(key, out int seen))
                {
                    if (seen != group)
                    {
                        creases.Add(key);
                    }
                }
                else
                {
                    firstGroup[key] = group;
                }
            }
        }

        return creases;
    }
}
