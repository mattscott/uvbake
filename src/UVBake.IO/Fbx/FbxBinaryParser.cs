using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace UVBake.IO;

/// <summary>
/// Clean-room parser for the binary FBX record format (Kaydara/Autodesk). Reads the documented structure
/// — header + version, node records (32-bit offsets pre-7500, 64-bit from 7500), and properties
/// (primitives, raw or zlib-compressed typed arrays, and strings) — into an <see cref="FbxNode"/> tree.
/// No Autodesk SDK and no native code; array decompression uses the framework <see cref="ZLibStream"/>.
/// </summary>
internal sealed class FbxBinaryParser
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("Kaydara FBX Binary  ");

    private readonly byte[] _data;
    private int _pos;
    private int _offsetSize;

    private FbxBinaryParser(byte[] data)
    {
        _data = data;
    }

    /// <summary>Parses a stream into a synthetic root node whose children are the top-level records.</summary>
    public static FbxNode Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return new FbxBinaryParser(ms.ToArray()).ParseDocument();
    }

    private FbxNode ParseDocument()
    {
        if (_data.Length < 27 || !_data.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw new InvalidDataException("not a binary FBX file (bad magic).");
        }

        uint version = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(23));
        _offsetSize = version >= 7500 ? 8 : 4;
        _pos = 27;

        var root = new FbxNode("(root)");
        while (_pos + (3 * _offsetSize) + 1 <= _data.Length)
        {
            FbxNode? node = ReadNode();
            if (node is null)
            {
                break;
            }

            root.Children.Add(node);
        }

        return root;
    }

    private FbxNode? ReadNode()
    {
        long endOffset = ReadOffset();
        long numProperties = ReadOffset();
        ReadOffset(); // property-list length (unused — we read each property)
        int nameLen = _data[_pos++];
        if (endOffset == 0)
        {
            return null; // null record (3*offsetSize+1 zero bytes) terminates a child list / the document
        }

        var node = new FbxNode(Encoding.ASCII.GetString(_data, _pos, nameLen));
        _pos += nameLen;

        for (long i = 0; i < numProperties; i++)
        {
            node.Properties.Add(ReadProperty());
        }

        while (_pos < endOffset)
        {
            FbxNode? child = ReadNode();
            if (child is null)
            {
                break;
            }

            node.Children.Add(child);
        }

        _pos = (int)endOffset;
        return node;
    }

    private object ReadProperty()
    {
        char type = (char)_data[_pos++];
        switch (type)
        {
            case 'Y': return (long)ReadInt16();
            case 'C': return _data[_pos++] != 0;
            case 'I': return (long)ReadInt32();
            case 'F': return (double)ReadSingle();
            case 'D': return ReadDouble();
            case 'L': return ReadInt64();
            case 'f': return ReadFloatArray();
            case 'd': return ReadDoubleArray();
            case 'l': return ReadLongArray();
            case 'i': return ReadIntArray();
            case 'b': return ReadBoolArray();
            case 'S': return ReadString();
            case 'R': return ReadRaw();
            default: throw new InvalidDataException($"unknown FBX property type '{type}'.");
        }
    }

    private (byte[] Raw, int Length) ReadArrayBytes(int elementSize)
    {
        int length = ReadInt32();
        int encoding = ReadInt32();
        int compressedLength = ReadInt32();
        ReadOnlySpan<byte> payload = _data.AsSpan(_pos, compressedLength);
        _pos += compressedLength;
        byte[] raw = encoding == 1 ? Inflate(payload, length * elementSize) : payload.ToArray();
        return (raw, length);
    }

    private double[] ReadDoubleArray()
    {
        (byte[] raw, int length) = ReadArrayBytes(8);
        var result = new double[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = BinaryPrimitives.ReadDoubleLittleEndian(raw.AsSpan(i * 8));
        }

        return result;
    }

    private float[] ReadFloatArray()
    {
        (byte[] raw, int length) = ReadArrayBytes(4);
        var result = new float[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(i * 4));
        }

        return result;
    }

    private int[] ReadIntArray()
    {
        (byte[] raw, int length) = ReadArrayBytes(4);
        var result = new int[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(i * 4));
        }

        return result;
    }

    private long[] ReadLongArray()
    {
        (byte[] raw, int length) = ReadArrayBytes(8);
        var result = new long[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = BinaryPrimitives.ReadInt64LittleEndian(raw.AsSpan(i * 8));
        }

        return result;
    }

    private bool[] ReadBoolArray()
    {
        (byte[] raw, int length) = ReadArrayBytes(1);
        var result = new bool[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = raw[i] != 0;
        }

        return result;
    }

    private static byte[] Inflate(ReadOnlySpan<byte> compressed, int rawLength)
    {
        using var ms = new MemoryStream(compressed.ToArray());
        using var z = new ZLibStream(ms, CompressionMode.Decompress);
        var buffer = new byte[rawLength];
        int read = 0;
        while (read < rawLength)
        {
            int n = z.Read(buffer, read, rawLength - read);
            if (n == 0)
            {
                break;
            }

            read += n;
        }

        return buffer;
    }

    private string ReadString()
    {
        int len = ReadInt32();
        string s = Encoding.Latin1.GetString(_data, _pos, len); // Latin1 preserves the \0\1 name separator
        _pos += len;
        return s;
    }

    private byte[] ReadRaw()
    {
        int len = ReadInt32();
        byte[] bytes = _data.AsSpan(_pos, len).ToArray();
        _pos += len;
        return bytes;
    }

    private long ReadOffset()
    {
        long value = _offsetSize == 8
            ? BinaryPrimitives.ReadInt64LittleEndian(_data.AsSpan(_pos))
            : BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(_pos));
        _pos += _offsetSize;
        return value;
    }

    private short ReadInt16()
    {
        short v = BinaryPrimitives.ReadInt16LittleEndian(_data.AsSpan(_pos));
        _pos += 2;
        return v;
    }

    private int ReadInt32()
    {
        int v = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(_pos));
        _pos += 4;
        return v;
    }

    private long ReadInt64()
    {
        long v = BinaryPrimitives.ReadInt64LittleEndian(_data.AsSpan(_pos));
        _pos += 8;
        return v;
    }

    private float ReadSingle()
    {
        float v = BinaryPrimitives.ReadSingleLittleEndian(_data.AsSpan(_pos));
        _pos += 4;
        return v;
    }

    private double ReadDouble()
    {
        double v = BinaryPrimitives.ReadDoubleLittleEndian(_data.AsSpan(_pos));
        _pos += 8;
        return v;
    }
}
