using BunnyCDN.Net.Storage;
using BunnyCDN.Net.Storage.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace BunnyTransfer.NET
{
    /// <summary>
    /// Main sync engine that handles the synchronization logic
    /// </summary>
    public class SyncEngine
    {
        private readonly SyncOptions _options;
        private BunnyCDNStorage? _storage;

        public SyncEngine(SyncOptions options)
        {
            _options = options;
        }

        public async Task ExecuteAsync()
        {
            _storage = new BunnyCDNStorage(_options.StorageZone, _options.AccessKey, _options.Region);

            if (_storage == null)
            {
                throw new InvalidOperationException("Failed to initialize storage client.");
            }

            var direction = _options.Direction.ToLower();
            var directionText = direction == "upload" ? "Local → Remote" : "Remote → Local";
            
            Console.WriteLine($"Starting sync: {directionText}");
            Console.WriteLine($"Local Path: {_options.LocalPath}");
            Console.WriteLine($"Storage Zone: {_options.StorageZone}");
            Console.WriteLine($"Region: {_options.Region}");
            if (!string.IsNullOrWhiteSpace(_options.RemotePath))
            {
                Console.WriteLine($"Remote Path: /{_options.RemotePath.Trim('/')}");
            }
            
            if (_options.DryRun)
            {
                Console.WriteLine("DRY RUN MODE - No changes will be made");
            }

            Console.WriteLine();

            if (direction == "download")
            {
                await SyncFromRemoteToLocalAsync();
            }
            else // upload
            {
                await SyncFromLocalToRemoteAsync();
            }

            Console.WriteLine();
            Console.WriteLine("Sync completed successfully!");
        }

        private async Task SyncFromLocalToRemoteAsync()
        {
            var localPath = Path.GetFullPath(_options.LocalPath);
            var storageZoneName = _options.StorageZone;

            if (!Directory.Exists(localPath))
            {
                throw new DirectoryNotFoundException($"Local directory not found: {localPath}");
            }

            // Get all local files
            var localFiles = GetLocalFiles(localPath);
            Console.WriteLine($"Found {localFiles.Count} local file(s)");

            // Build remote path with optional subdirectory
            var remoteBasePath = string.IsNullOrWhiteSpace(_options.RemotePath) 
                ? $"{storageZoneName}/" 
                : $"{storageZoneName}/{_options.RemotePath.Trim('/')}/";

            // Get all remote files
            var remoteFiles = await GetRemoteFilesRecursiveAsync(remoteBasePath);
            Console.WriteLine($"Found {remoteFiles.Count} remote file(s)");

            // Build file maps
            var localFileMap = BuildLocalFileMap(localFiles, localPath, remoteBasePath.TrimEnd('/'));
            // FullPath starts with "/" so we need to match it in local map keys
            var remoteFileMap = remoteFiles.ToDictionary(f => f.FullPath.TrimStart('/'), f => f);

            // Separate files for processing order: 
            // 1. Regular files (first)
            // 2. HTML/XML files (second to last)
            // 3. Custom pattern files specified by --upload-last (last)
            var customLastFiles = new List<string>();
            var htmlFiles = new List<string>();
            var otherFiles = new List<string>();

            foreach (var localFile in localFileMap.Keys)
            {
                var fileName = Path.GetFileName(localFileMap[localFile]);
                var isCustomLast = _options.UploadLastPatterns.Any(pattern => 
                    fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                    localFile.EndsWith("/" + pattern, StringComparison.OrdinalIgnoreCase));

                if (isCustomLast)
                {
                    customLastFiles.Add(localFile);
                }
                else if (localFile.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                         localFile.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) ||
                         localFile.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    htmlFiles.Add(localFile);
                }
                else
                {
                    otherFiles.Add(localFile);
                }
            }

            // Upload non-HTML files first
            int uploaded = 0;
            int skipped = 0;

            Console.WriteLine();
            Console.WriteLine($"Syncing files (parallel: {_options.ParallelUploads})...");
            Console.WriteLine();

            // Calculate number of files to potentially upload
            // Total bytes will be calculated dynamically as we determine which files need uploading
            var filesToUpload = otherFiles.Count + htmlFiles.Count + customLastFiles.Count;

            using (var progress = new ProgressDisplay())
            {
                progress.SetTotalFiles(filesToUpload, 0); // Start with 0 bytes, will add as we go

                // Upload other files in parallel
                var (uploadedOther, skippedOther) = await UploadFilesParallelAsync(otherFiles, localFileMap, remoteFileMap, "", progress);
                uploaded += uploadedOther;
                skipped += skippedOther;

                // Upload HTML/XML files
                var (uploadedHtml, skippedHtml) = await UploadFilesParallelAsync(htmlFiles, localFileMap, remoteFileMap, "HTML/XML", progress);
                uploaded += uploadedHtml;
                skipped += skippedHtml;

                // Upload custom pattern files last (e.g., hash files, manifests)
                var (uploadedCustom, skippedCustom) = await UploadFilesParallelAsync(customLastFiles, localFileMap, remoteFileMap, "LAST", progress);
                uploaded += uploadedCustom;
                skipped += skippedCustom;
            }

            // Delete remote files that don't exist locally
            var filesToDelete = remoteFileMap.Keys.Except(localFileMap.Keys).ToList();
            
            Console.WriteLine();
            Console.WriteLine("Cleaning up deleted files...");

            foreach (var fileToDelete in filesToDelete)
            {
                Console.WriteLine($"[DELETE] {fileToDelete}");

                if (!_options.DryRun)
                {
                    await _storage!.DeleteObjectAsync(fileToDelete);
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Summary: {uploaded} uploaded, {skipped} skipped, {filesToDelete.Count} deleted");
        }

        private async Task SyncFromRemoteToLocalAsync()
        {
            var storageZoneName = _options.StorageZone;
            var localPath = Path.GetFullPath(_options.LocalPath);

            if (!Directory.Exists(localPath))
            {
                Directory.CreateDirectory(localPath);
            }

            // Build remote path with optional subdirectory
            var remoteBasePath = string.IsNullOrWhiteSpace(_options.RemotePath) 
                ? $"{storageZoneName}/" 
                : $"{storageZoneName}/{_options.RemotePath.Trim('/')}/";

            // Get all remote files
            var remoteFiles = await GetRemoteFilesRecursiveAsync(remoteBasePath);
            Console.WriteLine($"Found {remoteFiles.Count} remote file(s)");

            // Get all local files
            var localFiles = GetLocalFiles(localPath);
            Console.WriteLine($"Found {localFiles.Count} local file(s)");

            var localFileMap = BuildLocalFileMap(localFiles, localPath, remoteBasePath.TrimEnd('/'));

            int downloaded = 0;
            int skipped = 0;

            Console.WriteLine();
            Console.WriteLine($"Syncing files (parallel: {_options.ParallelUploads})...");
            Console.WriteLine();

            // Prepare file list with metadata
            var filesToProcess = remoteFiles
                .Where(f => !f.IsDirectory)
                .Select(f =>
                {
                    var remoteBaseLength = remoteBasePath.TrimEnd('/').Length;
                    var relativePath = f.FullPath.Length > remoteBaseLength 
                        ? f.FullPath.Substring(remoteBaseLength + 1).TrimStart('/')
                        : f.ObjectName;
                    return (remoteFile: f, relativePath);
                })
                .ToList();

            using (var progress = new ProgressDisplay())
            {
                progress.SetTotalFiles(filesToProcess.Count, 0); // Start with 0 bytes, will add as we go

                // Download files in parallel
                var semaphore = new SemaphoreSlim(_options.ParallelUploads, _options.ParallelUploads);
                var tasks = new List<Task<(bool downloaded, bool skipped)>>();

                foreach (var (remoteFile, relativePath) in filesToProcess)
                {
                    await semaphore.WaitAsync();

                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            var localFilePath = Path.Combine(localPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                            var needsDownload = true;

                            if (File.Exists(localFilePath))
                            {
                                // Use checksum from API if available
                                if (!string.IsNullOrEmpty(remoteFile.Checksum))
                                {
                                    var localHash = CalculateFileHash(localFilePath);

                                    if (localHash.Equals(remoteFile.Checksum, StringComparison.OrdinalIgnoreCase))
                                    {
                                        needsDownload = false;
                                        if (_options.Verbose)
                                        {
                                            Console.WriteLine($"[SKIP] {relativePath} (unchanged)");
                                        }
                                        return (false, true);
                                    }
                                }
                                else
                                {
                                    // Fallback: compare file sizes if checksum not available
                                    var localFileInfo = new FileInfo(localFilePath);
                                    if (localFileInfo.Length == remoteFile.Length)
                                    {
                                        needsDownload = false;
                                        if (_options.Verbose)
                                        {
                                            Console.WriteLine($"[SKIP] {relativePath} (same size, no checksum)");
                                        }
                                        return (false, true);
                                    }
                                }
                            }

                            if (needsDownload)
                            {
                                // Add file size to total bytes for progress calculation
                                progress.AddToTotal(remoteFile.Length);

                                if (!_options.DryRun)
                                {
                                    var directory = Path.GetDirectoryName(localFilePath);
                                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                                    {
                                        Directory.CreateDirectory(directory);
                                    }

                                    // Start tracking this file in progress display
                                    progress.StartFile(relativePath, remoteFile.Length);

                                    // Download with progress callback
                                    await _storage!.DownloadObjectAsync(remoteFile.FullPath, localFilePath, (bytesDownloaded) =>
                                    {
                                        progress.UpdateFileProgress(relativePath, bytesDownloaded);
                                    });

                                    // Mark file as complete
                                    progress.CompleteFile(relativePath);
                                }
                                else
                                {
                                    if (_options.Verbose)
                                    {
                                        Console.WriteLine($"[DRY-RUN DOWNLOAD] {relativePath}");
                                    }
                                }

                                return (true, false);
                            }

                            return (false, false);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    tasks.Add(task);
                }

                var results = await Task.WhenAll(tasks);
                downloaded = results.Count(r => r.downloaded);
                skipped = results.Count(r => r.skipped);
            }

            // Delete local files that don't exist remotely
            var remoteFilePaths = remoteFiles
                .Where(f => !f.IsDirectory)
                .Select(f => f.FullPath.TrimStart('/'))
                .ToHashSet();

            var filesToDelete = new List<string>();

            foreach (var localFile in localFiles)
            {
                var relativePath = Path.GetRelativePath(localPath, localFile);
                var remotePathForFile = $"{remoteBasePath.TrimEnd('/')}/{relativePath.Replace(Path.DirectorySeparatorChar, '/')}";

                if (!remoteFilePaths.Contains(remotePathForFile))
                {
                    filesToDelete.Add(localFile);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Cleaning up deleted files...");

            foreach (var fileToDelete in filesToDelete)
            {
                var relativePath = Path.GetRelativePath(localPath, fileToDelete);
                Console.WriteLine($"[DELETE] {relativePath}");

                if (!_options.DryRun)
                {
                    File.Delete(fileToDelete);
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Summary: {downloaded} downloaded, {skipped} skipped, {filesToDelete.Count} deleted");
        }

        private async Task<List<StorageObject>> GetRemoteFilesRecursiveAsync(string path)
        {
            var result = new List<StorageObject>();
            var objects = await _storage!.GetStorageObjectsAsync(path);

            foreach (var obj in objects)
            {
                if (obj.IsDirectory)
                {
                    // Use FullPath which already includes storage zone name, just add trailing slash for directories
                    var subDirectoryPath = obj.FullPath + "/";
                    var subObjects = await GetRemoteFilesRecursiveAsync(subDirectoryPath);
                    result.AddRange(subObjects);
                }
                else
                {
                    result.Add(obj);
                }
            }

            return result;
        }

        private List<string> GetLocalFiles(string path)
        {
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories).ToList();
        }

        private Dictionary<string, string> BuildLocalFileMap(List<string> localFiles, string localBasePath, string storageZoneName)
        {
            var map = new Dictionary<string, string>();

            foreach (var localFile in localFiles)
            {
                var relativePath = Path.GetRelativePath(localBasePath, localFile);
                var remotePath = $"{storageZoneName}/{relativePath.Replace(Path.DirectorySeparatorChar, '/')}";
                map[remotePath] = localFile;
            }

            return map;
        }

        private string CalculateFileHash(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        private async Task<(int uploaded, int skipped)> UploadFilesParallelAsync(
            List<string> remoteFilePaths,
            Dictionary<string, string> localFileMap,
            Dictionary<string, StorageObject> remoteFileMap,
            string label,
            ProgressDisplay? progress = null)
        {
            if (remoteFilePaths.Count == 0)
                return (0, 0);

            var uploadedCount = 0;
            var skippedCount = 0;
            var semaphore = new SemaphoreSlim(_options.ParallelUploads, _options.ParallelUploads);
            var tasks = new List<Task<(bool uploaded, bool skipped)>>();

            foreach (var remoteFilePath in remoteFilePaths)
            {
                await semaphore.WaitAsync();

                var task = Task.Run(async () =>
                {
                    try
                    {
                        var localFilePath = localFileMap[remoteFilePath];
                        var fileInfo = new FileInfo(localFilePath);
                        var needsUpload = true;

                        if (remoteFileMap.ContainsKey(remoteFilePath))
                        {
                            var remoteObject = remoteFileMap[remoteFilePath];
                            
                            // Use checksum from API if available, otherwise compare by downloading
                            if (!string.IsNullOrEmpty(remoteObject.Checksum))
                            {
                                var localHash = CalculateFileHash(localFilePath);
                                
                                if (localHash.Equals(remoteObject.Checksum, StringComparison.OrdinalIgnoreCase))
                                {
                                    needsUpload = false;
                                    if (_options.Verbose)
                                    {
                                        Console.WriteLine($"[SKIP] {remoteFilePath} (unchanged)");
                                    }
                                    return (false, true);
                                }
                            }
                            else if (remoteObject.Length == fileInfo.Length)
                            {
                                // Fallback: if checksum not available, compare file sizes
                                needsUpload = false;
                                if (_options.Verbose)
                                {
                                    Console.WriteLine($"[SKIP] {remoteFilePath} (same size, no checksum)");
                                }
                                return (false, true);
                            }
                        }

                        if (needsUpload)
                        {
                            // Add file size to total when we decide to upload it
                            progress?.AddToTotal(fileInfo.Length);
                            progress?.StartFile(remoteFilePath, fileInfo.Length);

                            if (!_options.DryRun)
                            {
                                await _storage!.UploadWithProgressAsync(
                                    localFilePath, 
                                    remoteFilePath, 
                                    true,
                                    bytesUploaded => progress?.UpdateFileProgress(remoteFilePath, bytesUploaded));
                            }

                            progress?.CompleteFile(remoteFilePath);
                            return (true, false);
                        }

                        return (false, false);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);
            uploadedCount = results.Count(r => r.uploaded);
            skippedCount = results.Count(r => r.skipped);

            return (uploadedCount, skippedCount);
        }
    }
}
