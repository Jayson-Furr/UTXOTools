using UTXOTools.TXOutset.Encoding;
using UTXOTools.TXOutset.Models;
using UTXOTools.TXOutset.Scripts;

namespace UTXOTools.TXOutset.IO;

/// <summary>
/// Provides functionality for creating and writing unspent transaction output (UTXO) data to a binary file or stream in
/// a structured format.
/// </summary>
/// <remarks>UtxoFileWriter enables writing UTXO file headers, individual entries, and transactions to a binary
/// output, supporting both file-based and stream-based targets. The class ensures that headers are written before
/// entries and maintains metadata such as the number of entries written. It is not thread-safe; concurrent access
/// should be synchronized externally. After disposing the instance, no further operations should be
/// performed.</remarks>
public sealed class UtxoFileWriter : IDisposable
{
    private readonly BinaryWriter _writer;
    private readonly bool _leaveOpen;
    private bool _disposed;
    private bool _headerWritten;
    private ulong _entriesWritten;
    private long _utxoCountPosition;

    /// <summary>
    /// Gets the number of UTXO entries that have been written so far.
    /// </summary>
    public ulong EntriesWritten => _entriesWritten;

    /// <summary>
    /// Gets whether the header has been written.
    /// </summary>
    public bool HeaderWritten => _headerWritten;

    /// <summary>
    /// Initializes a new instance of the UtxoFileWriter class to create and write to a binary file containing UTXO
    /// data.
    /// </summary>
    /// <param name="filePath">The full path of the file to create and write to. If the directory does not exist, it will be created.</param>
    /// <param name="overwrite">Specifies whether to overwrite the file if it already exists. Set to <see langword="true"/> to overwrite;
    /// otherwise, an <see cref="IOException"/> is thrown if the file exists.</param>
    /// <exception cref="IOException">Thrown if <paramref name="filePath"/> refers to a file that already exists and <paramref name="overwrite"/> is
    /// <see langword="false"/>.</exception>
    public UtxoFileWriter(string filePath, bool overwrite = false)
    {
        // Check if file exists and handle overwrite option
        if (!overwrite && File.Exists(filePath))
        {
            throw new IOException($"File already exists: {filePath}");
        }

        // Ensure the directory exists
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Initialize the binary writer
        _writer = new BinaryWriter(File.Create(filePath));
        _leaveOpen = false;
    }

