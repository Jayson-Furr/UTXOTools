using System.Numerics;

namespace UTXOTools.TXOutset.Encoding;

/// <summary>
/// Provides methods for compressing and decompressing Bitcoin amounts (in satoshis).
/// Bitcoin Core uses a custom compression scheme for storing amounts efficiently.
/// </summary>
/// <remarks>
/// The compression scheme works as follows:
/// - x = 0: represents 0 satoshis
/// - x = 1 + 10*(9*n + d - 1) + e: represents n * 10^e satoshis where d is a digit 1-9
/// - x = 1 + 10*(n - 1) + 9: represents n * 10^9 satoshis (for multiples of 10^9)
/// </remarks>
public static class AmountCompression
{
    /// <summary>
    /// Decompresses a compressed amount value back to satoshis.
    /// </summary>
    /// <param name="x">The compressed amount value.</param>
    /// <returns>The decompressed amount in satoshis.</returns>
    public static ulong DecompressAmount(ulong x)
    {
        // x = 0 means 0 satoshis
        if (x == 0)
        {
            return 0;
        }

        x--;
        
        // x = 10*(9*n + d - 1) + e OR x = 10*(n - 1) + 9
        int e = (int)(x % 10);
        x /= 10;
        
        ulong n;
        if (e < 9)
        {
            // x = 9*n + d - 1
            int d = (int)(x % 9) + 1;
            x /= 9;
            // x = n
            n = x * 10 + (ulong)d;
        }
        else
        {
            // e == 9: x = n - 1
            n = x + 1;
        }

        // Multiply n by 10^e
        while (e > 0)
        {
            n *= 10;
            e--;
        }

        return n;
    }

    /// <summary>
    /// Compresses an amount in satoshis for storage.
    /// </summary>
    /// <param name="n">The amount in satoshis.</param>
    /// <returns>The compressed amount value.</returns>
    public static ulong CompressAmount(ulong n)
    {
        if (n == 0)
        {
            return 0;
        }

        // Count trailing zeros (powers of 10)
        int e = 0;
        while (((n % 10) == 0) && e < 9)
        {
            n /= 10;
            e++;
        }

        if (e < 9)
        {
            // Standard case: encode the last digit separately
            int d = (int)(n % 10);
            n /= 10;
            return 1 + (n * 9 + (ulong)(d - 1)) * 10 + (ulong)e;
        }
        else
        {
            // Special case for multiples of 10^9
            return 1 + (n - 1) * 10 + 9;
        }
    }
}
