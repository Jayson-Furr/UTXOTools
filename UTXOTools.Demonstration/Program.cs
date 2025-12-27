using UTXOTools.Demonstration.Commands;

namespace UTXOTools.Demonstration;

internal class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            ShowHelp();
            return 0;
        }

        string command = args[0].ToLowerInvariant();

        try
        {
            return command switch
            {
                "info" => InfoCommand.Execute(args[1..]),
                "list" => ListCommand.Execute(args[1..]),
                "export" => ExportCommand.Execute(args[1..]),
                "validate" => ValidateCommand.Execute(args[1..]),
                "stats" => StatsCommand.Execute(args[1..]),
                "p2pk" => P2pkCommand.Execute(args[1..]),
                "help" or "--help" or "-h" => ShowHelp(),
                "version" or "--version" or "-v" => ShowVersion(),
                _ => UnknownCommand(command)
            };
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (args.Contains("--verbose") || args.Contains("-v"))
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    private static int ShowHelp()
    {
        Console.WriteLine("""
            UTXOTools - Bitcoin UTXO File Utility

            Usage: UTXOTools.Demonstration <command> [options]

            Commands:
              info <file>              Show header information from a UTXO file
              list <file> [options]    List UTXO entries from a file
              export <file> <output>   Export UTXO data to various formats
              validate <file>          Validate a UTXO file
              stats <file>             Show statistics about the UTXO set
              p2pk <file> <output>     Extract P2PK public keys to binary file
              help                     Show this help message
              version                  Show version information

            Options for 'list':
              --limit <n>              Limit output to first n entries (default: 10)
              --offset <n>             Skip first n entries (default: 0)
              --txid <hex>             Filter by transaction ID
              --json                   Output in JSON format
              --csv                    Output in CSV format

            Options for 'export':
              --format <fmt>           Output format: json, csv (default: json)
              --limit <n>              Limit to first n entries

            Options for 'p2pk':
              --include-amount         Include satoshi amount for each public key
              --verbose                Show detailed progress

            Examples:
              UTXOTools.Demonstration info utxo.dat
              UTXOTools.Demonstration list utxo.dat --limit 100
              UTXOTools.Demonstration stats utxo.dat
              UTXOTools.Demonstration validate utxo.dat
              UTXOTools.Demonstration export utxo.dat output.json --format json
              UTXOTools.Demonstration p2pk utxo.dat p2pk_keys.bin
            """);
        return 0;
    }

    private static int ShowVersion()
    {
        Console.WriteLine("UTXOTools.Demonstration v1.0.0");
        Console.WriteLine("Supports UTXO file format version 2 (Bitcoin Core 30.x)");
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Console.Error.WriteLine("Use 'UTXOTools.Demonstration help' to see available commands.");
        return 1;
    }
}
