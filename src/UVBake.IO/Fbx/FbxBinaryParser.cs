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
/// <remarks>
/// Input is treated as untrusted: every length and offset read from the file is checked before it is used,
/// nesting depth and array sizes are capped, and any malformed file fails with
/// <see cref="InvalidDataException"/> rather than hanging, overflowing the stack, or exhausting memory.
/// </remarks>
internal sealed class FbxBinaryParser
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("Kaydara FBX Binary  ");

    // Hardening limits for untrusted input. Real FBX files nest well under 20 levels deep.
    private const int MaxDepth = 64;
    private const long MaxArrayBytes = 512L * 1024 * 1024;
    private const long MaxDeflateRatio = 1032; // DEFLATE's theoretical maximum expansion

    private readonly byte[] _data;
    private int _pos;
    private int _offsetSize;

    private FbxBinaryParser(byte[] data)
    {
        _data = data;
    }

    /// <summary>Parses a stream into a synthetic root node whose children are the top-level records.</summary>
    /// <exception cref="InvalidDataException">The stream is not a well-formed binary FBX file.</exception>
    public static FbxNode Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        try
        {
            return new FbxBinaryParser(ms.ToArray()).ParseDocument();
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or OverflowException)
        {
            // Truncated or inconsistent records surface as a single, catchable error type.
            throw new InvalidDataException("malformed binary FBX file.", ex);
        }
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
            FbxNode? node = ReadNode(0);
            if (node is null)
            {
                break;
            }

            root.Children.Add(node);
        }

        return root;
    }

    private FbxNode? ReadNode(int depth)
    {
        if (depth > MaxDepth)
        {
            throw new InvalidDataException($"FBX nesting is deeper than {MaxDepth} levels.");
        }

        int nodeStart = _pos;
        long endOffset = ReadOffset();
        long numProperties = ReadOffset();
        ReadOffset(); // property-list length (unused — we read each property)
        Require(1);
        int nameLen = _data[_pos++];
        if (endOffset == 0)
        {
            return null; // null record (3*offsetSize+1 zero bytes) terminates a child list / the document
        }

        // A record must end after it starts and inside the file. This also guarantees forward progress,
        // so a hostile offset can never send the parser backwards into an endless loop.
        if (endOffset <= nodeStart || endOffset > _data.Length)
        {
            throw new InvalidDataException($"FBX record at {nodeStart} has an invalid end offset {endOffset}.");
        }

        // Every property takes at least one byte, so a larger count cannot be honest.
        if (numProperties < 0 || numProperties > endOffset - _pos)
        {
            throw new InvalidDataException($"FBX record at {nodeStart} claims {numProperties} properties.");
        }

        Require(nameLen);
        var node = new FbxNode(Encoding.ASCII.GetString(_data, _pos, nameLen));
        _pos += nameLen;

        for (long i = 0; i < numProperties; i++)
        {
            node.Properties.Add(ReadProperty());
        }

        while (_pos < endOffset)
        {
            FbxNode? child = ReadNode(depth + 1);
            if (child is null)
            {
                break;
            }

            node.Children.Add(child);
        }

        if (_pos > endOffset)
        {
            throw new InvalidDataException($"FBX record at {nodeStart} overruns its end offset.");
        }

        _pos = (int)endOffset;
        return node;
    }

    private object ReadProperty()
    {
        Require(1);
        char type = (char)_data[_pos++];
        switch (type)
        {
            case 'Y': return (long)ReadInt16();
            case 'C': Require(1); return _data[_pos++] != 0;
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
        long rawBytes = (long)length * elementSize;
        if (length < 0 || rawBytes > MaxArrayBytes)
        {
            throw new InvalidDataException($"FBX array length {length} is out of range.");
        }

        Require(compressedLength);
        ReadOnlySpan<byte> payload = _data.AsSpan(_pos, compressedLength);
        _pos += compressedLength;

        byte[] raw;
        if (encoding == 0)
        {
            if (compressedLength != rawBytes)
            {
                throw new InvalidDataException("FBX raw array size does not match its element count.");
            }

            raw = payload.ToArray();
        }
        else if (encoding == 1)
        {
            // Refuse decompression bombs: the declared size must be reachable from the compressed bytes.
            if (rawBytes > (compressedLength * MaxDeflateRatio) + 1024)
            {
                throw new InvalidDataException("FBX compressed array claims an impossible expanded size.");
            }

            raw = Inflate(payload, (int)rawBytes);
        }
        else
        {
            throw new InvalidDataException($"unknown FBX array encoding {encoding}.");
        }

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

        if (read != rawLength)
        {
            throw new InvalidDataException("FBX compressed array is shorter than its declared size.");
        }

        return buffer;
    }

    private string ReadString()
    {
        int len = ReadInt32();
        Require(len);
        string s = Encoding.Latin1.GetString(_data, _pos, len); // Latin1 preserves the \0\1 name separator
        _pos += len;
        return s;
    }

    private byte[] ReadRaw()
    {
        int len = ReadInt32();
        Require(len);
        byte[] bytes = _data.AsSpan(_pos, len).ToArray();
        _pos += len;
        return bytes;
    }

    /// <summary>Throws unless <paramref name="count"/> bytes remain at the current position.</summary>
    private void Require(int count)
    {
        if (count < 0 || count > _data.Length - _pos)
        {
            throw new InvalidDataException($"FBX field at {_pos} claims {count} bytes past the end of the file.");
        }
    }

    private long ReadOffset()
    {
        Require(_offsetSize);
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
