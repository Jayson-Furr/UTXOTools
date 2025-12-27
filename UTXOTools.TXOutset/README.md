# UTXOTools.TXOutset

A .NET library for reading, parsing, writing, and validating Bitcoin UTXO (Unspent Transaction Output) dump files created by Bitcoin Core's `dumptxoutset` command.

## Features

- **Read UTXO files**: Parse UTXO dump files created by Bitcoin Core 30.x
- **Write UTXO files**: Create UTXO dump files compatible with Bitcoin Core's `loadtxoutset` command
- **Streaming support**: Process large UTXO files efficiently without loading everything into memory
- **Multi-network support**: Supports Mainnet, Signet, Testnet3, Testnet4, and Regtest
- **Version-aware**: Designed to support multiple UTXO file format versions (currently supports version 2)
- **Full encoding support**: Handles CompactSize, VarInt, and compressed amounts
- **Script decoding**: Decodes compressed scriptPubKeys including P2PKH, P2SH, P2PK, and raw scripts

## Installation

Add a reference to the `UTXOTools.TXOutset` project in your solution.

## Quick Start

### Reading a UTXO File

```csharp
using UTXOTools.TXOutset.IO;
using UTXOTools.TXOutset.Models;

// Open and read a UTXO dump file
using var reader = new UtxoFileReader("path/to/utxo.dat");

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

```csharp
using var reader = new UtxoFileReader("path/to/utxo.dat");
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
    utxoCount  // Total number of UTXOs
);
writer.WriteHeader(header);

// Write transactions
writer.WriteTransaction(transaction);

// Finalize (updates the UTXO count in header if needed)
writer.Finalize();
```

### Validating a UTXO File

```csharp
using var reader = new UtxoFileReader("path/to/utxo.dat");

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

## File Format

The library supports UTXO dump files with the following structure:

### Header
| Field | Size | Description |
|-------|------|-------------|
| File Magic | 5 bytes | `0x75 0x74 0x78 0x6f 0xff` ("utxo" + 0xff) |
| Version | 2 bytes | File format version (uint16, little-endian) |
| Network Magic | 4 bytes | Network identifier |
| Block Hash | 32 bytes | Hash of the UTXO set's block (reversed) |
| UTXO Count | 8 bytes | Total number of UTXOs (uint64, little-endian) |

### Network Magic Values
| Network | Magic Bytes |
|---------|-------------|
| Mainnet | `0xf9 0xbe 0xb4 0xd9` |
| Signet | `0x0a 0x03 0xcf 0x40` |
| Testnet3 | `0x0b 0x11 0x09 0x07` |
| Testnet4 | `0x1c 0x16 0x3f 0x28` |
| Regtest | `0xfa 0xbf 0xb5 0xda` |

### Entries
Entries are grouped by transaction ID:
- Transaction ID (32 bytes, reversed)
- Output count (CompactSize)
- For each output:
  - Vout index (CompactSize)
  - Height and coinbase flag (VarInt, height << 1 | coinbase)
  - Compressed amount (VarInt)
  - Compressed scriptPubKey

## Encodings

### CompactSize
Variable-length integer encoding used for counts and sizes:
- 0x00-0xFC: Single byte
- 0xFD-0xFFFF: 0xFD + 2 bytes (little-endian)
- 0x10000-0xFFFFFFFF: 0xFE + 4 bytes (little-endian)
- 0x100000000+: 0xFF + 8 bytes (little-endian)

### VarInt
Variable-length integer encoding used for vout, height, and amounts:
- Uses 7 bits per byte for data
- High bit (0x80) indicates continuation
- Each continuation adds 1 to prevent ambiguity

### Script Compression
| Type Byte | Script Type | Data |
|-----------|-------------|------|
| 0x00 | P2PKH | 20-byte pubkey hash |
| 0x01 | P2SH | 20-byte script hash |
| 0x02 | P2PK (compressed, even y) | 32-byte x-coordinate |
| 0x03 | P2PK (compressed, odd y) | 32-byte x-coordinate |
| 0x04 | P2PK (uncompressed, even y) | 32-byte x-coordinate |
| 0x05 | P2PK (uncompressed, odd y) | 32-byte x-coordinate |
| 6+ | Raw script | (type - 6) bytes |

## Supported Versions

Currently supported UTXO file format versions:
- **Version 2**: Bitcoin Core 30.x format

The library is designed to be easily extensible to support future versions.

## Exception Handling

The library provides specific exception types for different error scenarios:

- `UtxoException`: Base exception for all UTXO-related errors
- `UtxoFormatException`: Invalid or corrupted file format
- `UtxoVersionException`: Unsupported file version
- `UtxoValidationException`: File validation failures

## Project Structure

```
UTXOTools.TXOutset/
??? Encoding/
?   ??? CompactSizeEncoding.cs    # CompactSize read/write
?   ??? VarIntEncoding.cs         # VarInt read/write
?   ??? AmountCompression.cs      # Amount compression/decompression
?   ??? PubKeyCompression.cs      # Public key compression
??? Exceptions/
?   ??? UtxoException.cs          # Base exception
?   ??? UtxoFormatException.cs    # Format errors
?   ??? UtxoVersionException.cs   # Version errors
?   ??? UtxoValidationException.cs# Validation errors
??? IO/
?   ??? UtxoFileReader.cs         # File reading
?   ??? UtxoFileWriter.cs         # File writing
??? Models/
?   ??? UtxoFileHeader.cs         # Header model
?   ??? UtxoEntry.cs              # Single UTXO entry
?   ??? UtxoTransaction.cs        # Transaction group
??? Scripts/
?   ??? ScriptPubKeyDecoder.cs    # Script encoding/decoding
??? BitcoinNetwork.cs             # Network enum
??? UtxoFileConstants.cs          # Constants
```

## Requirements

- .NET 10.0 or later

## License

MIT License
