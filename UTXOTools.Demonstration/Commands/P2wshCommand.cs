using UTXOTools.TXOutset.IO;

namespace UTXOTools.Demonstration.Commands;

/// <summary>
/// Command to locate all P2WSH scriptPubKeys and export them to a binary file.
/// </summary>
internal static class P2wshCommand
{
    /// <summary>
    /// Binary file format:
    /// - Header (9 bytes):
    ///   - 4 bytes: Magic "PWSH" (0x50 0x57 0x53 0x48)
    ///   - 4 bytes: Entry count (uint32, little-endian)
    ///   - 1 byte: Flags (0x01 = includes amount)
    /// - For each entry:
    ///   - [Optional] 8 bytes: Amount in satoshis (uint64, little-endian) if --include-amount
    ///   - 32 bytes: Witness script hash (SHA256)
    /// </summary>
    public static int Execute(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: UTXOTools.Demonstration p2wsh <input-file> <output-file> [--include-amount] [--verbose]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --include-amount    Include amount (8 bytes) before each witness script hash");
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

        Console.WriteLine($"Scanning for P2WSH scripts in {inputPath}...");
        Console.WriteLine();

        using var reader = new UtxoFileReader(inputPath);
        var header = reader.ReadHeader();

        Console.WriteLine($"Network:    {header.Network}");
        Console.WriteLine($"Block Hash: {header.GetBlockHashHex()}");
        Console.WriteLine($"UTXOs:      {header.UtxoCount:N0}");
        Console.WriteLine();

        // Collect P2WSH entries
        var p2wshEntries = new List<P2wshEntry>();
        ulong transactionCount = 0;
        ulong entryCount = 0;

        foreach (var tx in reader.ReadTransactions())
        {
            transactionCount++;

            foreach (var output in tx.Outputs)
            {
                entryCount++;

                if (TryExtractP2wshHash(output.ScriptPubKey, out byte[]? witnessScriptHash))
                {
                    p2wshEntries.Add(new P2wshEntry
                    {
                        WitnessScriptHash = witnessScriptHash!,
                        Amount = output.Amount,
                        TxId = tx.TxId,
                        Vout = output.Vout,
                        Height = output.Height
                    });

                    if (verbose && p2wshEntries.Count <= 10)
                    {
                        Console.WriteLine($"  Found P2WSH: {tx.GetTxIdHex()}:{output.Vout} - {output.Amount:N0} sats");
                    }
                }
            }

            if (transactionCount % 100000 == 0)
            {
                Console.Write($"\rScanning... {entryCount:N0} UTXOs, found {p2wshEntries.Count:N0} P2WSH");
            }
        }

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine($"=== P2WSH Scan Results ===");
        Console.WriteLine($"Total UTXOs scanned: {entryCount:N0}");
        Console.WriteLine($"P2WSH entries found: {p2wshEntries.Count:N0}");
        Console.WriteLine();

        if (p2wshEntries.Count == 0)
        {
            Console.WriteLine("No P2WSH entries found. Output file not created.");
            return 0;
        }

        // Write binary output
        Console.WriteLine($"Writing to {outputPath}...");
        WriteBinaryOutput(outputPath, p2wshEntries, includeAmount);

        var fileInfo = new FileInfo(outputPath);
        Console.WriteLine($"Output file size: {FormatFileSize(fileInfo.Length)}");
        Console.WriteLine("Export complete.");

        return 0;
    }

    /// <summary>
    /// Tries to extract the witness script hash from a P2WSH scriptPubKey.
    /// </summary>
    /// <param name="script">The scriptPubKey bytes.</param>
    /// <param name="witnessScriptHash">The extracted 32-byte witness script hash (SHA256).</param>
    /// <returns>True if the script is P2WSH and the hash was extracted.</returns>
    /// <remarks>
    /// P2WSH script format (34 bytes):
    /// OP_0 (0x00) 0x20 (32 bytes) &lt;32-byte SHA256 hash&gt;
    /// </remarks>
    private static bool TryExtractP2wshHash(byte[] script, out byte[]? witnessScriptHash)
    {
        witnessScriptHash = null;

        // P2WSH: 0x00 0x20 <32 bytes hash>
        // Length: 1 + 1 + 32 = 34 bytes
        if (script.Length == 34 &&
            script[0] == 0x00 &&   // OP_0 (witness version 0)
            script[1] == 0x20)     // Push 32 bytes
        {
            witnessScriptHash = script[2..34];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Writes the P2WSH entries to a binary file.
    /// </summary>
    /// <remarks>
    /// Binary format:
    /// - Header (9 bytes):
    ///   - 4 bytes: Magic "PWSH" (0x50 0x57 0x53 0x48)
    ///   - 4 bytes: Entry count (uint32, little-endian)
    ///   - 1 byte: Flags (0x01 = includes amount)
    /// - For each entry:
    ///   - [Optional] 8 bytes: Amount in satoshis (uint64, little-endian) if --include-amount
    ///   - 32 bytes: Witness script hash
    /// </remarks>
    private static void WriteBinaryOutput(string outputPath, List<P2wshEntry> entries, bool includeAmount)
    {
        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream);

        // Write magic bytes "PWSH"
        writer.Write((byte)'P');
        writer.Write((byte)'W');
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

            // P2WSH hash is always 32 bytes, no need to write length
            writer.Write(entry.WitnessScriptHash);
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
    /// Represents a P2WSH entry with its witness script hash and metadata.
    /// </summary>
    private sealed class P2wshEntry
    {
        public required byte[] WitnessScriptHash { get; init; }
        public required ulong Amount { get; init; }
        public required byte[] TxId { get; init; }
        public required ulong Vout { get; init; }
        public required uint Height { get; init; }
    }
}
