namespace UTXOTools.TXOutset.Encoding;

/// <summary>
/// Provides methods for reading and writing CompactSize encoded values.
/// CompactSize is a variable-length integer encoding used in Bitcoin for sizes and counts.
/// </summary>
/// <remarks>
/// CompactSize encoding:
/// - Values 0x00-0xFC: stored as a single byte
/// - Values 0xFD-0xFFFF: 0xFD followed by 2 bytes (little-endian)
/// - Values 0x10000-0xFFFFFFFF: 0xFE followed by 4 bytes (little-endian)
/// - Values 0x100000000-0xFFFFFFFFFFFFFFFF: 0xFF followed by 8 bytes (little-endian)
/// </remarks>
public static class CompactSizeEncoding
{
    /// <summary>
    /// The maximum allowed size for CompactSize values (same as Bitcoin Core's MAX_SIZE).
    /// </summary>
    public const ulong MaxSize = 0x02000000; // 32 MB

    /// <summary>
    /// Reads a CompactSize encoded unsigned 64-bit integer from the stream.
    /// </summary>
    /// <param name="reader">The binary reader to read from.</param>
    /// <param name="rangeCheck">If true, throws if the value exceeds MaxSize.</param>
    /// <returns>The decoded unsigned 64-bit integer.</returns>
    /// <exception cref="InvalidDataException">Thrown when the encoding is non-canonical or value exceeds MaxSize.</exception>
    public static ulong ReadCompactSize(BinaryReader reader, bool rangeCheck = true)
    {
        byte chSize = reader.ReadByte();
        ulong nSizeRet;

        if (chSize < 253)
        {
            nSizeRet = chSize;
        }
        else if (chSize == 253)
        {
            nSizeRet = reader.ReadUInt16();
            if (nSizeRet < 253)
            {
                throw new InvalidDataException("Non-canonical CompactSize encoding.");
            }
        }
        else if (chSize == 254)
        {
            nSizeRet = reader.ReadUInt32();
            if (nSizeRet < 0x10000u)
            {
                throw new InvalidDataException("Non-canonical CompactSize encoding.");
            }
        }
        else // chSize == 255
        {
            nSizeRet = reader.ReadUInt64();
            if (nSizeRet < 0x100000000UL)
            {
                throw new InvalidDataException("Non-canonical CompactSize encoding.");
            }
        }

        if (rangeCheck && nSizeRet > MaxSize)
        {
            throw new InvalidDataException($"CompactSize value {nSizeRet} exceeds maximum allowed size {MaxSize}.");
        }

        return nSizeRet;
    }

    /// <summary>
    /// Writes a CompactSize encoded unsigned 64-bit integer to the stream.
    /// </summary>
    /// <param name="writer">The binary writer to write to.</param>
    /// <param name="value">The value to encode and write.</param>
    public static void WriteCompactSize(BinaryWriter writer, ulong value)
    {
        if (value < 253)
        {
            writer.Write((byte)value);
        }
        else if (value <= 0xFFFF)
        {
            writer.Write((byte)253);
            writer.Write((ushort)value);
        }
        else if (value <= 0xFFFFFFFF)
        {
            writer.Write((byte)254);
            writer.Write((uint)value);
        }
        else
        {
            writer.Write((byte)255);
            writer.Write(value);
        }
    }

    /// <summary>
    /// Gets the number of bytes required to encode a value in CompactSize format.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>The number of bytes required for the CompactSize encoding.</returns>
    public static int GetCompactSizeLength(ulong value)
    {
        if (value < 253)
            return 1;
        if (value <= 0xFFFF)
            return 3;
        if (value <= 0xFFFFFFFF)
            return 5;
        return 9;
    }
}
