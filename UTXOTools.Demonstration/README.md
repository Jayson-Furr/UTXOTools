# UTXOTools.Demonstration

A command-line utility for reading, analyzing, and exporting Bitcoin UTXO (Unspent Transaction Output) data from Bitcoin Core's UTXO snapshot files.

## Overview

UTXOTools.Demonstration provides a comprehensive set of commands for working with Bitcoin UTXO files, including:

- Viewing UTXO file metadata and statistics
- Listing and exporting UTXO entries
- Extracting specific script types to binary files for further analysis

## Requirements

- .NET 10 or later
- Bitcoin Core UTXO snapshot file (version 2 format, Bitcoin Core 30.x)

## Building

```bash
dotnet build
```

## Usage

```
UTXOTools.Demonstration <command> [options]
```

## Commands

### General Commands

| Command | Description |
|---------|-------------|
| `info <file>` | Show header information from a UTXO file |
| `list <file> [options]` | List UTXO entries from a file |
| `export <file> <output>` | Export UTXO data to various formats |
| `validate <file>` | Validate a UTXO file |
| `stats <file>` | Show statistics about the UTXO set |
| `help` | Show help message |
| `version` | Show version information |

### Script Extraction Commands

These commands scan the UTXO set for specific script types and export the extracted data to binary files.

| Command | Description |
|---------|-------------|
| `p2pk <file> <output>` | Extract P2PK public keys |
| `p2pkh <file> <output>` | Extract P2PKH public key hashes |
| `p2ms <file> <output>` | Extract P2MS (bare multisig) data |
| `p2sh <file> <output>` | Extract P2SH script hashes |
| `p2sh-p2wpkh <file> <output>` | Extract P2SH script hashes (potential P2SH-P2WPKH) |
| `p2sh-p2wsh <file> <output>` | Extract P2SH script hashes (potential P2SH-P2WSH) |
| `p2wpkh <file> <output>` | Extract P2WPKH witness public key hashes |
| `p2wsh <file> <output>` | Extract P2WSH witness script hashes |
| `p2tr <file> <output>` | Extract P2TR taproot output keys |

## Command Details

### info

Display header information from a UTXO file.

```bash
UTXOTools.Demonstration info utxo.dat
```

### list

List UTXO entries with optional filtering and formatting.

```bash
UTXOTools.Demonstration list utxo.dat --limit 100 --json
```

**Options:**
- `--limit <n>` - Limit output to first n entries (default: 10)
- `--offset <n>` - Skip first n entries (default: 0)
- `--txid <hex>` - Filter by transaction ID
- `--json` - Output in JSON format
- `--csv` - Output in CSV format

### export

Export UTXO data to JSON or CSV format.

```bash
UTXOTools.Demonstration export utxo.dat output.json --format json
```

**Options:**
- `--format <fmt>` - Output format: `json`, `csv` (default: json)
- `--limit <n>` - Limit to first n entries

### validate

Validate the integrity of a UTXO file.

```bash
UTXOTools.Demonstration validate utxo.dat
```

### stats

Display comprehensive statistics about the UTXO set.

```bash
UTXOTools.Demonstration stats utxo.dat
```

## Script Type Extraction

All script extraction commands support the following options:

- `--include-amount` - Include the satoshi amount (8 bytes) for each entry
- `--verbose` - Show detailed progress during scanning

### P2PK (Pay-to-Public-Key)

Extracts public keys from legacy P2PK outputs.

```bash
UTXOTools.Demonstration p2pk utxo.dat p2pk_keys.bin --include-amount
```

**Script format:** `<pubkey> OP_CHECKSIG`

**Binary output format:**
- Header: `P2PK` (4 bytes) + entry count (4 bytes) + flags (1 byte)
- Per entry: [amount (8 bytes, optional)] + key length (1 byte) + public key (33 or 65 bytes)

### P2PKH (Pay-to-Public-Key-Hash)

Extracts public key hashes from P2PKH outputs.

```bash
UTXOTools.Demonstration p2pkh utxo.dat p2pkh_hashes.bin
```

**Script format:** `OP_DUP OP_HASH160 <20-byte hash> OP_EQUALVERIFY OP_CHECKSIG`

**Binary output format:**
- Header: `P2KH` (4 bytes) + entry count (4 bytes) + flags (1 byte)
- Per entry: [amount (8 bytes, optional)] + public key hash (20 bytes)

### P2MS (Pay-to-Multisig)

Extracts M-of-N multisig data including all public keys.

```bash
UTXOTools.Demonstration p2ms utxo.dat p2ms_multisig.bin --verbose
```

**Script format:** `OP_M <pubkey1> ... <pubkeyN> OP_N OP_CHECKMULTISIG`

