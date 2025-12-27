namespace UTXOTools.TXOutset.Models;

/// <summary>
/// Represents the header of a UTXO dump file.
/// </summary>
public sealed class UtxoFileHeader
{
    /// <summary>
    /// Gets or sets the file format version.
    /// </summary>
    public ushort Version { get; set; }

    /// <summary>
    /// Gets or sets the Bitcoin network type.
    /// </summary>
    public BitcoinNetwork Network { get; set; }

    /// <summary>
    /// Gets or sets the raw network magic bytes.
    /// </summary>
    public byte[] NetworkMagic { get; set; } = [];

    /// <summary>
    /// Gets or sets the block hash of the UTXO set (in display order, not file order).
    /// </summary>
    public byte[] BlockHash { get; set; } = new byte[32];

    /// <summary>
    /// Gets or sets the total number of UTXOs in the file.
    /// </summary>
    public ulong UtxoCount { get; set; }

    /// <summary>
    /// Gets the block hash as a hexadecimal string (in standard display format).
    /// </summary>
    /// <returns>The block hash in hexadecimal format.</returns>
    public string GetBlockHashHex()
    {
        return Convert.ToHexString(BlockHash).ToLowerInvariant();
    }

    /// <summary>
    /// Creates a new UtxoFileHeader with default values for the current version.
    /// </summary>
    /// <param name="network">The Bitcoin network.</param>
    /// <param name="blockHash">The block hash (in display order).</param>
    /// <param name="utxoCount">The total UTXO count.</param>
    /// <returns>A new UtxoFileHeader instance.</returns>
    public static UtxoFileHeader Create(BitcoinNetwork network, byte[] blockHash, ulong utxoCount)
    {
        return new UtxoFileHeader
        {
            Version = UtxoFileConstants.CurrentVersion,
            Network = network,
            NetworkMagic = UtxoFileConstants.GetMagicFromNetwork(network),
            BlockHash = blockHash,
            UtxoCount = utxoCount
        };
    }

    /// <summary>
    /// Returns a string representation of the header.
    /// </summary>
    public override string ToString()
    {
        return $"Version: {Version}, Network: {Network}, BlockHash: {GetBlockHashHex()}, UTXOs: {UtxoCount:N0}";
    }
}
