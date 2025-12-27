using UTXOTools.TXOutset.Encoding;
using UTXOTools.TXOutset.Exceptions;
using UTXOTools.TXOutset.Models;
using UTXOTools.TXOutset.Scripts;

namespace UTXOTools.TXOutset.IO;

/// <summary>
/// Provides functionality for reading and validating unspent transaction output (UTXO) data from a file or stream in
/// the UTXO file format. Supports sequential access to file header, transactions, and individual UTXO entries, and
/// exposes methods for integrity validation and stream position management.
/// </summary>
/// <remarks>Instances of UtxoFileReader enable efficient, forward-only reading of large UTXO datasets by exposing
/// enumerables for transactions and entries. The reader caches header information after the first read and tracks the
/// number of entries processed. Thread safety is not guaranteed; callers should synchronize access if using from
/// multiple threads. The class implements IDisposable and must be disposed to release file or stream resources. After
/// disposal, further operations will throw exceptions.</remarks>
public sealed class UtxoFileReader : IDisposable
{
    private readonly BinaryReader _reader;
    private readonly bool _leaveOpen;
    private bool _disposed;
    private UtxoFileHeader? _header;
    private ulong _entriesRead;

    /// <summary>
    /// Gets the file header. Returns null if the header has not been read yet.
    /// </summary>
    public UtxoFileHeader? Header => _header;

    /// <summary>
    /// Gets the number of UTXO entries that have been read so far.
    /// </summary>
    public ulong EntriesRead => _entriesRead;

    /// <summary>
    /// Gets the underlying stream position.
    /// </summary>
    public long Position => _reader.BaseStream.Position;

    /// <summary>
    /// Gets the length of the underlying stream.
    /// </summary>
    public long Length => _reader.BaseStream.Length;

    /// <summary>
    /// Initializes a new instance of the UtxoFileReader class for reading unspent transaction output (UTXO) data from
    /// the specified file.
    /// </summary>
    /// <remarks>The file is opened for read-only access. The reader must be disposed when no longer needed to
    /// release file resources.</remarks>
    /// <param name="filePath">The path to the UTXO file to be read. Must refer to an existing file.</param>
    /// <exception cref="FileNotFoundException">Thrown if the file specified by <paramref name="filePath"/> does not exist.</exception>
    public UtxoFileReader(string filePath)
    {
        // Validate file existence
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("UTXO file not found.", filePath);
        }

