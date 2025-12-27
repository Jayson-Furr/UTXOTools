using System.Text.Json;
using UTXOTools.TXOutset.IO;
using UTXOTools.TXOutset.Models;

namespace UTXOTools.Demonstration.Commands;

/// <summary>
/// Command to export UTXO data to various formats.
/// </summary>
internal static class ExportCommand
{
    public static int Execute(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: UTXOTools.Demonstration export <input-file> <output-file> [--format json|csv] [--limit n]");
            return 1;
        }

        string inputPath = args[0];
        string outputPath = args[1];
        string format = "json";
        int? limit = null;

        // Parse options
        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--format" when i + 1 < args.Length:
                    format = args[++i].ToLowerInvariant();
                    break;
                case "--limit" when i + 1 < args.Length:
                    limit = int.Parse(args[++i]);
                    break;
            }
        }

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Error: Input file not found: {inputPath}");
            return 1;
        }

        Console.WriteLine($"Exporting UTXO data to {outputPath}...");

        using var reader = new UtxoFileReader(inputPath);
        var header = reader.ReadHeader();

        IEnumerable<UtxoEntry> entries = reader.ReadEntries();
        if (limit.HasValue)
        {
            entries = entries.Take(limit.Value);
        }

        switch (format)
        {
            case "json":
                ExportJson(outputPath, header, entries, limit);
                break;
            case "csv":
                ExportCsv(outputPath, entries, limit);
                break;
            default:
                Console.Error.WriteLine($"Unknown format: {format}. Supported formats: json, csv");
                return 1;
        }

        Console.WriteLine("Export complete.");
        return 0;
    }

    private static void ExportJson(string outputPath, UtxoFileHeader header, IEnumerable<UtxoEntry> entries, int? limit)
    {
        using var stream = File.Create(outputPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();

        // Write header info
        writer.WritePropertyName("header");
        writer.WriteStartObject();
        writer.WriteNumber("version", header.Version);
        writer.WriteString("network", header.Network.ToString());
        writer.WriteString("blockHash", header.GetBlockHashHex());
        writer.WriteNumber("utxoCount", header.UtxoCount);
        writer.WriteEndObject();

        // Write entries
        writer.WritePropertyName("entries");
        writer.WriteStartArray();

        int count = 0;
        foreach (var entry in entries)
        {
            writer.WriteStartObject();
            writer.WriteString("txid", entry.GetTxIdHex());
            writer.WriteNumber("vout", entry.Vout);
            writer.WriteNumber("amount", entry.Amount);
            writer.WriteNumber("amountBtc", entry.GetAmountInBtc());
            writer.WriteNumber("height", entry.Height);
            writer.WriteBoolean("isCoinbase", entry.IsCoinbase);
            writer.WriteString("scriptPubKey", entry.GetScriptPubKeyHex());
            writer.WriteEndObject();

            count++;
            if (count % 100000 == 0)
            {
                Console.WriteLine($"  Exported {count:N0} entries...");
            }
        }

        writer.WriteEndArray();
        writer.WriteNumber("exportedCount", count);
        writer.WriteEndObject();

        Console.WriteLine($"  Total entries exported: {count:N0}");
    }

    private static void ExportCsv(string outputPath, IEnumerable<UtxoEntry> entries, int? limit)
    {
        using var writer = new StreamWriter(outputPath);
        writer.WriteLine("txid,vout,amount_sats,amount_btc,height,is_coinbase,script_pubkey");

        int count = 0;
        foreach (var entry in entries)
        {
            writer.WriteLine($"{entry.GetTxIdHex()},{entry.Vout},{entry.Amount},{entry.GetAmountInBtc():F8},{entry.Height},{entry.IsCoinbase},{entry.GetScriptPubKeyHex()}");

            count++;
            if (count % 100000 == 0)
            {
                Console.WriteLine($"  Exported {count:N0} entries...");
            }
        }

        Console.WriteLine($"  Total entries exported: {count:N0}");
    }
}
