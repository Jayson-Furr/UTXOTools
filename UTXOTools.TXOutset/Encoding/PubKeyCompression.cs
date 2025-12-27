using System.Numerics;

namespace UTXOTools.TXOutset.Encoding;

/// <summary>
/// Provides methods for compressing and decompressing secp256k1 public keys.
/// Used for decompressing public keys stored in UTXO scriptPubKeys.
/// </summary>
public static class PubKeyCompression
{
    /// <summary>
    /// The prime field for secp256k1 curve: p = 2^256 - 2^32 - 977
    /// </summary>
    private static readonly BigInteger P = BigInteger.Parse(
        "115792089237316195423570985008687907853269984665640564039457584007908834671663");

    /// <summary>
    /// The 'b' coefficient for secp256k1: y^2 = x^3 + 7
    /// </summary>
    private const int B = 7;

    /// <summary>
    /// Determines whether the specified coordinates represent a valid uncompressed public key on the secp256k1 elliptic
    /// curve.
    /// </summary>
    /// <remarks>This method checks that the provided coordinates are within the valid field range and satisfy
    /// the secp256k1 curve equation. It does not verify whether the point is the point at infinity or whether it is a
    /// valid public key in a broader cryptographic context.</remarks>
    /// <param name="x">The x-coordinate of the point to validate. Must be in the range [0, p-1], where p is the prime order of the
    /// field.</param>
    /// <param name="y">The y-coordinate of the point to validate. Must be in the range [0, p-1], where p is the prime order of the
    /// field.</param>
    /// <returns>true if the (x, y) coordinates form a valid point on the secp256k1 curve; otherwise, false.</returns>
    public static bool IsValidUncompressedPubKey(BigInteger x, BigInteger y)
    {
        // Check if x and y are within the field range [0, p-1]
        if (x < 0 || x >= P || y < 0 || y >= P)
        {
            return false;
        }

        // Check the curve equation: y^2 mod p == (x^3 + 7) mod p
        BigInteger left = BigInteger.ModPow(y, 2, P);
        BigInteger right = (BigInteger.ModPow(x, 3, P) + B) % P;

        // Return whether the equation holds
        return left == right;
    }

    /// <summary>
    /// Determines whether the specified byte array represents a valid uncompressed public key in the expected format.
    /// </summary>
    /// <remarks>This method checks that the input array is the correct length and format for an uncompressed
    /// public key, and that the X and Y coordinates represent a valid point on the underlying elliptic curve. The
    /// method does not validate compressed or hybrid public key formats.</remarks>
    /// <param name="uncompressedPubKey">A byte array containing the uncompressed public key to validate. The array must be 65 bytes in length, with the
    /// first byte as the prefix (0x04) followed by the 32-byte X and Y coordinates.</param>
    /// <returns>true if the input is a valid uncompressed public key; otherwise, false.</returns>
    public static bool IsValidUncompressedPubKey(byte[] uncompressedPubKey)
    {
        // Check length
        if (uncompressedPubKey.Length != 65)
        {
            return false;
        }

        // Check prefix byte
        if (uncompressedPubKey[0] != 0x04)
        {
            return false;
        }

        // Extract x and y coordinates
        BigInteger x = new BigInteger(uncompressedPubKey.AsSpan(1, 32), isUnsigned: true, isBigEndian: true);
        BigInteger y = new BigInteger(uncompressedPubKey.AsSpan(33, 32), isUnsigned: true, isBigEndian: true);

        // Validate the point
        return IsValidUncompressedPubKey(x, y);
    }

