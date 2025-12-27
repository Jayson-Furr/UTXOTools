namespace UTXOTools.TXOutset.Models;

/// <summary>
/// Represents a group of UTXO entries that share the same transaction ID.
/// This matches how entries are stored in the UTXO file (grouped by txid).
/// </summary>
public sealed class UtxoTransaction
{
    /// <summary>
    /// Gets or sets the transaction ID (in display order, not file order).
    /// </summary>
    public byte[] TxId { get; set; } = new byte[32];

    /// <summary>
    /// Gets or sets the list of outputs for this transaction.
    /// </summary>
    public List<UtxoOutput> Outputs { get; set; } = [];

    /// <summary>
    /// Gets the transaction ID as a hexadecimal string.
    /// </summary>
    /// <returns>The transaction ID in hexadecimal format.</returns>
    public string GetTxIdHex()
    {
        return Convert.ToHexString(TxId).ToLowerInvariant();
    }

    /// <summary>
    /// Gets the total number of UTXOs in this transaction.
    /// </summary>
    public int OutputCount => Outputs.Count;

    /// <summary>
    /// Gets the total amount in satoshis of all UTXOs in this transaction.
    /// </summary>
    public ulong TotalAmount => Outputs.Aggregate(0UL, (sum, o) => sum + o.Amount);

    /// <summary>
    /// Converts this transaction group to individual UtxoEntry objects.
    /// </summary>
    /// <returns>An enumerable of UtxoEntry objects.</returns>
    public IEnumerable<UtxoEntry> ToEntries()
    {
        foreach (var output in Outputs)
        {
            yield return new UtxoEntry
            {
                TxId = TxId,
                Vout = output.Vout,
                Height = output.Height,
                IsCoinbase = output.IsCoinbase,
                Amount = output.Amount,
                ScriptPubKey = output.ScriptPubKey
            };
        }
    }

    /// <summary>
    /// Returns a string representation of the transaction group.
    /// </summary>
    public override string ToString()
    {
        return $"TxId: {GetTxIdHex()}, Outputs: {OutputCount}, Total: {TotalAmount} sats";
    }
}

/// <summary>
/// Represents a single output within a transaction group.
/// </summary>
public sealed class UtxoOutput
{
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
    /// Returns a string representation of the output.
    /// </summary>
    public override string ToString()
    {
        return $"Vout: {Vout}, Amount: {Amount} sats, Height: {Height}, Coinbase: {IsCoinbase}";
    }
}
