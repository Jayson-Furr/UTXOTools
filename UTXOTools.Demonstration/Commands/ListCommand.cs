using System.Text.Json;
using UTXOTools.TXOutset.IO;
using UTXOTools.TXOutset.Models;

namespace UTXOTools.Demonstration.Commands;

/// <summary>
/// Command to list UTXO entries from a file.
/// </summary>
internal static class ListCommand
{
    public static int Execute(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: UTXOTools.Demonstration list <file> [--limit n] [--offset n] [--txid hex] [--json|--csv]");
            return 1;
        }

        string filePath = args[0];
        int limit = 10;
        int offset = 0;
        string? txidFilter = null;
        string format = "text";

        // Parse options
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--limit" when i + 1 < args.Length:
                    limit = int.Parse(args[++i]);
                    break;
                case "--offset" when i + 1 < args.Length:
                    offset = int.Parse(args[++i]);
                    break;
                case "--txid" when i + 1 < args.Length:
                    txidFilter = args[++i].ToLowerInvariant();
                    break;
                case "--json":
                    format = "json";
                    break;
                case "--csv":
                    format = "csv";
                    break;
            }
        }

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: File not found: {filePath}");
            return 1;
        }

        using var reader = new UtxoFileReader(filePath);
        reader.ReadHeader();

        var entries = reader.ReadEntries()
            .Where(e => txidFilter == null || e.GetTxIdHex().Contains(txidFilter))
            .Skip(offset)
            .Take(limit)
            .ToList();

        switch (format)
        {
            case "json":
                OutputJson(entries);
                break;
            case "csv":
                OutputCsv(entries);
                break;
            default:
                OutputText(entries);
                break;
        }

        return 0;
    }

    private static void OutputText(List<UtxoEntry> entries)
    {
        Console.WriteLine($"Showing {entries.Count} entries:");
        Console.WriteLine();

        foreach (var entry in entries)
        {
            Console.WriteLine($"Outpoint:    {entry.GetOutpoint()}");
            Console.WriteLine($"  Amount:    {entry.Amount:N0} sats ({entry.GetAmountInBtc():F8} BTC)");
            Console.WriteLine($"  Height:    {entry.Height}");
            Console.WriteLine($"  Coinbase:  {entry.IsCoinbase}");
            Console.WriteLine($"  Script:    {TruncateScript(entry.GetScriptPubKeyHex())}");
            Console.WriteLine();
        }
    }

    private static void OutputJson(List<UtxoEntry> entries)
    {
        var output = entries.Select(e => new
        {
            txid = e.GetTxIdHex(),
            vout = e.Vout,
            amount = e.Amount,
            amountBtc = e.GetAmountInBtc(),
            height = e.Height,
            isCoinbase = e.IsCoinbase,
            scriptPubKey = e.GetScriptPubKeyHex()
        });

        var options = new JsonSerializerOptions { WriteIndented = true };
        Console.WriteLine(JsonSerializer.Serialize(output, options));
    }

    private static void OutputCsv(List<UtxoEntry> entries)
    {
        Console.WriteLine("txid,vout,amount_sats,amount_btc,height,is_coinbase,script_pubkey");
        foreach (var entry in entries)
        {
            Console.WriteLine($"{entry.GetTxIdHex()},{entry.Vout},{entry.Amount},{entry.GetAmountInBtc():F8},{entry.Height},{entry.IsCoinbase},{entry.GetScriptPubKeyHex()}");
        }
    }

    private static string TruncateScript(string script)
    {
        if (script.Length <= 60)
            return script;
        return script[..30] + "..." + script[^30..];
    }
}
