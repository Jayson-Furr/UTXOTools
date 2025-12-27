# UTXOTools

A collection of .NET tools and libraries for working with Bitcoin UTXO (Unspent Transaction Output) data from Bitcoin Core.

## Overview

UTXOTools provides a complete toolkit for reading, writing, analyzing, and extracting data from Bitcoin Core's UTXO snapshot files created by the `dumptxoutset` RPC command.

## Projects

| Project | Description |
|---------|-------------|
| [UTXOTools.TXOutset](UTXOTools.TXOutset/README.md) | Core library for reading and writing UTXO snapshot files |
| [UTXOTools.Demonstration](UTXOTools.Demonstration/README.md) | Command-line utility demonstrating library usage |

## Requirements

- .NET 10 or later
- Bitcoin Core UTXO snapshot file (version 2 format, Bitcoin Core 30.x)

## Quick Start

### Building

```bash
dotnet build
```

### Creating a UTXO Snapshot

Use Bitcoin Core to create a UTXO snapshot file:

```bash
bitcoin-cli dumptxoutset /path/to/utxo.dat
```

### Using the Command-Line Tool

```bash
# View file information
dotnet run --project UTXOTools.Demonstration -- info utxo.dat

# Show statistics
dotnet run --project UTXOTools.Demonstration -- stats utxo.dat

# Extract P2TR (Taproot) outputs
dotnet run --project UTXOTools.Demonstration -- p2tr utxo.dat taproot.bin

# Extract P2PKH outputs with amounts
dotnet run --project UTXOTools.Demonstration -- p2pkh utxo.dat p2pkh.bin --include-amount
```

### Using the Library

```csharp
using UTXOTools.TXOutset.IO;
using UTXOTools.TXOutset.Models;

// Read a UTXO snapshot file
using var reader = new UtxoFileReader("utxo.dat");

var header = reader.ReadHeader();
Console.WriteLine($"Network: {header.Network}");
Console.WriteLine($"Block: {header.GetBlockHashHex()}");
Console.WriteLine($"UTXOs: {header.UtxoCount:N0}");

// Process all entries
foreach (var entry in reader.ReadEntries())
{
    Console.WriteLine($"{entry.GetOutpoint()}: {entry.Amount} sats");
}
```

## UTXOTools.TXOutset

The core library providing:

- **File I/O**: Read and write UTXO snapshot files with `UtxoFileReader` and `UtxoFileWriter`
- **Models**: `UtxoFileHeader`, `UtxoEntry`, `UtxoTransaction`, `UtxoOutput`
- **Encoding**: Bitcoin-compatible encoding for amounts, integers, and scripts
- **Public Key Operations**: Compress and decompress secp256k1 public keys
- **Multi-Network Support**: Mainnet, Testnet3, Testnet4, Signet, Regtest

See [UTXOTools.TXOutset/README.md](UTXOTools.TXOutset/README.md) for detailed documentation.

## UTXOTools.Demonstration

A command-line utility providing:

- **File Operations**: `info`, `list`, `export`, `validate`, `stats`
- **Script Extraction**: Extract specific script types to binary files
  - `p2pk` - Pay-to-Public-Key
  - `p2pkh` - Pay-to-Public-Key-Hash
  - `p2ms` - Pay-to-Multisig (bare multisig)
  - `p2sh` - Pay-to-Script-Hash
  - `p2sh-p2wpkh` - P2SH-wrapped P2WPKH
  - `p2sh-p2wsh` - P2SH-wrapped P2WSH
  - `p2wpkh` - Pay-to-Witness-Public-Key-Hash
  - `p2wsh` - Pay-to-Witness-Script-Hash
  - `p2tr` - Pay-to-Taproot

See [UTXOTools.Demonstration/README.md](UTXOTools.Demonstration/README.md) for detailed documentation.

## Supported Script Types

| Script Type | Description | Command |
|-------------|-------------|---------|
| P2PK | Legacy pay-to-public-key | `p2pk` |
| P2PKH | Legacy pay-to-public-key-hash | `p2pkh` |
| P2MS | Bare multisig (M-of-N) | `p2ms` |
| P2SH | Pay-to-script-hash | `p2sh` |
| P2SH-P2WPKH | Wrapped SegWit (P2WPKH in P2SH) | `p2sh-p2wpkh` |
| P2SH-P2WSH | Wrapped SegWit (P2WSH in P2SH) | `p2sh-p2wsh` |
| P2WPKH | Native SegWit v0 (20-byte witness) | `p2wpkh` |
| P2WSH | Native SegWit v0 (32-byte witness) | `p2wsh` |
| P2TR | Taproot (SegWit v1) | `p2tr` |

## File Format Support

| Version | Bitcoin Core | Status |
|---------|--------------|--------|
| 2 | 30.x | ? Supported |

## Network Support

| Network | Description |
|---------|-------------|
| Mainnet | Bitcoin production network |
| Testnet3 | Third test network |
| Testnet4 | Fourth test network |
| Signet | Signature-based test network |
| Regtest | Local regression testing |

## Project Structure

```
UTXOTools/
??? UTXOTools.sln                    # Solution file
??? README.md                        # This file
??? UTXOTools.TXOutset/              # Core library
?   ??? README.md
?   ??? IO/                          # File readers and writers
?   ??? Models/                      # Data models
?   ??? Encoding/                    # Bitcoin encodings
?   ??? Scripts/                     # Script handling
?   ??? Exceptions/                  # Custom exceptions
??? UTXOTools.Demonstration/         # CLI tool
    ??? README.md
    ??? Program.cs                   # Entry point
    ??? Commands/                    # Command implementations
```

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## License

See [LICENSE](LICENSE) for details.