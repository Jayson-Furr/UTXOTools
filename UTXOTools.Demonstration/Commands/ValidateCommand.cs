using UTXOTools.TXOutset.Exceptions;
using UTXOTools.TXOutset.IO;

namespace UTXOTools.Demonstration.Commands;

/// <summary>
/// Command to validate a UTXO file.
/// </summary>
internal static class ValidateCommand
{
    public static int Execute(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: UTXOTools.Demonstration validate <file>");
            return 1;
        }

        string filePath = args[0];

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: File not found: {filePath}");
            return 1;
        }

        Console.WriteLine($"Validating {filePath}...");
        Console.WriteLine();

        try
        {
            using var reader = new UtxoFileReader(filePath);
            
            // Validate header
            Console.Write("Checking header... ");
            var header = reader.ReadHeader();
            Console.WriteLine("OK");

            Console.WriteLine($"  Version:    {header.Version}");
            Console.WriteLine($"  Network:    {header.Network}");
            Console.WriteLine($"  Block Hash: {header.GetBlockHashHex()}");
            Console.WriteLine($"  Expected:   {header.UtxoCount:N0} UTXOs");
            Console.WriteLine();

            // Count and validate entries
            Console.Write("Validating entries... ");
            
            ulong entryCount = 0;
            ulong transactionCount = 0;
            ulong totalAmount = 0;

            foreach (var tx in reader.ReadTransactions())
            {
                transactionCount++;
                foreach (var output in tx.Outputs)
                {
                    entryCount++;
                    totalAmount += output.Amount;
                }

                if (transactionCount % 100000 == 0)
                {
                    Console.Write($"\rValidating entries... {entryCount:N0} UTXOs in {transactionCount:N0} transactions");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"  Transactions: {transactionCount:N0}");
            Console.WriteLine($"  UTXOs:        {entryCount:N0}");
            Console.WriteLine($"  Total Amount: {totalAmount:N0} sats ({totalAmount / 100_000_000m:F8} BTC)");
            Console.WriteLine();

            // Verify count
            if (entryCount != header.UtxoCount)
            {
                Console.WriteLine($"VALIDATION FAILED: Count mismatch");
                Console.WriteLine($"  Header says:  {header.UtxoCount:N0}");
                Console.WriteLine($"  Actual count: {entryCount:N0}");
                return 1;
            }

            Console.WriteLine("VALIDATION PASSED");
            Console.WriteLine($"File is valid with {entryCount:N0} UTXOs.");
            return 0;
        }
        catch (UtxoFormatException ex)
        {
            Console.WriteLine();
            Console.WriteLine($"VALIDATION FAILED: Format error");
            Console.WriteLine($"  {ex.Message}");
            return 1;
        }
        catch (UtxoVersionException ex)
        {
            Console.WriteLine();
            Console.WriteLine($"VALIDATION FAILED: Version not supported");
            Console.WriteLine($"  {ex.Message}");
            return 1;
        }
    }
}
