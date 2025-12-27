using UTXOTools.TXOutset.Encoding;

namespace UTXOTools.TXOutset.Scripts;

/// <summary>
/// Provides static methods for decoding and encoding Bitcoin script public keys (scriptPubKey) to and from standardized
/// formats, supporting common script types such as Pay-to-PubKey-Hash (P2PKH), Pay-to-Script-Hash (P2SH), Pay-to-PubKey
/// (P2PK) compressed and uncompressed, and raw scripts.
/// </summary>
/// <remarks>This class is intended for use in applications that need to parse, analyze, or serialize Bitcoin-like
/// script public keys. All methods are thread-safe and do not modify shared state. The decoding and encoding operations
/// are designed to be robust against malformed or incomplete data, returning failure indicators rather than throwing
/// exceptions for invalid input. The class supports both standard script patterns and raw scripts, enabling
/// interoperability with a wide range of Bitcoin transaction formats.</remarks>
public static class ScriptPubKeyDecoder
{
    // Script template constants
    // Pay-to-PubKey-Hash (P2PKH)
    private static readonly byte[] P2PkhPrefix = [0x76, 0xa9, 0x14]; // OP_DUP OP_HASH160 <20 bytes>
    private static readonly byte[] P2PkhSuffix = [0x88, 0xac];       // OP_EQUALVERIFY OP_CHECKSIG

    // Pay-to-Script-Hash (P2SH)
    private static readonly byte[] P2ShPrefix = [0xa9, 0x14];        // OP_HASH160 <20 bytes>
    private static readonly byte[] P2ShSuffix = [0x87];              // OP_EQUAL

    // Pay-to-PubKey (P2PK)
    private static readonly byte[] P2PkCompressedSuffix = [0xac];   // OP_CHECKSIG
    private static readonly byte[] P2PkUncompressedSuffix = [0xac]; // OP_CHECKSIG

    /// <summary>
    /// Hash length for P2PKH and P2SH scripts.
    /// </summary>
    private const int HashLength = 20;

    /// <summary>
    /// Compressed public key x-coordinate length.
    /// </summary>
    private const int CompressedPubKeyDataLength = 32;

    /// <summary>
    /// Full compressed public key length (prefix + x-coordinate).
    /// </summary>
    private const int CompressedPubKeyLength = 33;

    /// <summary>
    /// Full uncompressed public key length.
    /// </summary>
    private const int UncompressedPubKeyLength = 65;

    /// <summary>
    /// Attempts to decode a Bitcoin scriptPubKey from the specified binary reader using recognized script types.
    /// </summary>
    /// <remarks>This method supports decoding standard Bitcoin scriptPubKey types, including
    /// Pay-to-PubKey-Hash (P2PKH), Pay-to-Script-Hash (P2SH), and Pay-to-PubKey (compressed and uncompressed). If the
    /// script type is not recognized, the method attempts to decode it as a raw script. The reader's position advances
    /// past the decoded script data if decoding is successful.</remarks>
    /// <param name="reader">The binary reader from which to read the encoded scriptPubKey data. The reader must be positioned at the start
    /// of the scriptPubKey encoding.</param>
    /// <param name="script">When this method returns, contains the decoded scriptPubKey as a byte array if decoding was successful;
    /// otherwise, null.</param>
    /// <returns>true if the scriptPubKey was successfully decoded and assigned to script; otherwise, false.</returns>
    public static bool TryDecodeScriptPubKey(BinaryReader reader, out byte[]? script)
    {
        script = null;

        // Read size/type byte as VarInt
        ulong size = VarIntEncoding.ReadVarInt(reader);

        // Decode based on size/type
        return size switch
        {
            0x00 => TryDecodeP2PKH(reader, out script),                         // Pay-to-PubKey-Hash
            0x01 => TryDecodeP2SH(reader, out script),                          // Pay-to-Script-Hash
            0x02 => TryDecodeP2PKCompressed(reader, 0x02, out script, false),   // P2PK compressed (even)
            0x03 => TryDecodeP2PKCompressed(reader, 0x03, out script, false),   // P2PK compressed (odd)
            0x04 => TryDecodeP2PKUncompressed(reader, 0x02, out script, false), // P2PK uncompressed (from even)
            0x05 => TryDecodeP2PKUncompressed(reader, 0x03, out script, false), // P2PK uncompressed (from odd)
            _ => TryDecodeRawScript(reader, (int)size - 6, out script)          // Raw script
        };
    }

