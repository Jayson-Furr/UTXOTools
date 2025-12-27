# UTXOTools.TXOutset

A .NET library for reading, parsing, writing, and validating Bitcoin UTXO (Unspent Transaction Output) snapshot files created by Bitcoin Core's `dumptxoutset` RPC command.

## Overview

UTXOTools.TXOutset provides a complete implementation of the Bitcoin Core UTXO file format, enabling developers to:

- **Read UTXO files**: Parse UTXO snapshot files created by Bitcoin Core 30.x
- **Write UTXO files**: Create UTXO snapshot files compatible with Bitcoin Core
- **Streaming support**: Process large UTXO files efficiently without loading everything into memory
- **Multi-network support**: Supports Mainnet, Signet, Testnet3, Testnet4, and Regtest
- **Full encoding support**: Handles CompactSize, VarInt, compressed amounts, and scriptPubKeys
- **Public key operations**: Compress and decompress secp256k1 public keys

## Requirements

- .NET 10 or later

## Installation

Add a reference to the `UTXOTools.TXOutset` project in your solution.

## Quick Start

### Reading a UTXO File

```csharp
using UTXOTools.TXOutset.IO;
using UTXOTools.TXOutset.Models;

// Open and read a UTXO snapshot file
using var reader = new UtxoFileReader("utxo.dat");

// Read the header
UtxoFileHeader header = reader.ReadHeader();
Console.WriteLine($"Network: {header.Network}");
Console.WriteLine($"Block Hash: {header.GetBlockHashHex()}");
Console.WriteLine($"UTXO Count: {header.UtxoCount:N0}");

// Stream through entries
foreach (var entry in reader.ReadEntries())
{
    Console.WriteLine($"TxId: {entry.GetTxIdHex()}");
    Console.WriteLine($"Vout: {entry.Vout}");
    Console.WriteLine($"Amount: {entry.GetAmountInBtc()} BTC");
    Console.WriteLine($"Script: {entry.GetScriptPubKeyHex()}");
}
```

### Reading by Transaction Groups

UTXOs in the file are stored grouped by transaction ID. Reading by transaction is more efficient:

```csharp
using var reader = new UtxoFileReader("utxo.dat");
reader.ReadHeader();

// Read transactions (UTXOs grouped by txid)
foreach (var tx in reader.ReadTransactions())
{
    Console.WriteLine($"Transaction: {tx.GetTxIdHex()}");
    Console.WriteLine($"  Outputs: {tx.OutputCount}");
    Console.WriteLine($"  Total Amount: {tx.TotalAmount} sats");
    
    foreach (var output in tx.Outputs)
    {
        Console.WriteLine($"    vout {output.Vout}: {output.Amount} sats");
    }
}
```

### Writing a UTXO File

```csharp
using UTXOTools.TXOutset.IO;
using UTXOTools.TXOutset.Models;

// Create a new UTXO file
using var writer = new UtxoFileWriter("output.dat", overwrite: true);

// Create and write the header
var header = UtxoFileHeader.Create(
    BitcoinNetwork.Mainnet,
    blockHash, // 32-byte block hash
    0          // UTXO count (updated at finalization)
);
writer.WriteHeader(header);

// Write transactions
writer.WriteTransaction(transaction);

// Finalize (updates the UTXO count in header)
writer.Finalize();
```

### Validating a UTXO File

```csharp
using var reader = new UtxoFileReader("utxo.dat");

try
{
    reader.Validate();
    Console.WriteLine("File is valid!");
}
catch (UtxoFormatException ex)
{
    Console.WriteLine($"Invalid file: {ex.Message}");
}
```

## Core Components

### Models

#### `UtxoFileHeader`

Represents the file header containing metadata:

| Property | Type | Description |
|----------|------|-------------|
| `Version` | `ushort` | File format version (currently 2) |
| `Network` | `BitcoinNetwork` | Network type (Mainnet, Testnet3, etc.) |
| `NetworkMagic` | `byte[]` | Raw 4-byte network magic |
| `BlockHash` | `byte[]` | 32-byte block hash (display order) |
| `UtxoCount` | `ulong` | Total number of UTXO entries |

**Methods:**
- `GetBlockHashHex()` - Returns the block hash as a hex string
- `Create(network, blockHash, utxoCount)` - Factory method to create headers