        // Initialize binary reader
        _reader = new BinaryReader(File.OpenRead(filePath));
        _leaveOpen = false;
    }

    /// <summary>
    /// Initializes a new instance of the UtxoFileReader class to read UTXO data from the specified stream.
    /// </summary>
    /// <param name="stream">The input stream containing UTXO data to be read. The stream must support reading.</param>
    /// <param name="leaveOpen">true to leave the stream open after the UtxoFileReader is disposed; otherwise, false.</param>
    /// <exception cref="ArgumentException">Thrown if the stream does not support reading.</exception>
    public UtxoFileReader(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Validate stream capabilities
        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        }

        // Initialize binary reader
        _reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen);
        _leaveOpen = leaveOpen;
    }

    /// <summary>
    /// Reads and validates the header information from the UTXO file, including file magic, version, network, block
    /// hash, and UTXO count.
    /// </summary>
    /// <remarks>This method caches the header after the first successful read, so repeated calls do not
    /// re-read the file. The header includes metadata required to interpret the rest of the UTXO file. Thread safety is
    /// not guaranteed; callers should ensure appropriate synchronization if accessing from multiple threads.</remarks>
    /// <returns>A <see cref="UtxoFileHeader"/> containing the parsed header details of the UTXO file. The same instance is
    /// returned on subsequent calls if the header has already been read.</returns>
    /// <exception cref="UtxoFormatException">Thrown if the file magic, network magic, or block hash is invalid, or if the end of file is reached unexpectedly
    /// while reading header fields.</exception>
    /// <exception cref="UtxoVersionException">Thrown if the file version is not supported.</exception>
    public UtxoFileHeader ReadHeader()
    {
        ThrowIfDisposed();

        // Return cached header if already read
        if (_header != null)
        {
            return _header;
        }

        // Read and validate file magic
        byte[] fileMagic = _reader.ReadBytes(UtxoFileConstants.FileMagicLength);

        // Validate length
        if (fileMagic.Length != UtxoFileConstants.FileMagicLength)
        {
            throw new UtxoFormatException("Unexpected end of file while reading file magic.");
        }

        // Validate magic
        if (!fileMagic.AsSpan().SequenceEqual(UtxoFileConstants.FileMagic))
        {
            throw new UtxoFormatException("Invalid UTXO file magic. Expected 'utxo\\xff'.");
        }

        // Read and validate version
        ushort version = _reader.ReadUInt16();
        if (!UtxoFileConstants.IsVersionSupported(version))
        {
            throw new UtxoVersionException(version, UtxoFileConstants.SupportedVersions);
        }

        // Read network magic
        byte[] networkMagic = _reader.ReadBytes(UtxoFileConstants.NetworkMagicLength);
        if (networkMagic.Length != UtxoFileConstants.NetworkMagicLength)
        {
            throw new UtxoFormatException("Unexpected end of file while reading network magic.");
        }

        BitcoinNetwork network = UtxoFileConstants.GetNetworkFromMagic(networkMagic);

        // Read block hash (stored in reverse order in file)
        byte[] blockHashReversed = _reader.ReadBytes(UtxoFileConstants.BlockHashLength);

        // Validate length
        if (blockHashReversed.Length != UtxoFileConstants.BlockHashLength)
        {
            throw new UtxoFormatException("Unexpected end of file while reading block hash.");
        }

        // Convert to display order
        byte[] blockHash = new byte[UtxoFileConstants.BlockHashLength];
        // Reverse byte order
        for (int i = 0; i < UtxoFileConstants.BlockHashLength; i++)
        {
            blockHash[i] = blockHashReversed[UtxoFileConstants.BlockHashLength - 1 - i];
        }

        // Read UTXO count
        ulong utxoCount = _reader.ReadUInt64();

        // Create and cache header
        _header = new UtxoFileHeader
        {
            Version = version,
            Network = network,
            NetworkMagic = networkMagic,
            BlockHash = blockHash,
            UtxoCount = utxoCount
        };

        // Return header
        return _header;
    }

    /// <summary>
    /// Returns an enumerable collection of unspent transaction output (UTXO) entries read from the underlying data
    /// source.
    /// </summary>
    /// <remarks>The returned enumerable yields entries as they are read, which may be useful for processing
    /// large datasets efficiently. This method throws an exception if the object has been disposed.</remarks>
    /// <returns>An <see cref="IEnumerable{UtxoEntry}"/> containing all UTXO entries available in the data source. The collection
    /// may be empty if no entries are present.</returns>
    public IEnumerable<UtxoEntry> ReadEntries()
    {
        ThrowIfDisposed();

        // Ensure header is read
        if (_header == null)
        {
            ReadHeader();
        }

        // Read entries from transactions
        foreach (var transaction in ReadTransactions())
        {
            // Yield each entry in the transaction
            foreach (var entry in transaction.ToEntries())
            {
                // Update entries read
                _entriesRead++;
                yield return entry;
            }
        }
    }

    /// <summary>
    /// Reads and returns all UTXO transactions from the underlying data source as an enumerable sequence.
    /// </summary>
    /// <remarks>This method reads transactions sequentially and yields each <see cref="UtxoTransaction"/> as
    /// it is processed. The enumeration will stop if the end of the data source is reached before all expected entries
    /// are read. The method should not be called after the object has been disposed.</remarks>
    /// <returns>An enumerable collection of <see cref="UtxoTransaction"/> objects representing all transactions in the data
    /// source. The collection contains one entry for each transaction until all expected UTXO entries have been read.</returns>
    /// <exception cref="UtxoFormatException">Thrown when the number of UTXO entries read does not match the count specified in the header, indicating a
    /// format or data inconsistency.</exception>
    public IEnumerable<UtxoTransaction> ReadTransactions()
    {
        ThrowIfDisposed();

        // Ensure header is read
        if (_header == null)
        {
            ReadHeader();
        }

        // Track entries processed
        ulong totalEntriesExpected = _header!.UtxoCount;
        ulong entriesProcessed = 0;

        // Read transactions until all entries are processed
        while (entriesProcessed < totalEntriesExpected)
        {
            // Read next transaction
            UtxoTransaction? transaction = ReadNextTransaction();

            // Check for end of file
            if (transaction == null)
            {
                // Unexpected end of file
                break;
            }

            // Update entries processed
            entriesProcessed += (ulong)transaction.OutputCount;

            // Update entries read
            yield return transaction;
        }

        // Validate total entries processed
        if (entriesProcessed != totalEntriesExpected)
        {
            // Mismatch in expected vs actual entries
            throw new UtxoFormatException($"UTXO count mismatch. Header indicated {totalEntriesExpected} entries, but read {entriesProcessed}.");
        }
    }

    /// <summary>
    /// Reads the next transaction from the underlying UTXO file stream.
    /// </summary>
    /// <remarks>This method advances the stream position to the next transaction. If the reader has reached
    /// the end of the file, <see langword="null"/> is returned. The method must not be called after the object has been
    /// disposed.</remarks>
    /// <returns>A <see cref="UtxoTransaction"/> object representing the next transaction in the file, or <see langword="null"/>
    /// if the end of the file has been reached.</returns>
    /// <exception cref="UtxoFormatException">Thrown when the file format is invalid or the end of the file is reached unexpectedly while reading a
    /// transaction.</exception>
    public UtxoTransaction? ReadNextTransaction()
    {
        ThrowIfDisposed();

        // Ensure header is read
        if (_header == null)
        {
            ReadHeader();
        }

        // Check for end of file
        if (_reader.BaseStream.Position >= _reader.BaseStream.Length)
        {
            return null;
        }

        try
        {
            // Read transaction ID (stored in reverse order in file)
            byte[] txIdReversed = _reader.ReadBytes(UtxoFileConstants.TxIdLength);

            // Check for end of file
            if (txIdReversed.Length == 0)
            {
                return null;
            }

            // Validate length
            if (txIdReversed.Length != UtxoFileConstants.TxIdLength)
            {
                throw new UtxoFormatException("Unexpected end of file while reading transaction ID.");
            }

            // Convert to display order
            byte[] txId = new byte[UtxoFileConstants.TxIdLength];
            // Reverse byte order
            for (int i = 0; i < UtxoFileConstants.TxIdLength; i++)
            {
                txId[i] = txIdReversed[UtxoFileConstants.TxIdLength - 1 - i];
            }

            // Read output count
            ulong outputCount = CompactSizeEncoding.ReadCompactSize(_reader);

            // Initialize transaction
            var transaction = new UtxoTransaction
            {
                TxId = txId,
                Outputs = new List<UtxoOutput>((int)Math.Min(outputCount, int.MaxValue))
            };
            
            // Read outputs
            for (ulong i = 0; i < outputCount; i++)
            {
                // Read single output
                var output = ReadOutput();

                // Add to transaction
                transaction.Outputs.Add(output);
            }

            // Return populated transaction
            return transaction;
        }
        catch (EndOfStreamException ex)
        {
            // Wrap and rethrow as UtxoFormatException
            throw new UtxoFormatException("Unexpected end of file while reading transaction.", ex);
        }
    }

    /// <summary>
    /// Reads a single UTXO output from the underlying binary stream and returns its deserialized representation.
    /// </summary>
    /// <remarks>This method expects the stream to be positioned at the start of a valid UTXO output encoding.
    /// The returned object reflects the decoded values as read from the stream. The caller is responsible for ensuring
    /// the stream contains sufficient and correctly formatted data.</remarks>
    /// <returns>A <see cref="UtxoOutput"/> instance containing the output's index, block height, coinbase status, amount, and
    /// scriptPubKey.</returns>
    /// <exception cref="UtxoFormatException">Thrown if the output data is malformed or the scriptPubKey cannot be decoded.</exception>
    private UtxoOutput ReadOutput()
    {
        // Read vout index
        ulong vout = CompactSizeEncoding.ReadCompactSize(_reader);

        // Read height and coinbase flag (combined as VarInt)
        ulong heightAndCoinbase = VarIntEncoding.ReadVarInt(_reader);
        uint height = (uint)(heightAndCoinbase >> 1);
        bool isCoinbase = (heightAndCoinbase & 1) != 0;

        // Read compressed amount
        ulong compressedAmount = VarIntEncoding.ReadVarInt(_reader);
        ulong amount = AmountCompression.DecompressAmount(compressedAmount);
        // Read scriptPubKey
        if (ScriptPubKeyDecoder.TryDecodeScriptPubKey(_reader, out byte[]? scriptPubKey))
        {
            // Validate scriptPubKey
            if (scriptPubKey == null)
            {
                throw new UtxoFormatException("Failed to decode scriptPubKey.");
            }

            return new UtxoOutput
            {
                Vout = vout,
                Height = height,
                IsCoinbase = isCoinbase,
                Amount = amount,
                ScriptPubKey = scriptPubKey
            };
        }
        throw new UtxoFormatException("Failed to decode scriptPubKey.");
    }

    /// <summary>
    /// Validates the integrity of the UTXO data by checking that the header information matches the actual transaction
    /// output count.
    /// </summary>
    /// <remarks>This method reads and verifies the header and iterates through all transactions to ensure
    /// consistency. If the object has been disposed, an exception may be thrown prior to validation.</remarks>
    /// <returns>true if the UTXO data is valid and the header count matches the actual number of transaction outputs; otherwise,
    /// an exception is thrown.</returns>
    /// <exception cref="UtxoFormatException">Thrown if the UTXO count specified in the header does not match the actual number of transaction outputs found.</exception>
    public bool Validate()
    {
        ThrowIfDisposed();

        // Ensure header is read
        ReadHeader();

        // Count total entries
        ulong entriesCount = 0;
        foreach (var transaction in ReadTransactions())
        {
            // Accumulate output count
            entriesCount += (ulong)transaction.OutputCount;
        }

        // Verify count matches header
        if (entriesCount != _header!.UtxoCount)
        {
            throw new UtxoFormatException($"UTXO count validation failed. Header: {_header.UtxoCount}, Actual: {entriesCount}");
        }

        // If we reach here, validation passed
        return true;
    }

    /// <summary>
    /// Resets the reader to the beginning of the underlying stream, allowing entries to be read again from the start.
    /// </summary>
    /// <remarks>After calling this method, any previously read state is cleared and subsequent read
    /// operations will begin from the start of the stream. This method throws an exception if the reader has been
    /// disposed.</remarks>
    public void Reset()
    {
        ThrowIfDisposed();

        // Reset stream position
        _reader.BaseStream.Seek(0, SeekOrigin.Begin);
        _header = null;
        _entriesRead = 0;
    }

    /// <summary>
    /// Throws an exception if the current instance has been disposed.
    /// </summary>
    /// <remarks>Call this method at the beginning of operations that require the object to be in a valid,
    /// non-disposed state. This helps prevent usage of resources after they have been released.</remarks>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Releases all resources used by the current instance.
    /// </summary>
    /// <remarks>Call this method when you are finished using the instance to free unmanaged resources
    /// promptly. After calling <see cref="Dispose"/>, the instance should not be used.</remarks>
    public void Dispose()
    {
        // Dispose of unmanaged resources
        if (!_disposed)
        {
            // Dispose reader if not leaving open
            if (!_leaveOpen)
            {
                _reader.Close();
                _reader.Dispose();
            }
            _disposed = true;
        }
    }
}