**Binary output format:**
- Header: `P2MS` (4 bytes) + entry count (4 bytes) + flags (1 byte)
- Per entry: [amount (8 bytes, optional)] + M (1 byte) + N (1 byte) + [key length (1 byte) + public key]×N

### P2SH (Pay-to-Script-Hash)

Extracts script hashes from P2SH outputs.

```bash
UTXOTools.Demonstration p2sh utxo.dat p2sh_hashes.bin
```

**Script format:** `OP_HASH160 <20-byte hash> OP_EQUAL`

**Binary output format:**
- Header: `P2SH` (4 bytes) + entry count (4 bytes) + flags (1 byte)
- Per entry: [amount (8 bytes, optional)] + script hash (20 bytes)

### P2SH-P2WPKH and P2SH-P2WSH

These commands extract P2SH outputs that could potentially be wrapped SegWit outputs.

```bash
UTXOTools.Demonstration p2sh-p2wpkh utxo.dat p2sh_p2wpkh_hashes.bin
UTXOTools.Demonstration p2sh-p2wsh utxo.dat p2sh_p2wsh_hashes.bin
```

> **Note:** P2SH-P2WPKH and P2SH-P2WSH outputs cannot be distinguished from regular P2SH outputs at the scriptPubKey level. The redeem script (which identifies the wrapped SegWit type) is only revealed when the output is spent. These commands extract all P2SH outputs for external matching against known wrapped SegWit script hashes.

**Binary output format:**
- P2SH-P2WPKH: Header `SHWP` + entries with 20-byte script hashes
- P2SH-P2WSH: Header `SHWS` + entries with 20-byte script hashes

### P2WPKH (Pay-to-Witness-Public-Key-Hash)

Extracts witness public key hashes from native SegWit v0 outputs.

```bash
UTXOTools.Demonstration p2wpkh utxo.dat p2wpkh_hashes.bin
```

**Script format:** `OP_0 <20-byte hash>`

**Binary output format:**
- Header: `WPKH` (4 bytes) + entry count (4 bytes) + flags (1 byte)
- Per entry: [amount (8 bytes, optional)] + witness public key hash (20 bytes)

### P2WSH (Pay-to-Witness-Script-Hash)

Extracts witness script hashes from native SegWit v0 outputs.

```bash
UTXOTools.Demonstration p2wsh utxo.dat p2wsh_hashes.bin
```

**Script format:** `OP_0 <32-byte hash>`

**Binary output format:**
- Header: `PWSH` (4 bytes) + entry count (4 bytes) + flags (1 byte)
- Per entry: [amount (8 bytes, optional)] + witness script hash (32 bytes)

### P2TR (Pay-to-Taproot)

Extracts taproot output keys (x-only public keys) from SegWit v1 outputs.

```bash
UTXOTools.Demonstration p2tr utxo.dat p2tr_keys.bin
```

**Script format:** `OP_1 <32-byte x-only pubkey>`

**Binary output format:**
- Header: `P2TR` (4 bytes) + entry count (4 bytes) + flags (1 byte)
- Per entry: [amount (8 bytes, optional)] + taproot output key (32 bytes)

## Binary File Format

All script extraction commands produce binary files with a common header structure:

| Offset | Size | Description |
|--------|------|-------------|
| 0 | 4 bytes | Magic identifier (ASCII, e.g., "P2PK", "P2TR") |
| 4 | 4 bytes | Entry count (uint32, little-endian) |
| 8 | 1 byte | Flags (0x01 = includes amount) |
| 9 | varies | Entry data |

## Examples

```bash
# View UTXO file information
UTXOTools.Demonstration info utxo.dat

# Get statistics about the UTXO set
UTXOTools.Demonstration stats utxo.dat

# List first 50 UTXOs in JSON format
UTXOTools.Demonstration list utxo.dat --limit 50 --json

# Export all UTXOs to CSV
UTXOTools.Demonstration export utxo.dat all_utxos.csv --format csv

# Extract all P2TR outputs with amounts
UTXOTools.Demonstration p2tr utxo.dat taproot_outputs.bin --include-amount --verbose

# Extract P2PKH hashes
UTXOTools.Demonstration p2pkh utxo.dat p2pkh_hashes.bin

# Extract bare multisig data
UTXOTools.Demonstration p2ms utxo.dat multisig_data.bin --verbose
```

## Obtaining UTXO Snapshot Files

UTXO snapshot files can be created using Bitcoin Core's `dumptxoutset` RPC command:

```bash
bitcoin-cli dumptxoutset /path/to/utxo.dat
```

This creates a snapshot of the current UTXO set that can be analyzed with UTXOTools.

## License

See the repository root for license information.
