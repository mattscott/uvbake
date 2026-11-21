using System.Buffers.Binary;
using System.Text;
using UVBake.Core.Geometry;
using UVBake.Core.Meshes;

namespace UVBake.IO;

/// <summary>
/// Writes a <see cref="Mesh"/> as a binary FBX file (version 7400, 32-bit offsets, uncompressed arrays).
/// Emits positions + polygon faces, a per-wedge UV layer (ByPolygonVertex / IndexToDirect — UVBake's
/// shared-texcoord model maps directly to FBX's UVIndex), and a smooth per-control-point normal layer
/// computed from the geometry (the unwrap output carries no normals, and a downstream tangent-space
/// baker needs them). Clean-room; round-trips through <see cref="FbxReader"/>.
/// </summary>
public static class FbxWriter
{
    private const uint Version = 7400;
    private const long GeometryId = 1_000_000L;
    private const long ModelId = 2_000_000L;

    /// <summary>Writes <paramref name="mesh"/> as a binary FBX file at <paramref name="path"/>.</summary>
    public static void Write(Mesh mesh, string path)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(path);
        using FileStream stream = File.Create(path);
        Write(mesh, stream);
    }

    /// <summary>Writes <paramref name="mesh"/> as binary FBX to <paramref name="stream"/>.</summary>
    public static void Write(Mesh mesh, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(stream);
        FbxNode root = BuildTree(mesh);
        FbxBinaryWriter.WriteDocument(stream, root.Children, Version);
    }

    private static FbxNode BuildTree(Mesh mesh)
    {
        int vcount = mesh.Positions.Count;
        var verts = new double[vcount * 3];
        for (int v = 0; v < vcount; v++)
        {
            Vector3 p = mesh.Positions[v];
            verts[v * 3] = p.X;
            verts[(v * 3) + 1] = p.Y;
            verts[(v * 3) + 2] = p.Z;
        }

        bool hasUv = mesh.HasTexCoords;
        var pvi = new List<int>();
        var uvIndex = new List<int>();
        foreach (Face face in mesh.Faces)
        {
            ReadOnlySpan<Corner> corners = face.Corners;
            for (int k = 0; k < corners.Length; k++)
            {
                Corner c = corners[k];
                pvi.Add(k == corners.Length - 1 ? ~c.Position : c.Position); // bitwise-NOT marks polygon end
                if (hasUv)
                {
                    uvIndex.Add(c.HasTexCoord ? c.TexCoord : 0);
                }
            }
        }

        var uvData = new double[mesh.TexCoords.Count * 2];
        for (int t = 0; t < mesh.TexCoords.Count; t++)
        {
            Vector2 uv = mesh.TexCoords[t];
            uvData[t * 2] = uv.X;
            uvData[(t * 2) + 1] = uv.Y;
        }

        double[] normals = ComputeSmoothNormals(mesh);

        var root = new FbxNode("(root)");

        var header = new FbxNode("FBXHeaderExtension");
        header.Children.Add(Scalar("FBXHeaderVersion", 1003));
        header.Children.Add(Scalar("FBXVersion", (int)Version));
        header.Children.Add(Scalar("Creator", "UVBake"));
        root.Children.Add(header);

        var objects = new FbxNode("Objects");

        var geo = new FbxNode("Geometry");
        geo.Properties.Add(GeometryId);
        geo.Properties.Add("UVBake\0Geometry");
        geo.Properties.Add("Mesh");
        geo.Children.Add(DoubleArray("Vertices", verts));
        geo.Children.Add(IntArray("PolygonVertexIndex", pvi.ToArray()));
        geo.Children.Add(Scalar("GeometryVersion", 124));

        var normalLayer = new FbxNode("LayerElementNormal");
        normalLayer.Properties.Add(0);
        normalLayer.Children.Add(Scalar("Version", 101));
        normalLayer.Children.Add(Scalar("Name", string.Empty));
        normalLayer.Children.Add(Scalar("MappingInformationType", "ByControlPoint"));
        normalLayer.Children.Add(Scalar("ReferenceInformationType", "Direct"));
        normalLayer.Children.Add(DoubleArray("Normals", normals));
        geo.Children.Add(normalLayer);

        if (hasUv)
        {
            var uvLayer = new FbxNode("LayerElementUV");
            uvLayer.Properties.Add(0);
            uvLayer.Children.Add(Scalar("Version", 101));
            uvLayer.Children.Add(Scalar("Name", "map1"));
            uvLayer.Children.Add(Scalar("MappingInformationType", "ByPolygonVertex"));
            uvLayer.Children.Add(Scalar("ReferenceInformationType", "IndexToDirect"));
            uvLayer.Children.Add(DoubleArray("UV", uvData));
            uvLayer.Children.Add(IntArray("UVIndex", uvIndex.ToArray()));
            geo.Children.Add(uvLayer);
        }

        var layer = new FbxNode("Layer");
        layer.Properties.Add(0);
        layer.Children.Add(Scalar("Version", 100));
        layer.Children.Add(LayerElement("LayerElementNormal"));
        if (hasUv)
        {
            layer.Children.Add(LayerElement("LayerElementUV"));
        }

        geo.Children.Add(layer);
        objects.Children.Add(geo);

        var model = new FbxNode("Model");
        model.Properties.Add(ModelId);
        model.Properties.Add("UVBake\0Model");
        model.Properties.Add("Mesh");
        model.Children.Add(Scalar("Version", 232));
        objects.Children.Add(model);

        root.Children.Add(objects);

        var connections = new FbxNode("Connections");
        connections.Children.Add(Connect(GeometryId, ModelId));
        connections.Children.Add(Connect(ModelId, 0L));
        root.Children.Add(connections);

        return root;
    }

    /// <summary>Area-weighted smooth normal per control point (one smoothing group — ideal for tangent-space baking).</summary>
    private static double[] ComputeSmoothNormals(Mesh mesh)
    {
        int n = mesh.Positions.Count;
        var accum = new Vector3[n];
        foreach (Face face in mesh.Faces)
        {
            ReadOnlySpan<Corner> c = face.Corners;
            for (int t = 1; t <= c.Length - 2; t++) // fan-triangulate the polygon
            {
                int i0 = c[0].Position, i1 = c[t].Position, i2 = c[t + 1].Position;
                Vector3 fn = (mesh.Positions[i1] - mesh.Positions[i0]).Cross(mesh.Positions[i2] - mesh.Positions[i0]);
                accum[i0] += fn;
                accum[i1] += fn;
                accum[i2] += fn;
            }
        }

        var result = new double[n * 3];
        for (int i = 0; i < n; i++)
        {
            Vector3 v = accum[i];
            Vector3 unit = v.LengthSquared > 1e-24 ? v.Normalized() : new Vector3(0, 0, 1);
            result[i * 3] = unit.X;
            result[(i * 3) + 1] = unit.Y;
            result[(i * 3) + 2] = unit.Z;
        }

        return result;
    }

    private static FbxNode LayerElement(string type)
    {
        var element = new FbxNode("LayerElement");
        element.Children.Add(Scalar("Type", type));
        element.Children.Add(Scalar("TypedIndex", 0));
        return element;
    }

    private static FbxNode Connect(long child, long parent)
    {
        var c = new FbxNode("C");
        c.Properties.Add("OO");
        c.Properties.Add(child);
        c.Properties.Add(parent);
        return c;
    }

    private static FbxNode Scalar(string name, int value)
    {
        var node = new FbxNode(name);
        node.Properties.Add(value);
        return node;
    }

    private static FbxNode Scalar(string name, string value)
    {
        var node = new FbxNode(name);
        node.Properties.Add(value);
        return node;
    }

    private static FbxNode DoubleArray(string name, double[] data)
    {
        var node = new FbxNode(name);
        node.Properties.Add(data);
        return node;
    }

    private static FbxNode IntArray(string name, int[] data)
    {
        var node = new FbxNode(name);
        node.Properties.Add(data);
        return node;
    }
}