    /// <summary>
    /// Attempts to decode a Pay-to-PubKey-Hash (P2PKH) script from the provided binary reader.
    /// </summary>
    /// <remarks>This method does not advance the reader if decoding fails. The returned script follows the
    /// standard P2PKH format: OP_DUP OP_HASH160 <20-byte hash> OP_EQUALVERIFY OP_CHECKSIG.</remarks>
    /// <param name="reader">The binary reader from which to read the P2PKH hash. The reader must be positioned at the start of the hash
    /// data.</param>
    /// <param name="script">When this method returns, contains the decoded P2PKH script if decoding succeeds; otherwise, null.</param>
    /// <returns>true if a valid P2PKH script is successfully decoded; otherwise, false.</returns>
    private static bool TryDecodeP2PKH(BinaryReader reader, out byte[]? script)
    {
        script = null;

        // Read hash
        byte[] hash = reader.ReadBytes(HashLength);

        // Validate length
        if (hash.Length != HashLength)
        {
            // Invalid length
            return false;
        }

        // Create script: OP_DUP OP_HASH160 <20 bytes> OP_EQUALVERIFY OP_CHECKSIG
        script = new byte[P2PkhPrefix.Length + HashLength + P2PkhSuffix.Length];
        P2PkhPrefix.CopyTo(script, 0);
        hash.CopyTo(script, P2PkhPrefix.Length);
        P2PkhSuffix.CopyTo(script, P2PkhPrefix.Length + HashLength);

        // Successful validation
        return true;
    }

    /// <summary>
    /// Attempts to decode a Pay-to-Script-Hash (P2SH) script from the specified binary reader.
    /// </summary>
    /// <remarks>The method reads a fixed-length hash from the reader and constructs the corresponding P2SH
    /// script. If the reader does not contain enough bytes, the method returns false and the out parameter is set to
    /// null.</remarks>
    /// <param name="reader">The binary reader from which to read the P2SH hash. The reader must be positioned at the start of the hash data.</param>
    /// <param name="script">When this method returns, contains the decoded P2SH script if the operation succeeds; otherwise, null. This
    /// parameter is passed uninitialized.</param>
    /// <returns>true if the P2SH script was successfully decoded and assigned to the out parameter; otherwise, false.</returns>
    private static bool TryDecodeP2SH(BinaryReader reader, out byte[]? script)
    {
        script = null;

        // Read hash
        byte[] hash = reader.ReadBytes(HashLength);

        // Validate length
        if (hash.Length != HashLength)
        {
            // Invalid length
            return false;
        }

        // Create script: OP_HASH160 <20 bytes> OP_EQUAL
        script = new byte[P2ShPrefix.Length + HashLength + P2ShSuffix.Length];
        P2ShPrefix.CopyTo(script, 0);
        hash.CopyTo(script, P2ShPrefix.Length);
        P2ShSuffix.CopyTo(script, P2ShPrefix.Length + HashLength);

        // Successful validation
        return true;
    }

    /// <summary>
    /// Attempts to decode a compressed Pay-to-PubKey (P2PK) script from the specified binary reader using the given
    /// prefix.
    /// </summary>
    /// <remarks>This method does not advance the reader if decoding fails. The output script is formatted as
    /// a standard compressed P2PK script, which includes the compressed public key and the OP_CHECKSIG
    /// opcode.</remarks>
    /// <param name="reader">The binary reader from which to read the compressed public key data.</param>
    /// <param name="prefix">The prefix byte indicating the parity of the public key (typically 0x02 or 0x03 for compressed keys).</param>
    /// <param name="script">When this method returns, contains the decoded P2PK script if decoding succeeds; otherwise, null.</param>
    /// <param name="validateOnCurve">true to validate that the public key is on the expected elliptic curve; otherwise, false. The default is true.</param>
    /// <returns>true if a valid compressed P2PK script is successfully decoded; otherwise, false.</returns>
    private static bool TryDecodeP2PKCompressed(BinaryReader reader, byte prefix, out byte[]? script, bool validateOnCurve = true)
    {
        script = null;

        // Read x-coordinate
        byte[] xCoord = reader.ReadBytes(CompressedPubKeyDataLength);

        // Validate length
        if (xCoord.Length != CompressedPubKeyDataLength)
        {
            // Invalid length
            return false;
        }

        // Validate compressed pubkey
        if (validateOnCurve)
        {
            // Validate the compressed public key
            if (!PubKeyCompression.IsValidCompressedPubKey(xCoord, prefix))
            {
                // Validation failed
                return false;
            }
        }

        // <33 bytes compressed pubkey> OP_CHECKSIG
        // Format: 0x21 (push 33 bytes) + prefix + x-coord + OP_CHECKSIG
        // Create script
        script = new byte[1 + CompressedPubKeyLength + P2PkCompressedSuffix.Length];
        script[0] = 0x21; // Push 33 bytes
        script[1] = prefix;
        xCoord.CopyTo(script, 2);
        P2PkCompressedSuffix.CopyTo(script, 1 + CompressedPubKeyLength);

        // Successful validation
        return true;
    }

