namespace UTXOTools.TXOutset.Models;

/// <summary>
/// Represents a single UTXO entry (unspent transaction output).
/// </summary>
public sealed class UtxoEntry
{
    /// <summary>
    /// Gets or sets the transaction ID this output belongs to (in display order, not file order).
    /// </summary>
    public byte[] TxId { get; set; } = new byte[32];

    /// <summary>
    /// Gets or sets the output index (vout) within the transaction.
    /// </summary>
    public ulong Vout { get; set; }

    /// <summary>
    /// Gets or sets the block height at which this output was created.
    /// </summary>
    public uint Height { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a coinbase output.
    /// </summary>
    public bool IsCoinbase { get; set; }

    /// <summary>
    /// Gets or sets the amount in satoshis.
    /// </summary>
    public ulong Amount { get; set; }

    /// <summary>
    /// Gets or sets the scriptPubKey (the locking script).
    /// </summary>
    public byte[] ScriptPubKey { get; set; } = [];

    /// <summary>
    /// Gets the transaction ID as a hexadecimal string.
    /// </summary>
    /// <returns>The transaction ID in hexadecimal format.</returns>
    public string GetTxIdHex()
    {
        return Convert.ToHexString(TxId).ToLowerInvariant();
    }

    /// <summary>
    /// Gets the scriptPubKey as a hexadecimal string.
    /// </summary>
    /// <returns>The scriptPubKey in hexadecimal format.</returns>
    public string GetScriptPubKeyHex()
    {
        return Convert.ToHexString(ScriptPubKey).ToLowerInvariant();
    }

    /// <summary>
    /// Gets the amount in BTC (with 8 decimal places).
    /// </summary>
    /// <returns>The amount in BTC.</returns>
    public decimal GetAmountInBtc()
    {
        return Amount / 100_000_000m;
    }

    /// <summary>
    /// Creates a string identifier for this UTXO in the format "txid:vout".
    /// </summary>
    /// <returns>The outpoint identifier.</returns>
    public string GetOutpoint()
    {
        return $"{GetTxIdHex()}:{Vout}";
    }

    /// <summary>
    /// Returns a string representation of the UTXO entry.
    /// </summary>
    public override string ToString()
    {
        return $"{GetOutpoint()} - {Amount} sats (height: {Height}, coinbase: {IsCoinbase})";
    }
}
