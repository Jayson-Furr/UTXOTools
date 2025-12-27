using UTXOTools.TXOutset.IO;

namespace UTXOTools.Demonstration.Commands;

/// <summary>
/// Command to locate all P2SH scriptPubKeys and export them to a binary file.
/// </summary>
internal static class P2shCommand
{
    /// <summary>
    /// Binary file format:
    /// - Header (9 bytes):
    ///   - 4 bytes: Magic "P2SH" (0x50 0x32 0x53 0x48)
    ///   - 4 bytes: Entry count (uint32, little-endian)
    ///   - 1 byte: Flags (0x01 = includes amount)
    /// - For each entry:
    ///   - [Optional] 8 bytes: Amount in satoshis (uint64, little-endian) if --include-amount
    ///   - 20 bytes: Script hash (HASH160)
    /// </summary>
    public static int Execute(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: UTXOTools.Demonstration p2sh <input-file> <output-file> [--include-amount] [--verbose]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --include-amount    Include amount (8 bytes) before each script hash");
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

        Console.WriteLine($"Scanning for P2SH scripts in {inputPath}...");
        Console.WriteLine();

        using var reader = new UtxoFileReader(inputPath);
        var header = reader.ReadHeader();

        Console.WriteLine($"Network:    {header.Network}");
        Console.WriteLine($"Block Hash: {header.GetBlockHashHex()}");
        Console.WriteLine($"UTXOs:      {header.UtxoCount:N0}");
        Console.WriteLine();

        // Collect P2SH entries
        var p2shEntries = new List<P2shEntry>();
        ulong transactionCount = 0;
        ulong entryCount = 0;

        foreach (var tx in reader.ReadTransactions())
        {
            transactionCount++;

            foreach (var output in tx.Outputs)
            {
                entryCount++;

                if (TryExtractP2shHash(output.ScriptPubKey, out byte[]? scriptHash))
                {
                    p2shEntries.Add(new P2shEntry
                    {
                        ScriptHash = scriptHash!,
                        Amount = output.Amount,
                        TxId = tx.TxId,
                        Vout = output.Vout,
                        Height = output.Height
                    });

                    if (verbose && p2shEntries.Count <= 10)
                    {
                        Console.WriteLine($"  Found P2SH: {tx.GetTxIdHex()}:{output.Vout} - {output.Amount:N0} sats");
                    }
                }
            }

            if (transactionCount % 100000 == 0)
            {
                Console.Write($"\rScanning... {entryCount:N0} UTXOs, found {p2shEntries.Count:N0} P2SH");
            }
        }

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine($"=== P2SH Scan Results ===");
        Console.WriteLine($"Total UTXOs scanned: {entryCount:N0}");
        Console.WriteLine($"P2SH entries found:  {p2shEntries.Count:N0}");
        Console.WriteLine();

        if (p2shEntries.Count == 0)
        {
            Console.WriteLine("No P2SH entries found. Output file not created.");
            return 0;
        }

        // Write binary output
        Console.WriteLine($"Writing to {outputPath}...");
        WriteBinaryOutput(outputPath, p2shEntries, includeAmount);

        var fileInfo = new FileInfo(outputPath);
        Console.WriteLine($"Output file size: {FormatFileSize(fileInfo.Length)}");
        Console.WriteLine("Export complete.");

        return 0;
    }

    /// <summary>
    /// Tries to extract the script hash from a P2SH scriptPubKey.
    /// </summary>
    /// <param name="script">The scriptPubKey bytes.</param>
    /// <param name="scriptHash">The extracted 20-byte script hash (HASH160).</param>
    /// <returns>True if the script is P2SH and the hash was extracted.</returns>
    /// <remarks>
    /// P2SH script format (23 bytes):
    /// OP_HASH160 (0xa9) 0x14 (20 bytes) &lt;20-byte hash&gt; OP_EQUAL (0x87)
    /// </remarks>
    private static bool TryExtractP2shHash(byte[] script, out byte[]? scriptHash)
    {
        scriptHash = null;

        // P2SH: 0xa9 0x14 <20 bytes hash> 0x87
        // Length: 1 + 1 + 20 + 1 = 23 bytes
        if (script.Length == 23 &&
            script[0] == 0xa9 &&   // OP_HASH160
            script[1] == 0x14 &&   // Push 20 bytes
            script[22] == 0x87)    // OP_EQUAL
        {
            scriptHash = script[2..22];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Writes the P2SH entries to a binary file.
    /// </summary>
    /// <remarks>
    /// Binary format:
    /// - Header (9 bytes):
    ///   - 4 bytes: Magic "P2SH" (0x50 0x32 0x53 0x48)
    ///   - 4 bytes: Entry count (uint32, little-endian)
    ///   - 1 byte: Flags (0x01 = includes amount)
    /// - For each entry:
    ///   - [Optional] 8 bytes: Amount in satoshis (uint64, little-endian) if --include-amount
    ///   - 20 bytes: Script hash
    /// </remarks>
    private static void WriteBinaryOutput(string outputPath, List<P2shEntry> entries, bool includeAmount)
    {
        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream);

        // Write magic bytes "P2SH"
        writer.Write((byte)'P');
        writer.Write((byte)'2');
        writer.Write((byte)'S');
        writer.Write((byte)'H');

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

            // P2SH hash is always 20 bytes, no need to write length
            writer.Write(entry.ScriptHash);
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
    /// Represents a P2SH entry with its script hash and metadata.
    /// </summary>
    private sealed class P2shEntry
    {
        public required byte[] ScriptHash { get; init; }
        public required ulong Amount { get; init; }
        public required byte[] TxId { get; init; }
        public required ulong Vout { get; init; }
        public required uint Height { get; init; }
    }
}