    /// <summary>
    /// Attempts to decode a Pay-to-PubKey (P2PK) uncompressed public key script from the provided binary reader using
    /// the specified compressed prefix.
    /// </summary>
    /// <remarks>This method does not throw exceptions for invalid or malformed input; instead, it returns
    /// false if decoding fails. The resulting script, if successful, is formatted as a standard uncompressed P2PK
    /// script suitable for use in Bitcoin-like systems.</remarks>
    /// <param name="reader">The binary reader from which to read the compressed public key data. The reader's position will advance by the
    /// number of bytes read.</param>
    /// <param name="compressedPrefix">The expected prefix byte indicating the compression type of the public key (typically 0x02 or 0x03 for
    /// compressed keys).</param>
    /// <param name="script">When this method returns, contains the decoded P2PK uncompressed script if the operation succeeds; otherwise,
    /// null. This parameter is passed uninitialized.</param>
    /// <param name="validateOnCurve">true to validate that the decompressed public key lies on the expected elliptic curve; otherwise, false. The
    /// default is true.</param>
    /// <returns>true if the compressed public key was successfully decompressed and the P2PK uncompressed script was created;
    /// otherwise, false.</returns>
    private static bool TryDecodeP2PKUncompressed(BinaryReader reader, byte compressedPrefix, out byte[]? script, bool validateOnCurve = true)
    {
        script = null;

        // Read x-coordinate
        byte[] xCoord = reader.ReadBytes(CompressedPubKeyDataLength);

        // Validate length
        if (xCoord.Length != CompressedPubKeyDataLength)
        {
            // Invalid length
            return false;
        }

        // Decompress the public key
        if (PubKeyCompression.TryDecompressPubKey(xCoord, compressedPrefix, out byte[]? uncompressedPubKey, validateOnCurve))
        {
            // <65 bytes uncompressed pubkey> OP_CHECKSIG
            // Format: 0x41 (push 65 bytes) + uncompressed pubkey + OP_CHECKSIG
            // Create script
            script = new byte[1 + UncompressedPubKeyLength + P2PkUncompressedSuffix.Length];
            script[0] = 0x41; // Push 65 bytes
            uncompressedPubKey!.CopyTo(script, 1);
            P2PkUncompressedSuffix.CopyTo(script, 1 + UncompressedPubKeyLength);

            // Successful decompression
            return true;
        }
        // Decompression failed
        return false;
    }

    /// <summary>
    /// Attempts to read a raw script of the specified length from the provided binary reader.
    /// </summary>
    /// <remarks>If length is zero, the out parameter is set to an empty array and the method returns <see
    /// langword="true"/>. If the reader does not have enough bytes remaining, the method returns <see
    /// langword="false"/> and the out parameter is set to null.</remarks>
    /// <param name="reader">The binary reader from which to read the script bytes. Must not be null and must be positioned at the start of
    /// the script data.</param>
    /// <param name="length">The number of bytes to read for the script. Must be zero or greater.</param>
    /// <param name="script">When this method returns, contains the byte array representing the script if the read was successful; otherwise,
    /// null.</param>
    /// <returns>true if the script was successfully read and assigned to the out parameter; otherwise, false.</returns>
    private static bool TryDecodeRawScript(BinaryReader reader, int length, out byte[]? script)
    {
        script = null;

        // Validate length
        if (length < 0)
        {
            // Invalid length
            return false;
        }

        // Handle empty script
        if (length == 0)
        {
            // Empty script
            script = [];
            return true;
        }

        // Read raw script bytes
        byte[] rawScript = reader.ReadBytes(length);

        // Validate length
        if (rawScript.Length != length)
        {
            // Invalid length
            return false;
        }

        // Assign raw script
        script = rawScript;

        // Successful read
        return true;
    }