    /// <summary>
    /// Determines whether the specified byte array represents a valid compressed public key in the expected format.
    /// </summary>
    /// <remarks>A valid compressed public key must be 33 bytes long and begin with either 0x02 or 0x03. The
    /// method also verifies that the key decompresses to a valid point on the underlying elliptic curve.</remarks>
    /// <param name="compressedPubKey">A byte array containing the compressed public key to validate. Must be 33 bytes in length, with the first byte
    /// equal to 0x02 or 0x03.</param>
    /// <returns>true if the input is a valid compressed public key; otherwise, false.</returns>
    public static bool IsValidCompressedPubKey(byte[] compressedPubKey)
    {
        // Check length
        if (compressedPubKey.Length != 33)
        {
            return false;
        }
        // Check prefix byte
        if (compressedPubKey[0] != 0x02 && compressedPubKey[0] != 0x03)
        {
            return false;
        }
        // Decompress the key
        if (!TryDecompressPubKey(compressedPubKey, out byte[]? uncompressedPubKey))
        {
            return false;
        }
        // Validate the point
        return IsValidUncompressedPubKey(uncompressedPubKey!);
    }

    /// <summary>
    /// Determines whether the specified x-coordinate and prefix represent a valid compressed public key.
    /// </summary>
    /// <remarks>This method checks the format and validity of a compressed public key as defined by the SEC1
    /// standard. The prefix byte specifies whether the y-coordinate is even (0x02) or odd (0x03).</remarks>
    /// <param name="xCoordinate">A 32-byte array containing the x-coordinate of the public key. The array must be exactly 32 bytes in length.</param>
    /// <param name="prefix">The prefix byte indicating the parity of the y-coordinate. Must be either 0x02 or 0x03 for valid compressed
    /// public keys.</param>
    /// <returns>true if the combination of x-coordinate and prefix forms a valid compressed public key; otherwise, false.</returns>
    public static bool IsValidCompressedPubKey(byte[] xCoordinate, byte prefix)
    {
        // Check length
        if (xCoordinate.Length != 32)
        {
            return false;
        }
        // Build compressed key
        byte[] compressed = new byte[33];
        compressed[0] = prefix;
        Array.Copy(xCoordinate, 0, compressed, 1, 32);
        // Validate compressed key
        return IsValidCompressedPubKey(compressed);
    }

    /// <summary>
    /// Attempts to decompress a 33-byte compressed public key to a 65-byte uncompressed public key.
    /// </summary>
    /// <param name="compressedPubKey">The 33-byte compressed public key (prefix + x-coordinate).</param>
    /// <param name="uncompressedPubKey">When this method returns true, contains the 65-byte uncompressed public key (0x04 + x + y); otherwise, null.</param>
    /// <returns>True if the decompression succeeded; otherwise, false.</returns>
    public static bool TryDecompressPubKey(byte[] compressedPubKey, out byte[]? uncompressedPubKey, bool validateOnCurve = true)
    {
        uncompressedPubKey = null;

        // Validate input
        if (compressedPubKey.Length != 33)
        {
            return false;
        }

        // Check prefix
        byte prefix = compressedPubKey[0];
        if (prefix != 0x02 && prefix != 0x03)
        {
            return false;
        }

        // Extract x-coordinate (big-endian)
        BigInteger x = new(compressedPubKey.AsSpan(1, 32), isUnsigned: true, isBigEndian: true);

        // Calculate y^2 = x^3 + 7 (mod p)
        BigInteger ySquared = (BigInteger.ModPow(x, 3, P) + B) % P;

        // Calculate y using modular square root: y = y_squared^((p+1)/4) mod p
        // This works because p ? 3 (mod 4) for secp256k1
        BigInteger y = BigInteger.ModPow(ySquared, (P + 1) / 4, P);

        if (validateOnCurve)
        {
            // Verify the point is on the curve
            BigInteger left = BigInteger.ModPow(y, 2, P);
            BigInteger right = (BigInteger.ModPow(x, 3, P) + B) % P;
            if (left != right)
            {
                return false;
            }

            // Verify the square root is correct
            if ((y * y) % P != ySquared)
            {
                return false;
            }
        }        

        // Adjust y based on prefix (0x02 = even y, 0x03 = odd y)
        bool yIsOdd = !y.IsEven;
        bool needsOddY = prefix == 0x03;

        if (yIsOdd != needsOddY)
        {
            y = P - y;
        }

        // Build uncompressed public key: 0x04 + x (32 bytes) + y (32 bytes)
        uncompressedPubKey = new byte[65];
        uncompressedPubKey[0] = 0x04;

        byte[] xBytes = x.ToByteArray(isUnsigned: true, isBigEndian: true);
        byte[] yBytes = y.ToByteArray(isUnsigned: true, isBigEndian: true);

        // Pad and copy x (right-aligned in 32 bytes)
        Array.Copy(xBytes, 0, uncompressedPubKey, 1 + (32 - xBytes.Length), xBytes.Length);

        // Pad and copy y (right-aligned in 32 bytes)
        Array.Copy(yBytes, 0, uncompressedPubKey, 33 + (32 - yBytes.Length), yBytes.Length);

        return true;
    }

