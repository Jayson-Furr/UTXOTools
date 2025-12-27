using UTXOTools.TXOutset.IO;

namespace UTXOTools.Demonstration.Commands;

/// <summary>
/// Command to display header information from a UTXO file.
/// </summary>
internal static class InfoCommand
{
    public static int Execute(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: UTXOTools.Demonstration info <file>");
            return 1;
        }

        string filePath = args[0];

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: File not found: {filePath}");
            return 1;
        }

        using var reader = new UtxoFileReader(filePath);
        var header = reader.ReadHeader();

        Console.WriteLine("=== UTXO File Information ===");
        Console.WriteLine();
        Console.WriteLine($"File:          {Path.GetFileName(filePath)}");
        Console.WriteLine($"File Size:     {FormatFileSize(reader.Length)}");
        Console.WriteLine($"Version:       {header.Version}");
        Console.WriteLine($"Network:       {header.Network}");
        Console.WriteLine($"Network Magic: {BitConverter.ToString(header.NetworkMagic).Replace("-", " ").ToLowerInvariant()}");
        Console.WriteLine($"Block Hash:    {header.GetBlockHashHex()}");
        Console.WriteLine($"UTXO Count:    {header.UtxoCount:N0}");

        return 0;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
