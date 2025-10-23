@echo off
echo Building BunnyTransfer.NET for Windows x64...
echo.

dotnet publish BunnyTransfer.NET\BunnyTransfer.NET.csproj -r win-x64 -c Release /p:PublishSingleFile=true

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build successful!
    echo Output: BunnyTransfer.NET\bin\Release\net9.0\win-x64\publish\bunnytransfer.exe
) else (
    echo.
    echo Build failed!
)