    /// <summary>
    /// Attempts to decompress a 32-byte x-coordinate with a given prefix to a 65-byte uncompressed public key.
    /// </summary>
    /// <param name="xCoordinate">The 32-byte x-coordinate.</param>
    /// <param name="prefix">The compression prefix (0x02 for even y, 0x03 for odd y).</param>
    /// <param name="uncompressedPubKey">When this method returns true, contains the 65-byte uncompressed public key (0x04 + x + y); otherwise, null.</param>
    /// <returns>True if the decompression succeeded; otherwise, false.</returns>
    public static bool TryDecompressPubKey(byte[] xCoordinate, byte prefix, out byte[]? uncompressedPubKey, bool validateOnCurve = true)
    {
        uncompressedPubKey = null;

        // Validate input
        if (xCoordinate.Length != 32)
        {
            return false;
        }

        // Build compressed key
        byte[] compressedPubKey = new byte[33];
        compressedPubKey[0] = prefix;
        Array.Copy(xCoordinate, 0, compressedPubKey, 1, 32);

        // Decompress using existing method
        return TryDecompressPubKey(compressedPubKey, out uncompressedPubKey, validateOnCurve);
    }

    /// <summary>
    /// Attempts to convert an uncompressed secp256k1 public key to its compressed form.
    /// </summary>
    /// <remarks>The method validates the format of the input public key before attempting compression. If the
    /// input does not represent a valid uncompressed secp256k1 public key, the method returns false and sets the output
    /// to null.</remarks>
    /// <param name="uncompressedPubKey">A byte array containing the uncompressed public key. Must be 65 bytes in length and start with 0x04.</param>
    /// <param name="compressedPubKey">When this method returns, contains the compressed public key as a 33-byte array if the conversion succeeds;
    /// otherwise, null.</param>
    /// <returns>true if the conversion is successful and the public key is valid; otherwise, false.</returns>
    public static bool TryCompressPubKey(byte[] uncompressedPubKey, out byte[]? compressedPubKey)
    {
        compressedPubKey = null;

        // Validate input
        if (uncompressedPubKey.Length != 65 || uncompressedPubKey[0] != 0x04)
        {
            return false;
        }

        // Extract x and y coordinates
        BigInteger x = new BigInteger(uncompressedPubKey.AsSpan(1, 32), isUnsigned: true, isBigEndian: true);
        BigInteger y = new BigInteger(uncompressedPubKey.AsSpan(33, 32), isUnsigned: true, isBigEndian: true);

        // Determine prefix based on y parity
        byte prefix = (byte)(y.IsEven ? 0x02 : 0x03);

        // Build compressed public key
        compressedPubKey = new byte[33];
        compressedPubKey[0] = prefix;
        byte[] xBytes = x.ToByteArray(isUnsigned: true, isBigEndian: true);
        Array.Copy(xBytes, 0, compressedPubKey, 1 + (32 - xBytes.Length), xBytes.Length);
        return true;
    }
}