#### `UtxoEntry`

Represents a single unspent transaction output:

| Property | Type | Description |
|----------|------|-------------|
| `TxId` | `byte[]` | 32-byte transaction ID (display order) |
| `Vout` | `ulong` | Output index within the transaction |
| `Height` | `uint` | Block height where output was created |
| `IsCoinbase` | `bool` | Whether this is a coinbase output |
| `Amount` | `ulong` | Amount in satoshis |
| `ScriptPubKey` | `byte[]` | The locking script |

**Methods:**
- `GetTxIdHex()` - Returns the transaction ID as a hex string
- `GetScriptPubKeyHex()` - Returns the scriptPubKey as a hex string
- `GetAmountInBtc()` - Returns the amount in BTC (decimal)
- `GetOutpoint()` - Returns the outpoint string (`txid:vout`)

#### `UtxoTransaction`

Represents a group of UTXOs sharing the same transaction ID:

| Property | Type | Description |
|----------|------|-------------|
| `TxId` | `byte[]` | 32-byte transaction ID |
| `Outputs` | `List<UtxoOutput>` | List of outputs for this transaction |
| `OutputCount` | `int` | Number of outputs |
| `TotalAmount` | `ulong` | Sum of all output amounts |

**Methods:**
- `GetTxIdHex()` - Returns the transaction ID as a hex string
- `ToEntries()` - Converts to individual `UtxoEntry` objects

#### `UtxoOutput`

Represents a single output within a transaction:

| Property | Type | Description |
|----------|------|-------------|
| `Vout` | `ulong` | Output index |
| `Height` | `uint` | Block height |
| `IsCoinbase` | `bool` | Coinbase flag |
| `Amount` | `ulong` | Amount in satoshis |
| `ScriptPubKey` | `byte[]` | The locking script |

### IO Classes

#### `UtxoFileReader`

Provides forward-only reading of UTXO files:

```csharp
// From file path
using var reader = new UtxoFileReader("utxo.dat");

// From stream
using var reader = new UtxoFileReader(stream, leaveOpen: false);

// Properties
UtxoFileHeader? Header { get; }  // Cached header (null until read)
ulong EntriesRead { get; }       // Number of entries read
long Position { get; }           // Current stream position
long Length { get; }             // Stream length

// Methods
UtxoFileHeader ReadHeader();                    // Read and cache header
IEnumerable<UtxoEntry> ReadEntries();          // Stream all entries
IEnumerable<UtxoTransaction> ReadTransactions(); // Stream transactions
UtxoTransaction? ReadNextTransaction();         // Read next transaction
bool Validate();                                // Validate file integrity
void Reset();                                   // Reset to beginning
```

#### `UtxoFileWriter`

Provides writing of UTXO files:

```csharp
// Create new file
using var writer = new UtxoFileWriter("output.dat", overwrite: true);

// From stream
using var writer = new UtxoFileWriter(stream, leaveOpen: false);

// Properties
ulong EntriesWritten { get; }  // Number of entries written
bool HeaderWritten { get; }    // Whether header has been written

// Methods
void WriteHeader(UtxoFileHeader header);           // Write header (required first)
void WriteEntry(UtxoEntry entry);                  // Write single entry
void WriteTransaction(UtxoTransaction tx);         // Write transaction
void WriteTransactions(IEnumerable<UtxoTransaction> txs); // Write multiple
void UpdateUtxoCount(ulong count);                 // Update header count
void Finalize();                                   // Update count and flush
void Flush();                                      // Flush buffers
```

### Encoding Classes

#### `AmountCompression`

Compresses and decompresses satoshi amounts using Bitcoin Core's compression scheme:

```csharp
// Compress an amount
ulong compressed = AmountCompression.CompressAmount(50000000); // 0.5 BTC

// Decompress an amount
ulong satoshis = AmountCompression.DecompressAmount(compressed);
```

#### `VarIntEncoding`

Handles Bitcoin's variable-length integer encoding (7 bits per byte, continuation bit):

```csharp
// Reading from stream
ulong value = VarIntEncoding.ReadVarInt(reader);

// Writing to stream
VarIntEncoding.WriteVarInt(writer, value);

// Encode to byte array
byte[] encoded = VarIntEncoding.EncodeVarInt(value);

// Get encoded length
int length = VarIntEncoding.GetVarIntLength(value);
```