    /// <summary>
    /// Attempts to encode the specified script public key into a standardized format if it matches a known pattern.
    /// </summary>
    /// <remarks>This method supports encoding for common Bitcoin script types, including Pay-to-PubKey-Hash
    /// (P2PKH), Pay-to-Script-Hash (P2SH), compressed and uncompressed Pay-to-PubKey (P2PK), and raw scripts of
    /// sufficient length. If the script does not match any known pattern or is too short for raw encoding, encoding
    /// fails and the output is null.</remarks>
    /// <param name="scriptPubKey">The script public key to analyze and encode. Must not be null.</param>
    /// <param name="encoded">When this method returns, contains the encoded representation of the script public key if encoding was
    /// successful; otherwise, null.</param>
    /// <returns>true if the script public key was successfully encoded into a recognized format; otherwise, false.</returns>
    public static bool TryEncodeScriptPubKey(byte[] scriptPubKey, out byte[]? encoded)
    {
        encoded = null;

        // Try to match known patterns
        if (TryMatchP2PKH(scriptPubKey, out byte[]? p2pkhHash))
        {
            // P2PKH
            if (p2pkhHash == null)
            {
                // Should not happen
                return false;
            }

            encoded = new byte[1 + HashLength];
            encoded[0] = 0x00; // P2PKH type
            p2pkhHash.CopyTo(encoded, 1);

            // Successful encoding
            return true;
        }
        else if (TryMatchP2SH(scriptPubKey, out byte[]? p2shHash))
        {
            // P2SH
            if (p2shHash == null)
            {
                // Should not happen
                return false;
            }

            encoded = new byte[1 + HashLength];
            encoded[0] = 0x01; // P2SH type
            p2shHash.CopyTo(encoded, 1);

            // Successful encoding
            return true;
        }
        else if (TryMatchP2PKCompressed(scriptPubKey, out byte[]? xCoordComp, out byte prefixComp))
        {
            // P2PK compressed
            if (xCoordComp == null)
            {
                // Should not happen
                return false;
            }

            encoded = new byte[1 + CompressedPubKeyDataLength];
            encoded[0] = prefixComp == 0x02 ? (byte)0x02 : (byte)0x03; // Compressed type
            xCoordComp.CopyTo(encoded, 1);

            // Successful encoding
            return true;
        }
        else if (TryMatchP2PKUncompressed(scriptPubKey, out byte[]? xCoordUncomp, out byte prefixUncomp))
        {
            // P2PK uncompressed
            if (xCoordUncomp == null)
            {
                // Should not happen
                return false;
            }

            encoded = new byte[1 + CompressedPubKeyDataLength];
            encoded[0] = prefixUncomp == 0x02 ? (byte)0x04 : (byte)0x05; // Uncompressed type
            xCoordUncomp.CopyTo(encoded, 1);

            // Successful encoding
            return true;
        }
        else
        {
            // Raw script
            if (scriptPubKey.Length < 6)
            {
                // Too short for raw script
                return false;
            }

            int rawLength = scriptPubKey.Length;
            encoded = new byte[1 + rawLength];
            encoded[0] = (byte)(rawLength + 6); // Raw script type
            scriptPubKey.CopyTo(encoded, 1);

            // Successful encoding
            return true;
        }
    }

