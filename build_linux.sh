#!/bin/bash

# Build script for Linux x64 Native AOT

echo "Building BunnyTransfer.NET for Linux x64..."
echo ""

# Clean previous build
echo "Cleaning previous build..."
rm -rf BunnyTransfer.NET/bin/Release/net9.0/linux-x64

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore BunnyTransfer.NET/BunnyTransfer.NET.csproj

# Build and publish Native AOT for Linux x64
echo "Building Native AOT binary for Linux x64..."
dotnet publish BunnyTransfer.NET/BunnyTransfer.NET.csproj -c Release -r linux-x64 --self-contained

echo ""
echo "Build completed!"
echo "Output: BunnyTransfer.NET/bin/Release/net9.0/linux-x64/publish/"
echo ""
ls -lh BunnyTransfer.NET/bin/Release/net9.0/linux-x64/publish/bunnytransfer
