using UTXOTools.TXOutset.IO;

namespace UTXOTools.Demonstration.Commands;

/// <summary>
/// Command to locate all P2MS (bare multisig) scriptPubKeys and export them to a binary file.
/// </summary>
internal static class P2msCommand
{
    /// <summary>
    /// Binary file format:
    /// - Header (9 bytes):
    ///   - 4 bytes: Magic "P2MS" (0x50 0x32 0x4D 0x53)
    ///   - 4 bytes: Entry count (uint32, little-endian)
    ///   - 1 byte: Flags (0x01 = includes amount)
    /// - For each entry:
    ///   - [Optional] 8 bytes: Amount in satoshis (uint64, little-endian) if --include-amount
    ///   - 1 byte: M (required signatures)
    ///   - 1 byte: N (total public keys)
    ///   - For each public key:
    ///     - 1 byte: Public key length (33 or 65)
    ///     - N bytes: Public key
    /// </summary>
    public static int Execute(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: UTXOTools.Demonstration p2ms <input-file> <output-file> [--include-amount] [--verbose]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --include-amount    Include amount (8 bytes) before each multisig entry");
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

        Console.WriteLine($"Scanning for P2MS scripts in {inputPath}...");
        Console.WriteLine();

        using var reader = new UtxoFileReader(inputPath);
        var header = reader.ReadHeader();

        Console.WriteLine($"Network:    {header.Network}");
        Console.WriteLine($"Block Hash: {header.GetBlockHashHex()}");
        Console.WriteLine($"UTXOs:      {header.UtxoCount:N0}");
        Console.WriteLine();

        // Collect P2MS entries
        var p2msEntries = new List<P2msEntry>();
        ulong transactionCount = 0;
        ulong entryCount = 0;
        var multisigTypeCounts = new Dictionary<string, ulong>();

        foreach (var tx in reader.ReadTransactions())
        {
            transactionCount++;

            foreach (var output in tx.Outputs)
            {
                entryCount++;

                if (TryExtractP2msData(output.ScriptPubKey, out byte m, out byte n, out List<byte[]>? publicKeys))
                {
                    p2msEntries.Add(new P2msEntry
                    {
                        M = m,
                        N = n,
                        PublicKeys = publicKeys!,
                        Amount = output.Amount,
                        TxId = tx.TxId,
                        Vout = output.Vout,
                        Height = output.Height
                    });

                    string typeKey = $"{m}-of-{n}";
                    if (!multisigTypeCounts.TryGetValue(typeKey, out ulong count))
                        count = 0;
                    multisigTypeCounts[typeKey] = count + 1;

                    if (verbose && p2msEntries.Count <= 10)
                    {
                        Console.WriteLine($"  Found P2MS: {tx.GetTxIdHex()}:{output.Vout} - {m}-of-{n} multisig, {output.Amount:N0} sats");
                    }
                }
            }

            if (transactionCount % 100000 == 0)
            {
                Console.Write($"\rScanning... {entryCount:N0} UTXOs, found {p2msEntries.Count:N0} P2MS");
            }
        }

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine($"=== P2MS Scan Results ===");
        Console.WriteLine($"Total UTXOs scanned: {entryCount:N0}");
        Console.WriteLine($"P2MS entries found:  {p2msEntries.Count:N0}");

        if (multisigTypeCounts.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Multisig types:");
            foreach (var kvp in multisigTypeCounts.OrderByDescending(x => x.Value))
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value:N0}");
            }
        }

        Console.WriteLine();

        if (p2msEntries.Count == 0)
        {
            Console.WriteLine("No P2MS entries found. Output file not created.");
            return 0;
        }

        // Write binary output
        Console.WriteLine($"Writing to {outputPath}...");
        WriteBinaryOutput(outputPath, p2msEntries, includeAmount);

        var fileInfo = new FileInfo(outputPath);
        Console.WriteLine($"Output file size: {FormatFileSize(fileInfo.Length)}");
        Console.WriteLine("Export complete.");

        return 0;
    }

    /// <summary>
    /// Tries to extract the multisig data from a P2MS scriptPubKey.
    /// </summary>
    /// <param name="script">The scriptPubKey bytes.</param>
    /// <param name="m">The number of required signatures.</param>
    /// <param name="n">The total number of public keys.</param>
    /// <param name="publicKeys">The list of public keys.</param>
    /// <returns>True if the script is P2MS and the data was extracted.</returns>
    /// <remarks>
    /// P2MS script format:
    /// OP_M &lt;pubkey1&gt; &lt;pubkey2&gt; ... &lt;pubkeyN&gt; OP_N OP_CHECKMULTISIG
    /// 
    /// Where:
    /// - OP_M is OP_1 (0x51) through OP_16 (0x60) representing M
    /// - Each pubkey is prefixed with its length (0x21 for compressed, 0x41 for uncompressed)
    /// - OP_N is OP_1 (0x51) through OP_16 (0x60) representing N
    /// - OP_CHECKMULTISIG is 0xae
    /// </remarks>
    private static bool TryExtractP2msData(byte[] script, out byte m, out byte n, out List<byte[]>? publicKeys)
    {
        m = 0;
        n = 0;
        publicKeys = null;

        // Minimum P2MS: OP_1 <33-byte key> OP_1 OP_CHECKMULTISIG = 1 + 34 + 1 + 1 = 37 bytes
        if (script.Length < 37)
            return false;

        // Last byte must be OP_CHECKMULTISIG (0xae)
        if (script[^1] != 0xae)
            return false;

        // First byte must be OP_1 through OP_16 (0x51-0x60)
        byte opM = script[0];
        if (opM < 0x51 || opM > 0x60)
            return false;
        m = (byte)(opM - 0x50);

        // Second-to-last byte must be OP_1 through OP_16 (0x51-0x60)
        byte opN = script[^2];
        if (opN < 0x51 || opN > 0x60)
            return false;
        n = (byte)(opN - 0x50);

        // M must be <= N
        if (m > n)
            return false;

        // Parse public keys
        publicKeys = [];
        int offset = 1;
        int expectedEnd = script.Length - 2; // Before OP_N and OP_CHECKMULTISIG

        while (offset < expectedEnd && publicKeys.Count < n)
        {
            byte keyLen = script[offset];

            // Valid key lengths: 0x21 (33 bytes compressed) or 0x41 (65 bytes uncompressed)
            if (keyLen != 0x21 && keyLen != 0x41)
                return false;

            int actualKeyLen = keyLen;
            if (offset + 1 + actualKeyLen > expectedEnd)
                return false;

            byte[] pubKey = script[(offset + 1)..(offset + 1 + actualKeyLen)];

            // Validate public key prefix
            if (actualKeyLen == 33)
            {
                // Compressed: must start with 0x02 or 0x03
                if (pubKey[0] != 0x02 && pubKey[0] != 0x03)
                    return false;
            }
            else if (actualKeyLen == 65)
            {
                // Uncompressed: must start with 0x04
                if (pubKey[0] != 0x04)
                    return false;
            }

            publicKeys.Add(pubKey);
            offset += 1 + actualKeyLen;
        }

        // Verify we parsed exactly N keys and consumed all expected bytes
        if (publicKeys.Count != n || offset != expectedEnd)
        {
            publicKeys = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Writes the P2MS entries to a binary file.
    /// </summary>
    /// <remarks>
    /// Binary format:
    /// - Header (9 bytes):
    ///   - 4 bytes: Magic "P2MS" (0x50 0x32 0x4D 0x53)
    ///   - 4 bytes: Entry count (uint32, little-endian)
    ///   - 1 byte: Flags (0x01 = includes amount)
    /// - For each entry:
    ///   - [Optional] 8 bytes: Amount in satoshis (uint64, little-endian) if --include-amount
    ///   - 1 byte: M (required signatures)
    ///   - 1 byte: N (total public keys)
    ///   - For each of N public keys:
    ///     - 1 byte: Public key length (33 or 65)
    ///     - N bytes: Public key
    /// </remarks>
    private static void WriteBinaryOutput(string outputPath, List<P2msEntry> entries, bool includeAmount)
    {
        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream);

        // Write magic bytes "P2MS"
        writer.Write((byte)'P');
        writer.Write((byte)'2');
        writer.Write((byte)'M');
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

            writer.Write(entry.M);
            writer.Write(entry.N);

            foreach (var pubKey in entry.PublicKeys)
            {
                writer.Write((byte)pubKey.Length);
                writer.Write(pubKey);
            }
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
    /// Represents a P2MS entry with its multisig data and metadata.
    /// </summary>
    private sealed class P2msEntry
    {
        public required byte M { get; init; }
        public required byte N { get; init; }
        public required List<byte[]> PublicKeys { get; init; }
        public required ulong Amount { get; init; }
        public required byte[] TxId { get; init; }
        public required ulong Vout { get; init; }
        public required uint Height { get; init; }
    }
}
