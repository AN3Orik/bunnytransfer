# BunnyTransfer.NET

Fast file synchronization tool for BunnyCDN Storage with Native AOT compilation.

## Features

- Native AOT compilation - single executable, no dependencies
- Parallel uploads/downloads with configurable concurrency
- SHA256 checksum verification - skip unchanged files
- Real-time progress tracking
- Smart upload order - HTML/XML files last, custom patterns supported
- Bidirectional sync - upload or download
- Cross-platform - Windows, Linux, macOS

## Installation

Download pre-built binaries from [Releases](../../releases):

- Windows x64: `bunnytransfer-win-x64.exe`
- Linux x64: `bunnytransfer-linux-x64`
- macOS ARM64: `bunnytransfer-macos-arm64`
- macOS Intel: `bunnytransfer-macos-x64`

## Usage

### Upload files

```bash
bunnytransfer sync --local-path ./dist --storage-zone my-zone --access-key YOUR_KEY
```

### Upload to subdirectory

```bash
bunnytransfer sync --local-path ./dist --storage-zone my-zone --access-key YOUR_KEY --remote-path v1.2.3
```

### Download files

```bash
bunnytransfer sync --local-path ./backup --storage-zone my-zone --access-key YOUR_KEY --direction download
```

### Custom upload order

```bash
bunnytransfer sync --local-path ./dist --storage-zone my-zone --access-key YOUR_KEY --upload-last "*.hash,manifest.json"
```

## Options

Required:
- `--local-path <path>` - Local directory path
- `--storage-zone <name>` - BunnyCDN storage zone name
- `--access-key <key>` - BunnyCDN API access key

Optional:
- `--direction <dir>` - Sync direction: upload or download (default: upload)
- `--remote-path <path>` - Remote subdirectory within storage zone
- `--region <region>` - Storage zone region: de, ny, sg (default: de)
- `-j, --parallel <num>` - Number of parallel operations 1-64 (default: 16)
- `--upload-last <patterns>` - Comma-separated file patterns to upload last
- `-v, --verbose` - Enable verbose output
- `--dry-run` - Show what would be synced without syncing
- `-h, --help` - Show help message

## Environment Variables

Set access key via environment variable:

```bash
# Linux/macOS
export BUNNY_ACCESS_KEY=your_access_key

# Windows
set BUNNY_ACCESS_KEY=your_access_key
```

## Building from Source

```bash
# Windows
build_win.bat

# Linux
./build_linux.sh

# macOS
./build_macos.sh

# Universal (auto-detect platform)
./build.sh
```

Output: `BunnyTransfer.NET/bin/Release/net9.0/<platform>/publish/bunnytransfer`

## Creating a Release

```bash
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions will automatically build binaries for all platforms and create a release.

## License

MIT License

## Author

ANZO © 2025