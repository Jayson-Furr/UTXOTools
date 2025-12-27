namespace UTXOTools.TXOutset;

/// <summary>
/// Contains constants used for UTXO file format parsing and validation.
/// </summary>
public static class UtxoFileConstants
{
    /// <summary>
    /// The file magic bytes that identify a UTXO dump file.
    /// </summary>
    public static ReadOnlySpan<byte> FileMagic => [0x75, 0x74, 0x78, 0x6f, 0xff];

    /// <summary>
    /// The length of the file magic in bytes.
    /// </summary>
    public const int FileMagicLength = 5;

    /// <summary>
    /// The length of the file version in bytes.
    /// </summary>
    public const int FileVersionLength = 2;

    /// <summary>
    /// The length of the network magic in bytes.
    /// </summary>
    public const int NetworkMagicLength = 4;

    /// <summary>
    /// The length of a block hash in bytes.
    /// </summary>
    public const int BlockHashLength = 32;

    /// <summary>
    /// The length of a transaction ID in bytes.
    /// </summary>
    public const int TxIdLength = 32;

    /// <summary>
    /// The length of the UTXO count field in bytes.
    /// </summary>
    public const int UtxoCountLength = 8;

    /// <summary>
    /// The total header length in bytes (magic + version + network + hash + count).
    /// </summary>
    public const int HeaderLength = FileMagicLength + FileVersionLength + NetworkMagicLength + BlockHashLength + UtxoCountLength;

    /// <summary>
    /// Set of supported UTXO file format versions.
    /// </summary>
    public static readonly HashSet<ushort> SupportedVersions = [2];

    /// <summary>
    /// The current default version for writing new files.
    /// </summary>
    public const ushort CurrentVersion = 2;

    /// <summary>
    /// Network magic bytes for Mainnet.
    /// </summary>
    public static ReadOnlySpan<byte> MainnetMagic => [0xf9, 0xbe, 0xb4, 0xd9];

    /// <summary>
    /// Network magic bytes for Signet.
    /// </summary>
    public static ReadOnlySpan<byte> SignetMagic => [0x0a, 0x03, 0xcf, 0x40];

    /// <summary>
    /// Network magic bytes for Testnet3.
    /// </summary>
    public static ReadOnlySpan<byte> Testnet3Magic => [0x0b, 0x11, 0x09, 0x07];

    /// <summary>
    /// Network magic bytes for Testnet4.
    /// </summary>
    public static ReadOnlySpan<byte> Testnet4Magic => [0x1c, 0x16, 0x3f, 0x28];

    /// <summary>
    /// Network magic bytes for Regtest.
    /// </summary>
    public static ReadOnlySpan<byte> RegtestMagic => [0xfa, 0xbf, 0xb5, 0xda];

    /// <summary>
    /// Gets the network type from the network magic bytes.
    /// </summary>
    /// <param name="magic">The 4-byte network magic.</param>
    /// <returns>The corresponding BitcoinNetwork value.</returns>
    public static BitcoinNetwork GetNetworkFromMagic(ReadOnlySpan<byte> magic)
    {
        if (magic.Length != NetworkMagicLength)
        {
            return BitcoinNetwork.Unknown;
        }

        if (magic.SequenceEqual(MainnetMagic))
            return BitcoinNetwork.Mainnet;
        if (magic.SequenceEqual(SignetMagic))
            return BitcoinNetwork.Signet;
        if (magic.SequenceEqual(Testnet3Magic))
            return BitcoinNetwork.Testnet3;
        if (magic.SequenceEqual(Testnet4Magic))
            return BitcoinNetwork.Testnet4;
        if (magic.SequenceEqual(RegtestMagic))
            return BitcoinNetwork.Regtest;

        return BitcoinNetwork.Unknown;
    }

    /// <summary>
    /// Gets the network magic bytes for a given network type.
    /// </summary>
    /// <param name="network">The network type.</param>
    /// <returns>The 4-byte network magic.</returns>
    /// <exception cref="ArgumentException">Thrown when the network is unknown.</exception>
    public static byte[] GetMagicFromNetwork(BitcoinNetwork network)
    {
        return network switch
        {
            BitcoinNetwork.Mainnet => MainnetMagic.ToArray(),
            BitcoinNetwork.Signet => SignetMagic.ToArray(),
            BitcoinNetwork.Testnet3 => Testnet3Magic.ToArray(),
            BitcoinNetwork.Testnet4 => Testnet4Magic.ToArray(),
            BitcoinNetwork.Regtest => RegtestMagic.ToArray(),
            _ => throw new ArgumentException($"Cannot get magic for unknown network: {network}", nameof(network))
        };
    }

    /// <summary>
    /// Validates if a file version is supported.
    /// </summary>
    /// <param name="version">The version to validate.</param>
    /// <returns>True if the version is supported; otherwise, false.</returns>
    public static bool IsVersionSupported(ushort version)
    {
        return SupportedVersions.Contains(version);
    }
}