    /// <summary>
    /// Initializes a new instance of the UtxoFileWriter class that writes UTXO data to the specified stream.
    /// </summary>
    /// <param name="stream">The stream to which UTXO data will be written. The stream must be writable and remain open for the duration of
    /// the writer's usage.</param>
    /// <param name="leaveOpen">true to leave the stream open after the UtxoFileWriter is disposed; otherwise, false.</param>
    /// <exception cref="ArgumentException">Thrown if stream is not writable.</exception>
    public UtxoFileWriter(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Ensure the stream is writable
        if (!stream.CanWrite)
        {
            throw new ArgumentException("Stream must be writable.", nameof(stream));
        }

        // Initialize the binary writer
        _writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen);
        _leaveOpen = leaveOpen;
    }

    /// <summary>
    /// Writes the UTXO file header to the underlying stream using the specified header information.
    /// </summary>
    /// <remarks>This method must be called before writing any UTXO entries to the file. The header can only
    /// be written once per file instance.</remarks>
    /// <param name="header">The header data to write, including version, network, block hash, and UTXO count. Cannot be null.</param>
    /// <exception cref="InvalidOperationException">Thrown if the header has already been written.</exception>
    public void WriteHeader(UtxoFileHeader header)
    {
        ThrowIfDisposed();

        // Ensure header has not already been written
        if (_headerWritten)
        {
            throw new InvalidOperationException("Header has already been written.");
        }

        ArgumentNullException.ThrowIfNull(header);

        // Write file magic
        _writer.Write(UtxoFileConstants.FileMagic);

        // Write version
        _writer.Write(header.Version);

        // Write network magic
        if (header.NetworkMagic != null && header.NetworkMagic.Length == UtxoFileConstants.NetworkMagicLength)
        {
            // Write provided network magic
            _writer.Write(header.NetworkMagic);
        }
        else
        {
            // Write network magic based on network
            _writer.Write(UtxoFileConstants.GetMagicFromNetwork(header.Network));
        }

        // Write reversed block hash
        byte[] blockHashReversed = new byte[UtxoFileConstants.BlockHashLength];
        // Reverse the BlockHash
        for (int i = 0; i < UtxoFileConstants.BlockHashLength; i++)
        {
            blockHashReversed[i] = header.BlockHash[UtxoFileConstants.BlockHashLength - 1 - i];
        }
        // Write the reversed block hash
        _writer.Write(blockHashReversed);

        // Remember position of UTXO count
        _utxoCountPosition = _writer.BaseStream.Position;

        // Write UTXO count (initially as provided in header)
        _writer.Write(header.UtxoCount);

        // Mark header as written
        _headerWritten = true;
    }

    /// <summary>
    /// Writes a single unspent transaction output (UTXO) entry to the underlying data store as a transaction with one
    /// output.
    /// </summary>
    /// <remarks>This method creates a transaction containing only the specified UTXO entry and writes it to
    /// the data store. The method throws an exception if the writer has been disposed or if the entry is
    /// null.</remarks>
    /// <param name="entry">The UTXO entry to write. Cannot be null.</param>
    public void WriteEntry(UtxoEntry entry)
    {
        ThrowIfDisposed();

        EnsureHeaderWritten();

        ArgumentNullException.ThrowIfNull(entry);

        // Create a transaction with a single output from the entry
        var transaction = new UtxoTransaction
        {
            TxId = entry.TxId,
            Outputs =
            [
                new UtxoOutput
                {
                    Vout = entry.Vout,
                    Height = entry.Height,
                    IsCoinbase = entry.IsCoinbase,
                    Amount = entry.Amount,
                    ScriptPubKey = entry.ScriptPubKey
                }
            ]
        };

        // Write the transaction
        WriteTransaction(transaction);
    }

    /// <summary>
    /// Writes the specified transaction and its outputs to the underlying data stream in the UTXO file format.
    /// </summary>
    /// <remarks>Transactions with no outputs are ignored and not written to the stream. The transaction ID is
    /// reversed before being written, as required by the file format.</remarks>
    /// <param name="transaction">The transaction to write. Must not be null and must contain at least one output.</param>
    public void WriteTransaction(UtxoTransaction transaction)
    {
        ThrowIfDisposed();

        EnsureHeaderWritten();

        ArgumentNullException.ThrowIfNull(transaction);

        // Skip empty transactions
        if (transaction.Outputs.Count == 0)
        {
            return;
        }

        // Reverse the transaction ID for file format
        byte[] txIdReversed = new byte[UtxoFileConstants.TxIdLength];
        // Reverse the TxId
        for (int i = 0; i < UtxoFileConstants.TxIdLength; i++)
        {
            txIdReversed[i] = transaction.TxId[UtxoFileConstants.TxIdLength - 1 - i];
        }

        // Write the reversed transaction ID
        _writer.Write(txIdReversed);

        // Write number of outputs
        CompactSizeEncoding.WriteCompactSize(_writer, (ulong)transaction.Outputs.Count);

        // Write each output
        foreach (var output in transaction.Outputs)
        {
            // Write the output
            WriteOutput(output);
            // Increment entries written count
            _entriesWritten++;
        }
    }

    /// <summary>
    /// Writes a collection of UtxoTransaction objects to the output in sequence.
    /// </summary>
    /// <param name="transactions">An enumerable collection of UtxoTransaction instances to be written. Cannot be null.</param>
    public void WriteTransactions(IEnumerable<UtxoTransaction> transactions)
    {
        ThrowIfDisposed();
                
        EnsureHeaderWritten();

        // Write each transaction
        foreach (var transaction in transactions)
        {
            // Write each transaction
            WriteTransaction(transaction);
        }
    }

    /// <summary>
    /// Writes the specified UTXO output to the underlying binary stream using compact and compressed encoding formats.
    /// </summary>
    /// <remarks>This method encodes the output fields using space-efficient formats, including compact size
    /// and compressed representations for amount and scriptPubKey. The caller should ensure that the provided output is
    /// valid and its scriptPubKey is supported by the encoder.</remarks>
    /// <param name="output">The UTXO output to write. Must contain a valid scriptPubKey that can be encoded.</param>
    /// <exception cref="InvalidOperationException">Thrown if the scriptPubKey of <paramref name="output"/> cannot be encoded.</exception>
    private void WriteOutput(UtxoOutput output)
    {
        // Write vout
        CompactSizeEncoding.WriteCompactSize(_writer, output.Vout);

        // Write height and coinbase flag
        ulong heightAndCoinbase = ((ulong)output.Height << 1) | (output.IsCoinbase ? 1UL : 0UL);
        VarIntEncoding.WriteVarInt(_writer, heightAndCoinbase);

        // Write compressed amount
        ulong compressedAmount = AmountCompression.CompressAmount(output.Amount);
        VarIntEncoding.WriteVarInt(_writer, compressedAmount);

        // Write compressed scriptPubKey
        if (ScriptPubKeyDecoder.TryEncodeScriptPubKey(output.ScriptPubKey, out byte[]? compressedScript))
        {
            // Sanity check
            if (compressedScript == null)
            {
                throw new InvalidOperationException("Failed to encode scriptPubKey.");
            }

            // Write length-prefixed compressed script
            _writer.Write(compressedScript);

            // Successfully written
            return;
        }
        throw new InvalidOperationException("Failed to encode scriptPubKey.");
    }

    /// <summary>
    /// Updates the stored UTXO count in the underlying stream.
    /// </summary>
    /// <remarks>This method overwrites the previously stored UTXO count at its designated position in the
    /// stream. The stream must support seeking for the update to succeed.</remarks>
    /// <param name="count">The new UTXO count value to be written. Must be a valid count representing the number of unspent transaction
    /// outputs.</param>
    /// <exception cref="InvalidOperationException">Thrown if the header has not been written or if the underlying stream does not support seeking.</exception>
    public void UpdateUtxoCount(ulong count)
    {
        ThrowIfDisposed();

        // Ensure header has been written
        if (!_headerWritten)
        {
            throw new InvalidOperationException("Header has not been written yet.");
        }

        // Ensure the stream supports seeking
        if (!_writer.BaseStream.CanSeek)
        {
            throw new InvalidOperationException("Cannot update UTXO count: stream does not support seeking.");
        }

        // Save current position
        long currentPosition = _writer.BaseStream.Position;

        // Seek to UTXO count position and update
        _writer.BaseStream.Seek(_utxoCountPosition, SeekOrigin.Begin);
        _writer.Write(count);
        _writer.BaseStream.Seek(currentPosition, SeekOrigin.Begin);
    }

    /// <summary>
    /// Finalizes the output by updating metadata and flushing any remaining data to the underlying stream.
    /// </summary>
    /// <remarks>Call this method when all entries have been written to ensure that the output is complete and
    /// consistent. This method updates the UTXO count in the header if possible and flushes buffered data to the
    /// stream. After calling this method, no further entries should be written.</remarks>
