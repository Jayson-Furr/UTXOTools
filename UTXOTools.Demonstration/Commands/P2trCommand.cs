using UTXOTools.TXOutset.IO;

namespace UTXOTools.Demonstration.Commands;

/// <summary>
/// Command to locate all P2TR (Taproot) scriptPubKeys and export them to a binary file.
/// </summary>
internal static class P2trCommand
{
    /// <summary>
    /// Binary file format:
    /// - Header (9 bytes):
    ///   - 4 bytes: Magic "P2TR" (0x50 0x32 0x54 0x52)
    ///   - 4 bytes: Entry count (uint32, little-endian)
    ///   - 1 byte: Flags (0x01 = includes amount)
    /// - For each entry:
    ///   - [Optional] 8 bytes: Amount in satoshis (uint64, little-endian) if --include-amount
    ///   - 32 bytes: Taproot output key (x-only public key)
    /// </summary>
    public static int Execute(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: UTXOTools.Demonstration p2tr <input-file> <output-file> [--include-amount] [--verbose]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --include-amount    Include amount (8 bytes) before each taproot output key");
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

        Console.WriteLine($"Scanning for P2TR scripts in {inputPath}...");
        Console.WriteLine();

        using var reader = new UtxoFileReader(inputPath);
        var header = reader.ReadHeader();

        Console.WriteLine($"Network:    {header.Network}");
        Console.WriteLine($"Block Hash: {header.GetBlockHashHex()}");
        Console.WriteLine($"UTXOs:      {header.UtxoCount:N0}");
        Console.WriteLine();

        // Collect P2TR entries
        var p2trEntries = new List<P2trEntry>();
        ulong transactionCount = 0;
        ulong entryCount = 0;

        foreach (var tx in reader.ReadTransactions())
        {
            transactionCount++;

            foreach (var output in tx.Outputs)
            {
                entryCount++;

                if (TryExtractP2trOutputKey(output.ScriptPubKey, out byte[]? outputKey))
                {
                    p2trEntries.Add(new P2trEntry
                    {
                        OutputKey = outputKey!,
                        Amount = output.Amount,
                        TxId = tx.TxId,
                        Vout = output.Vout,
                        Height = output.Height
                    });

                    if (verbose && p2trEntries.Count <= 10)
                    {
                        Console.WriteLine($"  Found P2TR: {tx.GetTxIdHex()}:{output.Vout} - {output.Amount:N0} sats");
                    }
                }
            }

            if (transactionCount % 100000 == 0)
            {
                Console.Write($"\rScanning... {entryCount:N0} UTXOs, found {p2trEntries.Count:N0} P2TR");
            }
        }

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine($"=== P2TR Scan Results ===");
        Console.WriteLine($"Total UTXOs scanned: {entryCount:N0}");
        Console.WriteLine($"P2TR entries found:  {p2trEntries.Count:N0}");
        Console.WriteLine();

        if (p2trEntries.Count == 0)
        {
            Console.WriteLine("No P2TR entries found. Output file not created.");
            return 0;
        }

        // Write binary output
        Console.WriteLine($"Writing to {outputPath}...");
        WriteBinaryOutput(outputPath, p2trEntries, includeAmount);

        var fileInfo = new FileInfo(outputPath);
        Console.WriteLine($"Output file size: {FormatFileSize(fileInfo.Length)}");
        Console.WriteLine("Export complete.");

        return 0;
    }

    /// <summary>
    /// Tries to extract the taproot output key from a P2TR scriptPubKey.
    /// </summary>
    /// <param name="script">The scriptPubKey bytes.</param>
    /// <param name="outputKey">The extracted 32-byte x-only public key (taproot output key).</param>
    /// <returns>True if the script is P2TR and the output key was extracted.</returns>
    /// <remarks>
    /// P2TR script format (34 bytes):
    /// OP_1 (0x51) 0x20 (32 bytes) &lt;32-byte x-only pubkey&gt;
    /// 
    /// The x-only public key is the taproot output key, which may be:
    /// - A simple key spend (internal key with no script tree)
    /// - A tweaked key (internal key + merkle root of script tree)
    /// </remarks>
    private static bool TryExtractP2trOutputKey(byte[] script, out byte[]? outputKey)
    {
        outputKey = null;

        // P2TR: 0x51 0x20 <32 bytes x-only pubkey>
        // Length: 1 + 1 + 32 = 34 bytes
        if (script.Length == 34 &&
            script[0] == 0x51 &&   // OP_1 (witness version 1)
            script[1] == 0x20)     // Push 32 bytes
        {
            outputKey = script[2..34];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Writes the P2TR entries to a binary file.
    /// </summary>
    /// <remarks>
    /// Binary format:
    /// - Header (9 bytes):
    ///   - 4 bytes: Magic "P2TR" (0x50 0x32 0x54 0x52)
    ///   - 4 bytes: Entry count (uint32, little-endian)
    ///   - 1 byte: Flags (0x01 = includes amount)
    /// - For each entry:
    ///   - [Optional] 8 bytes: Amount in satoshis (uint64, little-endian) if --include-amount
    ///   - 32 bytes: Taproot output key (x-only public key)
    /// </remarks>
    private static void WriteBinaryOutput(string outputPath, List<P2trEntry> entries, bool includeAmount)
    {
        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream);

        // Write magic bytes "P2TR"
        writer.Write((byte)'P');
        writer.Write((byte)'2');
        writer.Write((byte)'T');
        writer.Write((byte)'R');

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

            // P2TR output key is always 32 bytes, no need to write length
            writer.Write(entry.OutputKey);
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
    /// Represents a P2TR entry with its taproot output key and metadata.
    /// </summary>
    private sealed class P2trEntry
    {
        public required byte[] OutputKey { get; init; }
        public required ulong Amount { get; init; }
        public required byte[] TxId { get; init; }
        public required ulong Vout { get; init; }
        public required uint Height { get; init; }
    }
}