#### `CompactSizeEncoding`

Handles Bitcoin's CompactSize encoding for sizes and counts:

```csharp
// Reading from stream
ulong size = CompactSizeEncoding.ReadCompactSize(reader);

// Writing to stream
CompactSizeEncoding.WriteCompactSize(writer, size);

// Get encoded length
int length = CompactSizeEncoding.GetCompactSizeLength(value);

// Maximum allowed size
const ulong MaxSize = 0x02000000; // 32 MB
```

#### `PubKeyCompression`

Handles secp256k1 public key compression and decompression:

```csharp
// Decompress a 33-byte compressed key to 65-byte uncompressed
if (PubKeyCompression.TryDecompressPubKey(compressed, out byte[]? uncompressed))
{
    // Use uncompressed key (65 bytes: 0x04 + x + y)
}

// Decompress from x-coordinate and prefix
if (PubKeyCompression.TryDecompressPubKey(xCoordinate, prefix, out byte[]? uncompressed))
{
    // Use uncompressed key
}

// Compress a 65-byte uncompressed key to 33-byte compressed
if (PubKeyCompression.TryCompressPubKey(uncompressed, out byte[]? compressed))
{
    // Use compressed key (33 bytes: prefix + x)
}

// Validate keys
bool isValid = PubKeyCompression.IsValidCompressedPubKey(compressedKey);
bool isValid = PubKeyCompression.IsValidUncompressedPubKey(uncompressedKey);
bool isValid = PubKeyCompression.IsValidCompressedPubKey(xCoordinate, prefix);
```

### Network Support

The library supports all Bitcoin networks via the `BitcoinNetwork` enum:

| Network | Magic Bytes | Description |
|---------|-------------|-------------|
| `Mainnet` | `f9beb4d9` | Production network |
| `Testnet3` | `0b110907` | Third test network |
| `Testnet4` | `1c163f28` | Fourth test network |
| `Signet` | `0a03cf40` | Signature-based test network |
| `Regtest` | `fabfb5da` | Local regression testing |
| `Unknown` | - | Unrecognized network |

**Utility methods in `UtxoFileConstants`:**

```csharp
// Get network from magic bytes
BitcoinNetwork network = UtxoFileConstants.GetNetworkFromMagic(magicBytes);

// Get magic bytes from network
byte[] magic = UtxoFileConstants.GetMagicFromNetwork(BitcoinNetwork.Mainnet);

// Check version support
bool supported = UtxoFileConstants.IsVersionSupported(version);
```

## File Format

The UTXO file format (version 2) consists of:

### Header (51 bytes)

| Offset | Size | Description |
|--------|------|-------------|
| 0 | 5 | File magic (`utxo\xff` = `0x75 0x74 0x78 0x6f 0xff`) |
| 5 | 2 | Version (little-endian, currently 2) |
| 7 | 4 | Network magic |
| 11 | 32 | Block hash (reversed byte order) |
| 43 | 8 | UTXO count (little-endian) |

### Transaction Records

Entries are grouped by transaction ID:

| Field | Encoding | Description |
|-------|----------|-------------|
| TxId | 32 bytes | Transaction ID (reversed byte order) |
| Output Count | CompactSize | Number of outputs in this transaction |
| Outputs | Variable | Output records |

### Output Records

Each output contains:

| Field | Encoding | Description |
|-------|----------|-------------|
| Vout | CompactSize | Output index |
| Height+Coinbase | VarInt | `(height << 1) \| coinbase_flag` |
| Amount | VarInt | Compressed amount |
| ScriptPubKey | Variable | Compressed script |

### Script Compression

| Type Byte | Script Type | Data |
|-----------|-------------|------|
| `0x00` | P2PKH | 20-byte pubkey hash |
| `0x01` | P2SH | 20-byte script hash |
| `0x02` | P2PK (compressed, even y) | 32-byte x-coordinate |
| `0x03` | P2PK (compressed, odd y) | 32-byte x-coordinate |
| `0x04` | P2PK (uncompressed, even y) | 32-byte x-coordinate |
| `0x05` | P2PK (uncompressed, odd y) | 32-byte x-coordinate |
| `6+` | Raw script | `(type - 6)` bytes of raw script |

## Exceptions

