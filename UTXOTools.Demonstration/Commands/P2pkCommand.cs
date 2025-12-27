using UTXOTools.TXOutset.Encoding;
using UTXOTools.TXOutset.IO;

namespace UTXOTools.Demonstration.Commands;

/// <summary>
/// Command to locate all P2PK scriptPubKeys and export them to a binary file.
/// </summary>
internal static class P2pkCommand
{
    /// <summary>
    /// Binary file format:
    /// - 4 bytes: Entry count (uint32, little-endian)
    /// - For each entry:
    ///   - 1 byte: Public key type (0x21 = compressed 33 bytes, 0x41 = uncompressed 65 bytes)
    ///   - N bytes: Public key (33 or 65 bytes depending on type)
    /// </summary>
    public static int Execute(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: UTXOTools.Demonstration p2pk <input-file> <output-file> [--include-amount] [--verbose]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --include-amount    Include amount (8 bytes) before each public key");
            Console.Error.WriteLine("  --verbose           Show detailed progress");
            return 1;
        }

        string inputPath = args[0];
        string outputPath = args[1];
        bool includeAmount = args.Contains("--include-amount");
        bool verbose = args.Contains("--verbose");

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Error: Input file not found: {inputPath}");
            return 1;
        }

        Console.WriteLine($"Scanning for P2PK scripts in {inputPath}...");
        Console.WriteLine();

        using var reader = new UtxoFileReader(inputPath);
        var header = reader.ReadHeader();

        Console.WriteLine($"Network:    {header.Network}");
        Console.WriteLine($"Block Hash: {header.GetBlockHashHex()}");
        Console.WriteLine($"UTXOs:      {header.UtxoCount:N0}");
        Console.WriteLine();

        // Collect P2PK entries
        var p2pkEntries = new List<P2pkEntry>();
        ulong transactionCount = 0;
        ulong entryCount = 0;
        ulong compressedCount = 0;
        ulong uncompressedCount = 0;

        foreach (var tx in reader.ReadTransactions())
        {
            transactionCount++;

            foreach (var output in tx.Outputs)
            {
                entryCount++;

                if (TryExtractP2pkPublicKey(output.ScriptPubKey, out byte[]? publicKey, out bool isCompressed))
                {
                    p2pkEntries.Add(new P2pkEntry
                    {
                        PublicKey = publicKey!,
                        IsCompressed = isCompressed,
                        Amount = output.Amount,
                        TxId = tx.TxId,
                        Vout = output.Vout,
                        Height = output.Height
                    });

                    if (isCompressed)
                        compressedCount++;
                    else
                        uncompressedCount++;

                    if (verbose && p2pkEntries.Count <= 10)
                    {
                        Console.WriteLine($"  Found P2PK: {tx.GetTxIdHex()}:{output.Vout} - {output.Amount:N0} sats ({(isCompressed ? "compressed" : "uncompressed")})");
                    }
                }
            }

            if (transactionCount % 100000 == 0)
            {
                Console.Write($"\rScanning... {entryCount:N0} UTXOs, found {p2pkEntries.Count:N0} P2PK");
            }
        }

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine($"=== P2PK Scan Results ===");
        Console.WriteLine($"Total UTXOs scanned: {entryCount:N0}");
        Console.WriteLine($"P2PK entries found:  {p2pkEntries.Count:N0}");
        Console.WriteLine($"  Compressed (33b):  {compressedCount:N0}");
        Console.WriteLine($"  Uncompressed (65b): {uncompressedCount:N0}");
        Console.WriteLine();

        if (p2pkEntries.Count == 0)
        {
            Console.WriteLine("No P2PK entries found. Output file not created.");
            return 0;
        }

        // Write binary output
        Console.WriteLine($"Writing to {outputPath}...");
        WriteBinaryOutput(outputPath, p2pkEntries, includeAmount);

        var fileInfo = new FileInfo(outputPath);
        Console.WriteLine($"Output file size: {FormatFileSize(fileInfo.Length)}");
        Console.WriteLine("Export complete.");

        return 0;
    }

    /// <summary>
    /// Tries to extract the public key from a P2PK scriptPubKey.
    /// </summary>
    /// <param name="script">The scriptPubKey bytes.</param>
    /// <param name="publicKey">The extracted public key (33 or 65 bytes).</param>
    /// <param name="isCompressed">True if the public key is compressed (33 bytes).</param>
    /// <returns>True if the script is P2PK and the public key was extracted.</returns>
    private static bool TryExtractP2pkPublicKey(byte[] script, out byte[]? publicKey, out bool isCompressed)
    {
        publicKey = null;
        isCompressed = false;

        // P2PK compressed: 0x21 <33 bytes pubkey> 0xac (OP_CHECKSIG)
        if (script.Length == 35 && script[0] == 0x21 && script[34] == 0xac)
        {
            byte prefix = script[1];
            if (prefix == 0x02 || prefix == 0x03)
            {
                publicKey = script[1..34];
                if (PubKeyCompression.TryDecompressPubKey(publicKey, out byte[]? decompressedPubKey))
                {
                    // Sanity check the decompressed public key
                    if (decompressedPubKey == null)
                    {
                        return false;
                    }

                    if (PubKeyCompression.IsValidUncompressedPubKey(decompressedPubKey))
                    {
                        isCompressed = true;
                        return true;
                    }
                }
                return false;
            }
        }

        // P2PK uncompressed: 0x41 <65 bytes pubkey> 0xac (OP_CHECKSIG)
        if (script.Length == 67 && script[0] == 0x41 && script[66] == 0xac)
        {
            if (script[1] == 0x04)
            {
                publicKey = script[1..66];
                isCompressed = false;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Writes the P2PK entries to a binary file.
    /// </summary>
    /// <remarks>
    /// Binary format:
    /// - Header (8 bytes):
    ///   - 4 bytes: Magic "P2PK" (0x50 0x32 0x50 0x4B)
    ///   - 4 bytes: Entry count (uint32, little-endian)
    /// - For each entry:
    ///   - [Optional] 8 bytes: Amount in satoshis (uint64, little-endian) if --include-amount
    ///   - 1 byte: Public key length (33 or 65)
    ///   - N bytes: Public key
    /// </remarks>
    private static void WriteBinaryOutput(string outputPath, List<P2pkEntry> entries, bool includeAmount)
    {
        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream);

        // Write magic bytes "P2PK"
        writer.Write((byte)'P');
        writer.Write((byte)'2');
        writer.Write((byte)'P');
        writer.Write((byte)'K');

        // Write entry count
        writer.Write((uint)entries.Count);

        // Write flags byte (for future extensibility)
        byte flags = 0;
        if (includeAmount) flags |= 0x01;
        writer.Write(flags);

        // Write each entry
        foreach (var entry in entries)
        {
            if (includeAmount)
            {
                writer.Write(entry.Amount);
            }

            writer.Write((byte)entry.PublicKey.Length);
            writer.Write(entry.PublicKey);
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Represents a P2PK entry with its public key and metadata.
    /// </summary>
    private sealed class P2pkEntry
    {
        public required byte[] PublicKey { get; init; }
        public required bool IsCompressed { get; init; }
        public required ulong Amount { get; init; }
        public required byte[] TxId { get; init; }
        public required ulong Vout { get; init; }
        public required uint Height { get; init; }
    }
}