    /// <summary>
    /// Attempts to match the provided script to the standard Pay-to-PubKey-Hash (P2PKH) pattern and extract the public
    /// key hash if successful.
    /// </summary>
    /// <remarks>This method does not validate the contents of the extracted hash beyond matching the P2PKH
    /// script structure. The caller should ensure that the input script is well-formed and intended for P2PKH
    /// analysis.</remarks>
    /// <param name="script">The script to examine, as a byte array. Must be non-null and of the expected length for a P2PKH script.</param>
    /// <param name="hash">When this method returns, contains the extracted 20-byte public key hash if the script matches the P2PKH
    /// pattern; otherwise, null.</param>
    /// <returns>true if the script matches the P2PKH pattern and the public key hash is extracted successfully; otherwise,
    /// false.</returns>
    private static bool TryMatchP2PKH(byte[] script, out byte[]? hash)
    {
        hash = null;

        // P2PKH: OP_DUP OP_HASH160 <20 bytes> OP_EQUALVERIFY OP_CHECKSIG
        // Calculate expected length
        int expectedLength = P2PkhPrefix.Length + HashLength + P2PkhSuffix.Length;

        // Validate length
        if (script.Length != expectedLength)
        {
            // Invalid length
            return false;
        }

        // Check prefix
        if (!script.AsSpan(0, P2PkhPrefix.Length).SequenceEqual(P2PkhPrefix))
        {
            // Prefix does not match
            return false;
        }

        // Check suffix
        if (!script.AsSpan(P2PkhPrefix.Length + HashLength).SequenceEqual(P2PkhSuffix))
        {
            // Suffix does not match
            return false;
        }

        // Extract hash
        hash = script.AsSpan(P2PkhPrefix.Length, HashLength).ToArray();

        // Successful match
        return true;
    }

    /// <summary>
    /// Attempts to determine whether the specified script matches the standard Pay-to-Script-Hash (P2SH) pattern and
    /// extracts the embedded hash if successful.
    /// </summary>
    /// <remarks>This method checks whether the script conforms to the standard P2SH format: OP_HASH160
    /// <20-byte hash> OP_EQUAL. If the script matches, the extracted hash can be used for further validation or address
    /// derivation.</remarks>
    /// <param name="script">The script bytes to examine for a P2SH pattern.</param>
    /// <param name="hash">When this method returns, contains the 20-byte hash extracted from the script if the match is successful;
    /// otherwise, null.</param>
    /// <returns>true if the script matches the P2SH pattern and the hash is extracted successfully; otherwise, false.</returns>
    private static bool TryMatchP2SH(byte[] script, out byte[]? hash)
    {
        hash = null;

        // P2SH: OP_HASH160 <20 bytes> OP_EQUAL
        // Calculate expected length
        int expectedLength = P2ShPrefix.Length + HashLength + P2ShSuffix.Length;

        if (script.Length != expectedLength)
        {
            // Invalid length
            return false;
        }

        // Check prefix
        if (!script.AsSpan(0, P2ShPrefix.Length).SequenceEqual(P2ShPrefix))
        {
            // Prefix does not match
            return false;
        }

        // Check suffix
        if (!script.AsSpan(P2ShPrefix.Length + HashLength).SequenceEqual(P2ShSuffix))
        {
            // Suffix does not match
            return false;
        }

        // Extract hash
        hash = script.AsSpan(P2ShPrefix.Length, HashLength).ToArray();

        // Successful match
        return true;
    }

    /// <summary>
    /// Attempts to match the provided script to a compressed Pay-to-PubKey (P2PK) format and extract the public key
    /// components if successful.
    /// </summary>
    /// <remarks>This method does not modify the output parameters unless the script matches the expected
    /// compressed P2PK format. The method validates both the structure of the script and the integrity of the public
    /// key data.</remarks>
    /// <param name="script">The script bytes to analyze for a compressed P2PK pattern.</param>
    /// <param name="xCoord">When this method returns, contains the 32-byte x-coordinate of the compressed public key if the script matches;
    /// otherwise, null. This parameter is passed uninitialized.</param>
    /// <param name="prefix">When this method returns, contains the prefix byte (0x02 or 0x03) of the compressed public key if the script
    /// matches; otherwise, 0. This parameter is passed uninitialized.</param>
    /// <returns>true if the script matches the compressed P2PK format and the public key components are valid; otherwise, false.</returns>
    private static bool TryMatchP2PKCompressed(byte[] script, out byte[]? xCoord, out byte prefix)
    {
        xCoord = null;
        prefix = 0;

        // P2PK compressed: 0x21 <33 bytes compressed pubkey> OP_CHECKSIG
        // Calculate expected length
        int expectedLength = 1 + CompressedPubKeyLength + P2PkCompressedSuffix.Length;

        // Validate length
        if (script.Length != expectedLength)
        {
            // Invalid length
            return false;
        }

        // Check push opcode
        if (script[0] != 0x21)
        {
            // Invalid push opcode
            return false;
        }

        // Check prefix byte
        prefix = script[1];
        if (prefix != 0x02 && prefix != 0x03)
        {
            // Invalid prefix
            return false;
        }

        // Check suffix
        if (!script.AsSpan(1 + CompressedPubKeyLength).SequenceEqual(P2PkCompressedSuffix))
        {
            // Suffix does not match
            return false;
        }

        // Extract x-coordinate
        xCoord = script.AsSpan(2, CompressedPubKeyDataLength).ToArray();

        // Validate compressed pubkey
        if (PubKeyCompression.TryDecompressPubKey(xCoord, prefix, out byte[]? decompressed))
        {
            if (decompressed == null || decompressed.Length != UncompressedPubKeyLength)
            {
                // Invalid decompressed pubkey
                return false;
            }

            // Validate uncompressed pubkey
            if (PubKeyCompression.IsValidUncompressedPubKey(decompressed))
            {
                // Successful match
                return true;
            }
        }

        // Decompression failed
        return false;        
    }