/// <summary>
/// Serializes an <see cref="FbxNode"/> tree to the binary FBX record format. Buffers into a seekable
/// <see cref="MemoryStream"/> so node end-offsets and property-list lengths can be back-patched, then
/// copies the result to the target stream. Arrays are written uncompressed (encoding 0).
/// </summary>
internal static class FbxBinaryWriter
{
    private static ReadOnlySpan<byte> Magic => "Kaydara FBX Binary  "u8;

    private static ReadOnlySpan<byte> EndMagic => new byte[]
    {
        0xf8, 0x5a, 0x8c, 0x6a, 0xde, 0xf5, 0xd9, 0x7e, 0xec, 0xe9, 0x0c, 0xe3, 0x75, 0x8f, 0x29, 0x0b,
    };

    public static void WriteDocument(Stream target, IReadOnlyList<FbxNode> topLevel, uint version)
    {
        using var buffer = new MemoryStream();

        buffer.Write(Magic);
        buffer.WriteByte(0x00);
        buffer.WriteByte(0x1a);
        buffer.WriteByte(0x00);
        WriteUInt32(buffer, version);

        foreach (FbxNode node in topLevel)
        {
            WriteNode(buffer, node);
        }

        WriteNullRecord(buffer);
        WriteFooter(buffer, version);

        buffer.Position = 0;
        buffer.CopyTo(target);
    }

