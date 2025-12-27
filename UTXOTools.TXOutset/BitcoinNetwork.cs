namespace UTXOTools.TXOutset;

/// <summary>
/// Represents the Bitcoin network types that can be identified in UTXO files.
/// </summary>
public enum BitcoinNetwork
{
    /// <summary>
    /// Unknown or unrecognized network.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Bitcoin Mainnet - the primary production network.
    /// </summary>
    Mainnet = 1,

    /// <summary>
    /// Signet - a signature-based test network.
    /// </summary>
    Signet = 2,

    /// <summary>
    /// Testnet3 - the third iteration of the test network.
    /// </summary>
    Testnet3 = 3,

    /// <summary>
    /// Testnet4 - the fourth iteration of the test network.
    /// </summary>
    Testnet4 = 4,

    /// <summary>
    /// Regtest - a regression testing network for local development.
    /// </summary>
    Regtest = 5
}
