#!/bin/bash

# Build script for Linux x64 Native AOT

echo "Building BunnyTransfer.NET for Linux x64..."
echo ""

# Clean previous build
echo "Cleaning previous build..."
rm -rf bin/Release/net9.0/linux-x64

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore

# Build and publish Native AOT for Linux x64
echo "Building Native AOT binary for Linux x64..."
dotnet publish -c Release -r linux-x64 --self-contained

echo ""
echo "Build completed!"
echo "Output: bin/Release/net9.0/linux-x64/publish/"
echo ""
ls -lh bin/Release/net9.0/linux-x64/publish/bunnytransfer
