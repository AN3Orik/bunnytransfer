using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace BunnyTransfer.NET
{
    /// <summary>
    /// Handles progress display for file uploads/downloads with live updates
    /// </summary>
    public class ProgressDisplay : IDisposable
    {
        private readonly ConcurrentDictionary<string, FileProgress> _activeFiles = new();
        private readonly object _consoleLock = new();
        private readonly Timer _updateTimer;
        private readonly int _maxDisplayFiles;
        private int _totalFiles;
        private int _completedFiles;
        private long _totalBytes;
        private long _completedBytes;
        private readonly Stopwatch _overallTimer = new();
        private int _lastLineCount = 0;

        public class FileProgress
        {
            public string FileName { get; set; } = string.Empty;
            public long TotalSize { get; set; }
            public long TransferredSize { get; set; }
            public Stopwatch Timer { get; set; } = Stopwatch.StartNew();
            public bool IsCompleted { get; set; }
            public DateTime? CompletedTime { get; set; }
        }

        public ProgressDisplay(int maxDisplayFiles = 10)
        {
            _maxDisplayFiles = maxDisplayFiles;
            _updateTimer = new Timer(UpdateDisplay, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
            _overallTimer.Start();
        }

        public void SetTotalFiles(int count, long totalBytes)
        {
            _totalFiles = count;
            _totalBytes = totalBytes;
        }

        public void AddToTotal(long bytes)
        {
            Interlocked.Add(ref _totalBytes, bytes);
        }

        public void StartFile(string fileName, long fileSize)
        {
            _activeFiles[fileName] = new FileProgress
            {
                FileName = fileName,
                TotalSize = fileSize,
                TransferredSize = 0,
                IsCompleted = false
            };
        }

        public void UpdateFileProgress(string fileName, long transferredBytes)
        {
            if (_activeFiles.TryGetValue(fileName, out var progress))
            {
                progress.TransferredSize = transferredBytes;
            }
        }

        public void CompleteFile(string fileName)
        {
            if (_activeFiles.TryGetValue(fileName, out var progress))
            {
                progress.IsCompleted = true;
                progress.CompletedTime = DateTime.Now;
                progress.TransferredSize = progress.TotalSize; // Ensure 100%
                Interlocked.Increment(ref _completedFiles);
                Interlocked.Add(ref _completedBytes, progress.TotalSize);
            }
        }

        private void UpdateDisplay(object? state)
        {
            lock (_consoleLock)
            {
                try
                {
                    // Remove files that have been completed for more than 2 seconds
                    var now = DateTime.Now;
                    var filesToRemove = _activeFiles
                        .Where(kvp => kvp.Value.IsCompleted && 
                                     kvp.Value.CompletedTime.HasValue && 
                                     (now - kvp.Value.CompletedTime.Value).TotalSeconds > 2)
                        .Select(kvp => kvp.Key)
                        .ToList();
                    
                    foreach (var fileName in filesToRemove)
                    {
                        _activeFiles.TryRemove(fileName, out _);
                    }

                    // Clear previous lines
                    if (_lastLineCount > 0)
                    {
                        Console.SetCursorPosition(0, Math.Max(0, Console.CursorTop - _lastLineCount));
                        for (int i = 0; i < _lastLineCount; i++)
                        {
                            Console.Write(new string(' ', Console.BufferWidth - 1));
                            Console.WriteLine();
                        }
                        Console.SetCursorPosition(0, Math.Max(0, Console.CursorTop - _lastLineCount));
                    }

                    var lines = 0;

                    // Calculate current transferred bytes including in-progress files
                    var currentTransferred = _completedBytes;
                    foreach (var file in _activeFiles.Values.Where(f => !f.IsCompleted))
                    {
                        currentTransferred += file.TransferredSize;
                    }

                    // Overall progress based on bytes, not file count
                    var overallPercent = _totalBytes > 0 ? (currentTransferred * 100.0 / _totalBytes) : 0;
                    var overallSpeed = _overallTimer.ElapsedMilliseconds > 0 
                        ? (currentTransferred / 1024.0 / 1024.0) / (_overallTimer.ElapsedMilliseconds / 1000.0)
                        : 0;

                    var header = $"Overall: {_completedFiles}/{_totalFiles} files ({overallPercent:F1}%) | " +
                                $"{FormatBytes(currentTransferred)}/{FormatBytes(_totalBytes)} | " +
                                $"{overallSpeed:F2} MB/s | {FormatTime(_overallTimer.Elapsed)}";
                    Console.WriteLine(header);
                    lines++;

                    // Progress bar
                    var barWidth = Math.Min(50, Console.BufferWidth - 10);
                    var filled = (int)(barWidth * overallPercent / 100.0);
                    var progressBar = $"[{new string('█', filled)}{new string('░', barWidth - filled)}]";
                    Console.WriteLine(progressBar);
                    lines++;

                    Console.WriteLine();
                    lines++;

                    // Active files (show both in-progress and recently completed)
                    var activeFiles = _activeFiles.Values
                        .OrderByDescending(f => f.IsCompleted) // Completed files first
                        .ThenByDescending(f => f.TransferredSize)
                        .Take(_maxDisplayFiles)
                        .ToList();

                    if (activeFiles.Any())
                    {
                        Console.WriteLine("Active transfers:");
                        lines++;

                        foreach (var file in activeFiles)
                        {
                            var percent = file.TotalSize > 0 ? (file.TransferredSize * 100.0 / file.TotalSize) : 0;
                            var speed = file.Timer.ElapsedMilliseconds > 0
                                ? (file.TransferredSize / 1024.0 / 1024.0) / (file.Timer.ElapsedMilliseconds / 1000.0)
                                : 0;

                            var fileBarWidth = 20;
                            var fileFilled = (int)(fileBarWidth * percent / 100.0);
                            var fileBar = file.IsCompleted 
                                ? $"[{new string('█', fileBarWidth)}]" // Fully filled for completed
                                : $"[{new string('█', fileFilled)}{new string('░', fileBarWidth - fileFilled)}]";

                            var fileName = file.FileName.Length > 40 
                                ? "..." + file.FileName.Substring(file.FileName.Length - 37)
                                : file.FileName;

                            var status = file.IsCompleted ? "✓" : " ";
                            var line = $"  {status}{fileBar} {percent,5:F1}% {fileName,-40} {FormatBytes(file.TransferredSize),10}/{FormatBytes(file.TotalSize),-10} {speed,7:F2} MB/s";
                            
                            // Truncate line if too long
                            if (line.Length > Console.BufferWidth - 1)
                            {
                                line = line.Substring(0, Console.BufferWidth - 1);
                            }
                            
                            Console.WriteLine(line);
                            lines++;
                        }
                    }

                    // Show how many more files are being processed
                    var remainingActive = _activeFiles.Values.Count(f => !f.IsCompleted) - activeFiles.Count(f => !f.IsCompleted);
                    if (remainingActive > 0)
                    {
                        Console.WriteLine($"  ... and {remainingActive} more file(s) transferring");
                        lines++;
                    }

                    _lastLineCount = lines;
                }
                catch
                {
                    // Ignore console errors during resize or other issues
                }
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F2} GB";
            if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F2} MB";
            if (bytes >= 1_024) return $"{bytes / 1_024.0:F2} KB";
            return $"{bytes} B";
        }

        private static string FormatTime(TimeSpan time)
        {
            if (time.TotalHours >= 1)
                return $"{(int)time.TotalHours}h {time.Minutes}m {time.Seconds}s";
            if (time.TotalMinutes >= 1)
                return $"{(int)time.TotalMinutes}m {time.Seconds}s";
            return $"{time.Seconds}s";
        }

        public void Dispose()
        {
            _updateTimer?.Dispose();
            
            // Final update
            lock (_consoleLock)
            {
                try
                {
                    if (_lastLineCount > 0)
                    {
                        Console.SetCursorPosition(0, Math.Max(0, Console.CursorTop - _lastLineCount));
                        
                        // Clear all progress lines
                        for (int i = 0; i < _lastLineCount; i++)
                        {
                            Console.Write(new string(' ', Console.BufferWidth - 1));
                            Console.WriteLine();
                        }
                        
                        Console.SetCursorPosition(0, Math.Max(0, Console.CursorTop - _lastLineCount));
                        _lastLineCount = 0;
                    }
                }
                catch
                {
                    Console.WriteLine();
                }
            }
        }
    }
}