| Exception | Description |
|-----------|-------------|
| `UtxoException` | Base exception for all UTXO-related errors |
| `UtxoFormatException` | Invalid file format, corrupted data, or unexpected EOF |
| `UtxoVersionException` | Unsupported file version |
| `UtxoValidationException` | Data validation failures |

## Thread Safety

`UtxoFileReader` and `UtxoFileWriter` are **not** thread-safe. Callers should synchronize access when using from multiple threads.

## Examples

### Count UTXOs by Script Type

```csharp
using var reader = new UtxoFileReader("utxo.dat");
reader.ReadHeader();

var scriptTypes = new Dictionary<string, ulong>();

foreach (var entry in reader.ReadEntries())
{
    string type = ClassifyScript(entry.ScriptPubKey);
    scriptTypes.TryGetValue(type, out ulong count);
    scriptTypes[type] = count + 1;
}

foreach (var kvp in scriptTypes.OrderByDescending(x => x.Value))
{
    Console.WriteLine($"{kvp.Key}: {kvp.Value:N0}");
}
```

### Calculate Total Value

```csharp
using var reader = new UtxoFileReader("utxo.dat");
var header = reader.ReadHeader();

decimal totalBtc = 0;
foreach (var tx in reader.ReadTransactions())
{
    totalBtc += tx.TotalAmount / 100_000_000m;
}

Console.WriteLine($"Total: {totalBtc:N8} BTC across {header.UtxoCount:N0} UTXOs");
```

### Filter and Copy UTXOs

```csharp
using var reader = new UtxoFileReader("input.dat");
using var writer = new UtxoFileWriter("filtered.dat", overwrite: true);

var header = reader.ReadHeader();
writer.WriteHeader(UtxoFileHeader.Create(header.Network, header.BlockHash, 0));

foreach (var tx in reader.ReadTransactions())
{
    // Filter: only include outputs >= 1 BTC
    var filtered = tx.Outputs.Where(o => o.Amount >= 100_000_000).ToList();
    if (filtered.Count > 0)
    {
        var filteredTx = new UtxoTransaction { TxId = tx.TxId, Outputs = filtered };
        writer.WriteTransaction(filteredTx);
    }
}

writer.Finalize();
```

### Stream Processing with Progress

```csharp
using var reader = new UtxoFileReader("utxo.dat");
var header = reader.ReadHeader();

ulong processed = 0;
foreach (var tx in reader.ReadTransactions())
{
    processed += (ulong)tx.OutputCount;
    
    if (processed % 1_000_000 == 0)
    {
        double pct = (double)processed / header.UtxoCount * 100;
        Console.Write($"\rProgress: {pct:F1}%");
    }
    
    // Process transaction...
}
Console.WriteLine("\nDone!");
```

## Project Structure

```
UTXOTools.TXOutset/
??? Encoding/
?   ??? AmountCompression.cs      # Amount compression/decompression
?   ??? CompactSizeEncoding.cs    # CompactSize read/write
?   ??? PubKeyCompression.cs      # Public key compression
?   ??? VarIntEncoding.cs         # VarInt read/write
??? Exceptions/
?   ??? UtxoException.cs          # Base exception
?   ??? UtxoFormatException.cs    # Format errors
?   ??? UtxoValidationException.cs# Validation errors
?   ??? UtxoVersionException.cs   # Version errors
??? IO/
?   ??? UtxoFileReader.cs         # File reading
?   ??? UtxoFileWriter.cs         # File writing
??? Models/
?   ??? UtxoEntry.cs              # Single UTXO entry
?   ??? UtxoFileHeader.cs         # Header model
?   ??? UtxoTransaction.cs        # Transaction group + UtxoOutput
??? Scripts/
?   ??? ScriptPubKeyDecoder.cs    # Script encoding/decoding
??? BitcoinNetwork.cs             # Network enum
??? UtxoFileConstants.cs          # File format constants
```

## Creating UTXO Snapshot Files

UTXO snapshot files can be created using Bitcoin Core's `dumptxoutset` RPC command:

```bash
bitcoin-cli dumptxoutset /path/to/utxo.dat
```

This creates a snapshot of the current UTXO set that can be read with this library.

## Supported Versions

| Version | Bitcoin Core | Status |
|---------|--------------|--------|
| 2 | 30.x | ? Supported |

The library is designed to be extensible for future versions.

## License

See the repository root for license information.
