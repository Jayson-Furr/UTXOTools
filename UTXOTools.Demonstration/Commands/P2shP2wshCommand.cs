using UTXOTools.TXOutset.IO;

namespace UTXOTools.Demonstration.Commands;

/// <summary>
/// Command to locate all P2SH scriptPubKeys that could potentially be P2SH-P2WSH 
/// (P2WSH wrapped in P2SH) and export them to a binary file.
/// </summary>
/// <remarks>
/// <para>
/// P2SH-P2WSH outputs are indistinguishable from other P2SH outputs at the scriptPubKey level.
/// The difference is only revealed when spending, as the redeem script would be:
/// OP_0 0x20 &lt;32-byte witness script hash&gt; (34 bytes total)
/// </para>
/// <para>
/// Since the UTXO set only contains scriptPubKeys (not redeem scripts), this command extracts
/// all P2SH outputs. The output can be used to match against a known set of P2SH-P2WSH 
/// script hashes if available from other sources.
/// </para>
/// </remarks>
internal static class P2shP2wshCommand
{
    /// <summary>
    /// Binary file format:
    /// - Header (9 bytes):
    ///   - 4 bytes: Magic "SHWS" (0x53 0x48 0x57 0x53)
    ///   - 4 bytes: Entry count (uint32, little-endian)
    ///   - 1 byte: Flags (0x01 = includes amount)
    /// - For each entry:
    ///   - [Optional] 8 bytes: Amount in satoshis (uint64, little-endian) if --include-amount
    ///   - 20 bytes: Script hash (HASH160 of the redeem script)
    /// </summary>
    public static int Execute(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: UTXOTools.Demonstration p2sh-p2wsh <input-file> <output-file> [--include-amount] [--verbose]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --include-amount    Include amount (8 bytes) before each script hash");
            Console.Error.WriteLine("  --verbose           Show detailed progress");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Note: P2SH-P2WSH outputs cannot be distinguished from other P2SH outputs");
            Console.Error.WriteLine("      at the scriptPubKey level. This command extracts all P2SH outputs.");
            Console.Error.WriteLine("      The redeem script (which identifies P2SH-P2WSH) is only revealed");
            Console.Error.WriteLine("      when the output is spent.");
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

        Console.WriteLine($"Scanning for P2SH scripts (potential P2SH-P2WSH) in {inputPath}...");
        Console.WriteLine();
        Console.WriteLine("Note: P2SH-P2WSH cannot be distinguished from other P2SH at scriptPubKey level.");
        Console.WriteLine("      This extracts all P2SH outputs for external matching.");
        Console.WriteLine();

        using var reader = new UtxoFileReader(inputPath);
        var header = reader.ReadHeader();

        Console.WriteLine($"Network:    {header.Network}");
        Console.WriteLine($"Block Hash: {header.GetBlockHashHex()}");
        Console.WriteLine($"UTXOs:      {header.UtxoCount:N0}");
        Console.WriteLine();

        // Collect P2SH entries (potential P2SH-P2WSH)
        var entries = new List<P2shP2wshEntry>();
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
                    entries.Add(new P2shP2wshEntry
                    {
                        ScriptHash = scriptHash!,
                        Amount = output.Amount,
                        TxId = tx.TxId,
                        Vout = output.Vout,
                        Height = output.Height
                    });

                    if (verbose && entries.Count <= 10)
                    {
                        Console.WriteLine($"  Found P2SH: {tx.GetTxIdHex()}:{output.Vout} - {output.Amount:N0} sats");
                    }
                }
            }

            if (transactionCount % 100000 == 0)
            {
                Console.Write($"\rScanning... {entryCount:N0} UTXOs, found {entries.Count:N0} P2SH");
            }
        }

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine($"=== P2SH-P2WSH Scan Results ===");
        Console.WriteLine($"Total UTXOs scanned:      {entryCount:N0}");
        Console.WriteLine($"P2SH entries found:       {entries.Count:N0}");
        Console.WriteLine($"(Potential P2SH-P2WSH - requires external verification)");
        Console.WriteLine();

        if (entries.Count == 0)
        {
            Console.WriteLine("No P2SH entries found. Output file not created.");
            return 0;
        }

        // Write binary output
        Console.WriteLine($"Writing to {outputPath}...");
        WriteBinaryOutput(outputPath, entries, includeAmount);

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
    /// 
    /// For P2SH-P2WSH, the hash is HASH160 of the redeem script:
    /// OP_0 (0x00) 0x20 (32 bytes) &lt;32-byte witness script hash&gt;
    /// 
    /// However, this cannot be determined from the scriptPubKey alone.
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
    /// Writes the P2SH-P2WSH entries to a binary file.
    /// </summary>
    /// <remarks>
    /// Binary format:
    /// - Header (9 bytes):
    ///   - 4 bytes: Magic "SHWS" (0x53 0x48 0x57 0x53)
    ///   - 4 bytes: Entry count (uint32, little-endian)
    ///   - 1 byte: Flags (0x01 = includes amount)
    /// - For each entry:
    ///   - [Optional] 8 bytes: Amount in satoshis (uint64, little-endian) if --include-amount
    ///   - 20 bytes: Script hash
    /// </remarks>
    private static void WriteBinaryOutput(string outputPath, List<P2shP2wshEntry> entries, bool includeAmount)
    {
        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream);

        // Write magic bytes "SHWS" (SH-WSh)
        writer.Write((byte)'S');
        writer.Write((byte)'H');
        writer.Write((byte)'W');
        writer.Write((byte)'S');

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

            // Script hash is always 20 bytes, no need to write length
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
    /// Represents a P2SH-P2WSH entry with its script hash and metadata.
    /// </summary>
    private sealed class P2shP2wshEntry
    {
        public required byte[] ScriptHash { get; init; }
        public required ulong Amount { get; init; }
        public required byte[] TxId { get; init; }
        public required ulong Vout { get; init; }
        public required uint Height { get; init; }
    }
}
