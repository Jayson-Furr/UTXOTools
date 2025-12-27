using UTXOTools.TXOutset.IO;

namespace UTXOTools.Demonstration.Commands;

/// <summary>
/// Command to locate all P2PKH scriptPubKeys and export them to a binary file.
/// </summary>
internal static class P2pkhCommand
{
    /// <summary>
    /// Binary file format:
    /// - Header (9 bytes):
    ///   - 4 bytes: Magic "P2KH" (0x50 0x32 0x4B 0x48)
    ///   - 4 bytes: Entry count (uint32, little-endian)
    ///   - 1 byte: Flags (0x01 = includes amount)
    /// - For each entry:
    ///   - [Optional] 8 bytes: Amount in satoshis (uint64, little-endian) if --include-amount
    ///   - 20 bytes: Public key hash (HASH160)
    /// </summary>
    public static int Execute(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: UTXOTools.Demonstration p2pkh <input-file> <output-file> [--include-amount] [--verbose]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --include-amount    Include amount (8 bytes) before each public key hash");
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

        Console.WriteLine($"Scanning for P2PKH scripts in {inputPath}...");
        Console.WriteLine();

        using var reader = new UtxoFileReader(inputPath);
        var header = reader.ReadHeader();

        Console.WriteLine($"Network:    {header.Network}");
        Console.WriteLine($"Block Hash: {header.GetBlockHashHex()}");
        Console.WriteLine($"UTXOs:      {header.UtxoCount:N0}");
        Console.WriteLine();

        // Collect P2PKH entries
        var p2pkhEntries = new List<P2pkhEntry>();
        ulong transactionCount = 0;
        ulong entryCount = 0;

        foreach (var tx in reader.ReadTransactions())
        {
            transactionCount++;

            foreach (var output in tx.Outputs)
            {
                entryCount++;

                if (TryExtractP2pkhHash(output.ScriptPubKey, out byte[]? pubKeyHash))
                {
                    p2pkhEntries.Add(new P2pkhEntry
                    {
                        PubKeyHash = pubKeyHash!,
                        Amount = output.Amount,
                        TxId = tx.TxId,
                        Vout = output.Vout,
                        Height = output.Height
                    });

                    if (verbose && p2pkhEntries.Count <= 10)
                    {
                        Console.WriteLine($"  Found P2PKH: {tx.GetTxIdHex()}:{output.Vout} - {output.Amount:N0} sats");
                    }
                }
            }

            if (transactionCount % 100000 == 0)
            {
                Console.Write($"\rScanning... {entryCount:N0} UTXOs, found {p2pkhEntries.Count:N0} P2PKH");
            }
        }

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine($"=== P2PKH Scan Results ===");
        Console.WriteLine($"Total UTXOs scanned: {entryCount:N0}");
        Console.WriteLine($"P2PKH entries found: {p2pkhEntries.Count:N0}");
        Console.WriteLine();

        if (p2pkhEntries.Count == 0)
        {
            Console.WriteLine("No P2PKH entries found. Output file not created.");
            return 0;
        }

        // Write binary output
        Console.WriteLine($"Writing to {outputPath}...");
        WriteBinaryOutput(outputPath, p2pkhEntries, includeAmount);

        var fileInfo = new FileInfo(outputPath);
        Console.WriteLine($"Output file size: {FormatFileSize(fileInfo.Length)}");
        Console.WriteLine("Export complete.");

        return 0;
    }

    /// <summary>
    /// Tries to extract the public key hash from a P2PKH scriptPubKey.
    /// </summary>
    /// <param name="script">The scriptPubKey bytes.</param>
    /// <param name="pubKeyHash">The extracted 20-byte public key hash (HASH160).</param>
    /// <returns>True if the script is P2PKH and the hash was extracted.</returns>
    /// <remarks>
    /// P2PKH script format (25 bytes):
    /// OP_DUP (0x76) OP_HASH160 (0xa9) 0x14 (20 bytes) &lt;20-byte hash&gt; OP_EQUALVERIFY (0x88) OP_CHECKSIG (0xac)
    /// </remarks>
    private static bool TryExtractP2pkhHash(byte[] script, out byte[]? pubKeyHash)
    {
        pubKeyHash = null;

        // P2PKH: 0x76 0xa9 0x14 <20 bytes hash> 0x88 0xac
        // Length: 1 + 1 + 1 + 20 + 1 + 1 = 25 bytes
        if (script.Length == 25 &&
            script[0] == 0x76 &&   // OP_DUP
            script[1] == 0xa9 &&   // OP_HASH160
            script[2] == 0x14 &&   // Push 20 bytes
            script[23] == 0x88 &&  // OP_EQUALVERIFY
            script[24] == 0xac)    // OP_CHECKSIG
        {
            pubKeyHash = script[3..23];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Writes the P2PKH entries to a binary file.
    /// </summary>
    /// <remarks>
    /// Binary format:
    /// - Header (9 bytes):
    ///   - 4 bytes: Magic "P2KH" (0x50 0x32 0x4B 0x48)
    ///   - 4 bytes: Entry count (uint32, little-endian)
    ///   - 1 byte: Flags (0x01 = includes amount)
    /// - For each entry:
    ///   - [Optional] 8 bytes: Amount in satoshis (uint64, little-endian) if --include-amount
    ///   - 20 bytes: Public key hash
    /// </remarks>
    private static void WriteBinaryOutput(string outputPath, List<P2pkhEntry> entries, bool includeAmount)
    {
        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream);

        // Write magic bytes "P2KH"
        writer.Write((byte)'P');
        writer.Write((byte)'2');
        writer.Write((byte)'K');
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

            // P2PKH hash is always 20 bytes, no need to write length
            writer.Write(entry.PubKeyHash);
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
    /// Represents a P2PKH entry with its public key hash and metadata.
    /// </summary>
    private sealed class P2pkhEntry
    {
        public required byte[] PubKeyHash { get; init; }
        public required ulong Amount { get; init; }
        public required byte[] TxId { get; init; }
        public required ulong Vout { get; init; }
        public required uint Height { get; init; }
    }
}
