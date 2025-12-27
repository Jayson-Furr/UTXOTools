using UTXOTools.TXOutset.IO;

namespace UTXOTools.Demonstration.Commands;

/// <summary>
/// Command to display statistics about a UTXO set.
/// </summary>
internal static class StatsCommand
{
    public static int Execute(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: UTXOTools.Demonstration stats <file>");
            return 1;
        }

        string filePath = args[0];

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: File not found: {filePath}");
            return 1;
        }

        Console.WriteLine($"Analyzing {filePath}...");
        Console.WriteLine();

        using var reader = new UtxoFileReader(filePath);
        var header = reader.ReadHeader();

        ulong totalAmount = 0;
        ulong transactionCount = 0;
        ulong coinbaseCount = 0;
        ulong p2pkhCount = 0;
        ulong p2shCount = 0;
        ulong p2pkCount = 0;
        ulong otherScriptCount = 0;
        ulong minAmount = ulong.MaxValue;
        ulong maxAmount = 0;
        uint minHeight = uint.MaxValue;
        uint maxHeight = 0;

        Dictionary<uint, ulong> heightDistribution = [];
        Dictionary<int, ulong> amountRanges = new()
        {
            [0] = 0,    // 0 sats (dust)
            [1] = 0,    // 1-999 sats
            [2] = 0,    // 1000-9999 sats
            [3] = 0,    // 0.0001-0.001 BTC
            [4] = 0,    // 0.001-0.01 BTC
            [5] = 0,    // 0.01-0.1 BTC
            [6] = 0,    // 0.1-1 BTC
            [7] = 0,    // 1-10 BTC
            [8] = 0,    // 10-100 BTC
            [9] = 0     // 100+ BTC
        };

        ulong entryCount = 0;
        foreach (var tx in reader.ReadTransactions())
        {
            transactionCount++;

            foreach (var output in tx.Outputs)
            {
                entryCount++;
                totalAmount += output.Amount;

                if (output.Amount < minAmount) minAmount = output.Amount;
                if (output.Amount > maxAmount) maxAmount = output.Amount;
                if (output.Height < minHeight) minHeight = output.Height;
                if (output.Height > maxHeight) maxHeight = output.Height;

                if (output.IsCoinbase) coinbaseCount++;

                // Classify script type
                ClassifyScript(output.ScriptPubKey, ref p2pkhCount, ref p2shCount, ref p2pkCount, ref otherScriptCount);

                // Amount range classification
                ClassifyAmount(output.Amount, amountRanges);
            }

            if (transactionCount % 100000 == 0)
            {
                Console.Write($"\rAnalyzing... {entryCount:N0} UTXOs");
            }
        }

        Console.WriteLine();
        Console.WriteLine();

        // Output statistics
        Console.WriteLine("=== UTXO Set Statistics ===");
        Console.WriteLine();
        Console.WriteLine($"Network:           {header.Network}");
        Console.WriteLine($"Block Hash:        {header.GetBlockHashHex()}");
        Console.WriteLine();
        Console.WriteLine($"--- Counts ---");
        Console.WriteLine($"Total UTXOs:       {entryCount:N0}");
        Console.WriteLine($"Transactions:      {transactionCount:N0}");
        Console.WriteLine($"Coinbase UTXOs:    {coinbaseCount:N0} ({100.0 * coinbaseCount / entryCount:F2}%)");
        Console.WriteLine($"Avg UTXOs/Tx:      {(double)entryCount / transactionCount:F2}");
        Console.WriteLine();
        Console.WriteLine($"--- Amounts ---");
        Console.WriteLine($"Total Amount:      {totalAmount:N0} sats");
        Console.WriteLine($"Total BTC:         {totalAmount / 100_000_000m:F8} BTC");
        Console.WriteLine($"Min Amount:        {minAmount:N0} sats");
        Console.WriteLine($"Max Amount:        {maxAmount:N0} sats ({maxAmount / 100_000_000m:F8} BTC)");
        Console.WriteLine($"Avg Amount:        {totalAmount / entryCount:N0} sats");
        Console.WriteLine();
        Console.WriteLine($"--- Heights ---");
        Console.WriteLine($"Min Height:        {minHeight:N0}");
        Console.WriteLine($"Max Height:        {maxHeight:N0}");
        Console.WriteLine();
        Console.WriteLine($"--- Script Types ---");
        Console.WriteLine($"P2PKH:             {p2pkhCount:N0} ({100.0 * p2pkhCount / entryCount:F2}%)");
        Console.WriteLine($"P2SH:              {p2shCount:N0} ({100.0 * p2shCount / entryCount:F2}%)");
        Console.WriteLine($"P2PK:              {p2pkCount:N0} ({100.0 * p2pkCount / entryCount:F2}%)");
        Console.WriteLine($"Other:             {otherScriptCount:N0} ({100.0 * otherScriptCount / entryCount:F2}%)");
        Console.WriteLine();
        Console.WriteLine($"--- Amount Distribution ---");
        Console.WriteLine($"0 sats (dust):     {amountRanges[0]:N0}");
        Console.WriteLine($"1-999 sats:        {amountRanges[1]:N0}");
        Console.WriteLine($"1k-10k sats:       {amountRanges[2]:N0}");
        Console.WriteLine($"10k-100k sats:     {amountRanges[3]:N0}");
        Console.WriteLine($"100k-1M sats:      {amountRanges[4]:N0}");
        Console.WriteLine($"1M-10M sats:       {amountRanges[5]:N0}");
        Console.WriteLine($"10M-100M sats:     {amountRanges[6]:N0}");
        Console.WriteLine($"100M-1B sats:      {amountRanges[7]:N0}");
        Console.WriteLine($"1B-10B sats:       {amountRanges[8]:N0}");
        Console.WriteLine($">10B sats:         {amountRanges[9]:N0}");

        return 0;
    }

    private static void ClassifyScript(byte[] script, ref ulong p2pkh, ref ulong p2sh, ref ulong p2pk, ref ulong other)
    {
        if (script.Length == 25 && script[0] == 0x76 && script[1] == 0xa9)
        {
            p2pkh++;
        }
        else if (script.Length == 23 && script[0] == 0xa9 && script[1] == 0x14)
        {
            p2sh++;
        }
        else if ((script.Length == 35 && script[0] == 0x21) || (script.Length == 67 && script[0] == 0x41))
        {
            p2pk++;
        }
        else
        {
            other++;
        }
    }

    private static void ClassifyAmount(ulong amount, Dictionary<int, ulong> ranges)
    {
        int range = amount switch
        {
            0 => 0,
            < 1000 => 1,
            < 10000 => 2,
            < 100000 => 3,
            < 1000000 => 4,
            < 10000000 => 5,
            < 100000000 => 6,
            < 1000000000 => 7,
            < 10000000000 => 8,
            _ => 9
        };
        ranges[range]++;
    }
}
