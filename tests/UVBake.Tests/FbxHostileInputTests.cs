using System.Buffers.Binary;
using System.Text;
using UVBake.IO;
using Xunit;

namespace UVBake.Tests;

/// <summary>
/// The FBX parser treats input as untrusted. Each hand-built hostile file below must be rejected quickly
/// with <see cref="InvalidDataException"/>: no hang, no stack overflow, no runaway allocation.
/// </summary>
public class FbxHostileInputTests
{
    private const int Version = 7400; // 32-bit record offsets

    [Fact(Timeout = 10_000)]
    public async Task Deeply_nested_records_are_rejected_without_stack_overflow()
    {
        // 100,000 records, each nested inside the previous one. Without a depth cap this recursion
        // overflows the stack, which .NET cannot catch and which kills the whole process.
        const int levels = 100_000;
        const int recordHeader = 13 + 1; // 3 offsets + name length + 1-byte name
        var file = Header(27 + (levels * recordHeader));
        for (int i = 0; i < levels; i++)
        {
            file.AddRange(Record(endOffset: (uint)(27 + (levels * recordHeader)), numProperties: 0, name: "A"));
        }

        await AssertRejectedAsync(file);
    }

    [Fact(Timeout = 10_000)]
    public async Task Record_whose_end_offset_points_backwards_is_rejected_without_hanging()
    {
        // The end offset points into the header, before the record itself. Following it would move
        // the parser backwards and loop forever.
        var file = Header(0);
        file.AddRange(Record(endOffset: 5, numProperties: 0, name: "A"));
        file.AddRange(new byte[13]); // a null record after it, as a real file would have
        await AssertRejectedAsync(file);
    }

    [Fact(Timeout = 10_000)]
    public async Task Compressed_array_claiming_a_huge_size_is_rejected_before_allocating()
    {
        // A double array declaring 10 million elements (80 MB, under the size cap) backed by 4 compressed
        // bytes: more than DEFLATE could ever expand to, so it is a decompression bomb.
        var property = new List<byte> { (byte)'d' };
        property.AddRange(Int32(10_000_000)); // element count
        property.AddRange(Int32(1));           // encoding: zlib
        property.AddRange(Int32(4));           // compressed length
        property.AddRange(new byte[] { 0x78, 0x9C, 0x03, 0x00 });
        InvalidDataException ex = await AssertRejectedAsync(SingleRecordWith(property));
        Assert.Contains("impossible expanded size", ex.Message); // refused before inflating, not after
    }

    [Fact(Timeout = 10_000)]
    public async Task Array_length_beyond_the_size_cap_is_rejected()
    {
        var property = new List<byte> { (byte)'l' };
        property.AddRange(Int32(int.MaxValue)); // ~17 GB of longs
        property.AddRange(Int32(0));            // encoding: raw
        property.AddRange(Int32(0));
        await AssertRejectedAsync(SingleRecordWith(property));
    }

    [Fact(Timeout = 10_000)]
    public async Task String_with_negative_length_is_rejected()
    {
        var property = new List<byte> { (byte)'S' };
        property.AddRange(Int32(-1));
        await AssertRejectedAsync(SingleRecordWith(property));
    }

    [Fact(Timeout = 10_000)]
    public async Task Record_running_past_end_of_file_is_rejected_with_InvalidDataException()
    {
        // A record header claiming to end at byte 1000 in a 40-byte file.
        var file = Header(0);
        file.AddRange(Record(endOffset: 1000, numProperties: 0, name: ""));
        await AssertRejectedAsync(file);
    }

    private static async Task<InvalidDataException> AssertRejectedAsync(List<byte> file)
    {
        byte[] bytes = file.ToArray();
        return await Task.Run(() =>
        {
            using var stream = new MemoryStream(bytes);
            return Assert.Throws<InvalidDataException>(() => FbxReader.Read(stream));
        });
    }

    private static List<byte> SingleRecordWith(List<byte> property)
    {
        var file = Header(0);
        uint end = (uint)(file.Count + 13 + 1 + property.Count);
        file.AddRange(Record(end, numProperties: 1, name: "A"));
        file.AddRange(property);
        file.AddRange(new byte[13]);
        return file;
    }

    private static List<byte> Header(int sizeHint)
    {
        var header = new List<byte>(Math.Max(sizeHint, 64));
        header.AddRange(Encoding.ASCII.GetBytes("Kaydara FBX Binary  "));
        header.Add(0x00);
        header.Add(0x1A);
        header.Add(0x00);
        header.AddRange(Int32(Version));
        return header; // 27 bytes
    }

    private static IEnumerable<byte> Record(uint endOffset, uint numProperties, string name)
    {
        var bytes = new List<byte>();
        bytes.AddRange(UInt32(endOffset));
        bytes.AddRange(UInt32(numProperties));
        bytes.AddRange(UInt32(0)); // property-list length (unused by the parser)
        bytes.Add((byte)name.Length);
        bytes.AddRange(Encoding.ASCII.GetBytes(name));
        return bytes;
    }

    private static byte[] Int32(int v)
    {
        var b = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(b, v);
        return b;
    }

    private static byte[] UInt32(uint v)
    {
        var b = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, v);
        return b;
    }
}