#pragma warning disable CS0465 // Introducing a 'Finalize' method can interfere with destructor invocation
    public void Finalize()
#pragma warning restore CS0465 // Introducing a 'Finalize' method can interfere with destructor invocation
    {
        ThrowIfDisposed();

        // Update UTXO count in header if possible
        if (_headerWritten && _writer.BaseStream.CanSeek)
        {
            UpdateUtxoCount(_entriesWritten);
        }

        // Flush any remaining data
        _writer.Flush();
    }

    /// <summary>
    /// Clears all buffers for the underlying writer and causes any buffered data to be written to the output.
    /// </summary>
    /// <remarks>This method should be called to ensure that all buffered data is written, especially before
    /// closing the writer or when immediate output is required. If the writer has already been disposed, an exception
    /// will be thrown.</remarks>
    public void Flush()
    {
        ThrowIfDisposed();

        // Flush the underlying writer
        _writer.Flush();
    }

    /// <summary>
    /// Ensures that the header has been written before allowing further entries to be written.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the header has not been written prior to writing entries.</exception>
    private void EnsureHeaderWritten()
    {
        // Ensure the header has been written before writing entries
        if (!_headerWritten)
        {
            throw new InvalidOperationException("Header must be written before writing entries.");
        }
    }

    /// <summary>
    /// Throws an exception if the current instance has been disposed.
    /// </summary>
    /// <remarks>Call this method before performing operations that require the instance to be valid. This
    /// helps ensure that methods are not invoked on an object that is no longer usable.</remarks>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Releases all resources used by the current instance and flushes any buffered data to the underlying writer.
    /// </summary>
    /// <remarks>Call this method when you are finished using the instance to ensure that all data is written
    /// and resources are released. After calling <see cref="Dispose"/>, the instance should not be used
    /// further.</remarks>
    public void Dispose()
    {
        if (!_disposed)
        {
            // Flush any remaining data before disposing
            Flush();
            // Dispose the underlying writer if not leaving open
            if (!_leaveOpen)
            {
                _writer.Close();
                _writer.Dispose();
            }
            _disposed = true;
        }
    }
}
