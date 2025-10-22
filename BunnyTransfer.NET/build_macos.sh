#!/bin/bash

# Build script for macOS (Apple Silicon - ARM64)

echo "Building BunnyTransfer.NET for macOS ARM64..."
echo ""

# Clean previous build
echo "Cleaning previous build..."
rm -rf bin/Release/net9.0/osx-arm64

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore

# Build and publish Native AOT for macOS ARM64
echo "Building Native AOT binary for macOS ARM64..."
dotnet publish -c Release -r osx-arm64 --self-contained

echo ""
echo "Build completed!"
echo "Output: bin/Release/net9.0/osx-arm64/publish/"
echo ""
ls -lh bin/Release/net9.0/osx-arm64/publish/bunnytransfer
