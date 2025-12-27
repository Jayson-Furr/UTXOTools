namespace UTXOTools.TXOutset.Encoding;

/// <summary>
/// Provides methods for reading and writing VarInt encoded values.
/// VarInt is a variable-length integer encoding used in Bitcoin for compact storage.
/// </summary>
/// <remarks>
/// VarInt encoding (different from CompactSize):
/// - Uses 7 bits per byte for data, with the high bit indicating continuation.
/// - If the high bit (0x80) is set, more bytes follow.
/// - Each continuation adds 1 to prevent ambiguity.
/// - Bytes are read MSB first (most significant bits first).
/// </remarks>
public static class VarIntEncoding
{
    /// <summary>
    /// Reads a VarInt encoded unsigned 64-bit integer from the stream.
    /// </summary>
    /// <param name="reader">The binary reader to read from.</param>
    /// <returns>The decoded unsigned 64-bit integer.</returns>
    /// <exception cref="InvalidDataException">Thrown when the value would overflow.</exception>
    public static ulong ReadVarInt(BinaryReader reader)
    {
        ulong n = 0;
        
        while (true)
        {
            int b = reader.ReadByte();
            ulong v = (ulong)(b & 0x7F);

            // Check shift overflow BEFORE shifting
            if (n > (ulong.MaxValue >> 7))
                throw new InvalidDataException("VarInt value too large.");

            n = (n << 7) | v;

            if ((b & 0x80) != 0)
            {
                // Check (n + 1) overflow
                if (n == ulong.MaxValue)
                    throw new InvalidDataException("VarInt value too large.");

                n++;
            }
            else
            {
                return n;
            }
        }

    }

    /// <summary>
    /// Writes a VarInt encoded unsigned 64-bit integer to the stream.
    /// </summary>
    /// <param name="writer">The binary writer to write to.</param>
    /// <param name="value">The value to encode and write.</param>
    public static void WriteVarInt(BinaryWriter writer, ulong value)
    {
        Span<byte> buffer = stackalloc byte[10]; // Max 10 bytes for 64-bit value
        int length = 0;

        // Build the encoding from LSB to MSB
        buffer[length++] = (byte)(value & 0x7F);

        while (value > 0x7F)
        {
            value = (value >> 7) - 1;
            buffer[length++] = (byte)((value & 0x7F) | 0x80);
        }

        // Write in reverse order (MSB first)
        for (int i = length - 1; i >= 0; i--)
        {
            writer.Write(buffer[i]);
        }
    }

    /// <summary>
    /// Encode a VarInt encoded unsigned 64-bit integer and return as a byte array.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    public static byte[] EncodeVarInt(ulong value)
    {
        Span<byte> buffer = stackalloc byte[10]; // Max 10 bytes for 64-bit value
        int length = 0;
        // Build the encoding from LSB to MSB
        buffer[length++] = (byte)(value & 0x7F);
        while (value > 0x7F)
        {
            value = (value >> 7) - 1;
            buffer[length++] = (byte)((value & 0x7F) | 0x80);
        }
        // Return the slice in reverse order (MSB first)
        return buffer.Slice(0, length).ToArray().Reverse().ToArray();
    }

    /// <summary>
    /// Gets the number of bytes required to encode a value in VarInt format.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>The number of bytes required for the VarInt encoding.</returns>
    public static int GetVarIntLength(ulong value)
    {
        int length = 1;
        while (value > 0x7F)
        {
            value = (value >> 7) - 1;
            length++;
        }
        return length;
    }
}