    private static void WriteNode(MemoryStream s, FbxNode node)
    {
        long endOffsetPos = s.Position;
        WriteUInt32(s, 0);                          // EndOffset (back-patched)
        WriteUInt32(s, (uint)node.Properties.Count);
        long propLenPos = s.Position;
        WriteUInt32(s, 0);                          // PropertyListLength (back-patched)
        byte[] name = Encoding.ASCII.GetBytes(node.Name);
        s.WriteByte((byte)name.Length);
        s.Write(name);

        long propsStart = s.Position;
        foreach (object prop in node.Properties)
        {
            WriteProperty(s, prop);
        }

        Backpatch(s, propLenPos, (uint)(s.Position - propsStart));

        if (node.Children.Count > 0)
        {
            foreach (FbxNode child in node.Children)
            {
                WriteNode(s, child);
            }

            WriteNullRecord(s); // a nested list is terminated by a null record
        }

        Backpatch(s, endOffsetPos, (uint)s.Position);
    }

    private static void WriteProperty(MemoryStream s, object prop)
    {
        switch (prop)
        {
            case int i:
                s.WriteByte((byte)'I');
                WriteInt32(s, i);
                break;
            case long l:
                s.WriteByte((byte)'L');
                WriteInt64(s, l);
                break;
            case double d:
                s.WriteByte((byte)'D');
                WriteUInt64(s, BitConverter.DoubleToUInt64Bits(d));
                break;
            case string str:
                s.WriteByte((byte)'S');
                byte[] bytes = Encoding.Latin1.GetBytes(str); // Latin1 preserves the \0\1 name separator
                WriteUInt32(s, (uint)bytes.Length);
                s.Write(bytes);
                break;
            case double[] da:
                WriteDoubleArray(s, da);
                break;
            case int[] ia:
                WriteIntArray(s, ia);
                break;
            default:
                throw new NotSupportedException($"cannot serialize FBX property of type {prop.GetType()}.");
        }
    }

    private static void WriteDoubleArray(MemoryStream s, double[] data)
    {
        s.WriteByte((byte)'d');
        WriteUInt32(s, (uint)data.Length);
        WriteUInt32(s, 0);                        // encoding: raw
        WriteUInt32(s, (uint)(data.Length * 8));  // payload length
        Span<byte> b = stackalloc byte[8];
        foreach (double v in data)
        {
            BinaryPrimitives.WriteDoubleLittleEndian(b, v);
            s.Write(b);
        }
    }

    private static void WriteIntArray(MemoryStream s, int[] data)
    {
        s.WriteByte((byte)'i');
        WriteUInt32(s, (uint)data.Length);
        WriteUInt32(s, 0);                        // encoding: raw
        WriteUInt32(s, (uint)(data.Length * 4));  // payload length
        Span<byte> b = stackalloc byte[4];
        foreach (int v in data)
        {
            BinaryPrimitives.WriteInt32LittleEndian(b, v);
            s.Write(b);
        }
    }

    private static void WriteNullRecord(MemoryStream s)
    {
        Span<byte> zero = stackalloc byte[13]; // 3 x uint32 + 1 byte, all zero (32-bit offsets)
        zero.Clear();
        s.Write(zero);
    }

    private static void WriteFooter(MemoryStream s, uint version)
    {
        Span<byte> footerId = stackalloc byte[16]; // importers do not validate it; zeros are accepted
        footerId.Clear();
        s.Write(footerId);

        while (s.Position % 16 != 0)
        {
            s.WriteByte(0);
        }

        WriteUInt32(s, version);
        Span<byte> zeros = stackalloc byte[120];
        zeros.Clear();
        s.Write(zeros);
        s.Write(EndMagic);
    }

    private static void Backpatch(MemoryStream s, long position, uint value)
    {
        long save = s.Position;
        s.Position = position;
        WriteUInt32(s, value);
        s.Position = save;
    }

    private static void WriteUInt32(MemoryStream s, uint value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, value);
        s.Write(b);
    }

    private static void WriteInt32(MemoryStream s, int value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(b, value);
        s.Write(b);
    }

    private static void WriteInt64(MemoryStream s, long value)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(b, value);
        s.Write(b);
    }

    private static void WriteUInt64(MemoryStream s, ulong value)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(b, value);
        s.Write(b);
    }
}
