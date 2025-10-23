#!/bin/bash

# Universal build script that detects the platform

echo "Detecting platform..."

# Detect OS and architecture
OS=$(uname -s)
ARCH=$(uname -m)

case "$OS" in
    Linux*)
        PLATFORM="linux-x64"
        ;;
    Darwin*)
        if [ "$ARCH" = "arm64" ]; then
            PLATFORM="osx-arm64"
        else
            PLATFORM="osx-x64"
        fi
        ;;
    *)
        echo "Unsupported OS: $OS"
        exit 1
        ;;
esac

echo "Building for platform: $PLATFORM"
echo ""

# Clean previous build
echo "Cleaning previous build..."
rm -rf BunnyTransfer.NET/bin/Release/net9.0/$PLATFORM

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore BunnyTransfer.NET/BunnyTransfer.NET.csproj

# Build and publish Native AOT
echo "Building Native AOT binary..."
dotnet publish BunnyTransfer.NET/BunnyTransfer.NET.csproj -c Release -r $PLATFORM --self-contained

echo ""
echo "Build completed!"
echo "Output: BunnyTransfer.NET/bin/Release/net9.0/$PLATFORM/publish/"
echo ""
ls -lh BunnyTransfer.NET/bin/Release/net9.0/$PLATFORM/publish/bunnytransfer