    /// <summary>
    /// Attempts to match the provided script to a Pay-to-PubKey (P2PK) uncompressed public key pattern and extract the
    /// compressed public key's x-coordinate and prefix if successful.
    /// </summary>
    /// <remarks>This method validates the script structure and public key format according to Bitcoin's P2PK
    /// uncompressed standard. If the match is successful, the output parameters provide the compressed form of the
    /// public key for further cryptographic use.</remarks>
    /// <param name="script">The script bytes to analyze for a P2PK uncompressed public key pattern. Must not be null.</param>
    /// <param name="xCoord">When this method returns <see langword="true"/>, contains the 32-byte x-coordinate of the compressed public key
    /// extracted from the script; otherwise, <see langword="null"/>.</param>
    /// <param name="prefix">When this method returns <see langword="true"/>, contains the prefix byte (0x02 or 0x03) of the compressed
    /// public key; otherwise, 0.</param>
    /// <returns>true if the script matches a valid P2PK uncompressed public key pattern and the compressed public key was
    /// successfully extracted; otherwise, false.</returns>
    private static bool TryMatchP2PKUncompressed(byte[] script, out byte[]? xCoord, out byte prefix)
    {
        xCoord = null;
        prefix = 0;

        // P2PK uncompressed: 0x41 <65 bytes uncompressed pubkey> OP_CHECKSIG
        // Calculate expected length
        int expectedLength = 1 + UncompressedPubKeyLength + P2PkUncompressedSuffix.Length;

        // Validate length
        if (script.Length != expectedLength)
        {
            // Invalid length
            return false;
        }

        // Check push opcode
        if (script[0] != 0x41)
        {
            // Invalid push opcode
            return false;
        }

        // Check uncompressed pubkey prefix
        if (script[1] != 0x04)
        {
            // Invalid uncompressed pubkey prefix
            return false;
        }

        // Check suffix
        if (!script.AsSpan(1 + UncompressedPubKeyLength).SequenceEqual(P2PkUncompressedSuffix))
        {
            // Suffix does not match
            return false;
        }

        // Extract uncompressed pubkey
        byte[] uncompressed = script.AsSpan(1, UncompressedPubKeyLength).ToArray();

        // Validate uncompressed pubkey
        if (!PubKeyCompression.IsValidUncompressedPubKey(uncompressed))
        {
            // Invalid uncompressed pubkey
            return false;
        }

        // Compress the pubkey
        if (PubKeyCompression.TryCompressPubKey(uncompressed, out byte[]? compressedPubKey))
        {
            if (compressedPubKey == null || compressedPubKey.Length != CompressedPubKeyLength)
            {
                // Invalid compressed pubkey
                return false;
            }

            // Extract prefix and x-coordinate
            prefix = compressedPubKey[0];
            xCoord = compressedPubKey.AsSpan(1, CompressedPubKeyDataLength).ToArray();
            return true;
        }
        {
            // Compression failed
            return false;
        }

    }

    /// <summary>
    /// Gets the script type name for a given compressed size byte.
    /// </summary>
    /// <param name="size">The size/type byte.</param>
    /// <returns>A human-readable script type name.</returns>
    public static string GetScriptTypeName(byte size)
    {
        return size switch
        {
            0x00 => "P2PKH",
            0x01 => "P2SH",
            0x02 => "P2PK (compressed, even)",
            0x03 => "P2PK (compressed, odd)",
            0x04 => "P2PK (uncompressed, even)",
            0x05 => "P2PK (uncompressed, odd)",
            _ => $"Raw script ({size - 6} bytes)"
        };
    }
}
