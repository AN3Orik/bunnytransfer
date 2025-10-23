using System.Security.Cryptography;
using BunnyCDN.Net.Storage;
using BunnyCDN.Net.Storage.Models;

namespace BunnyTransfer.NET.Implementation
{
    /// <summary>
    /// Main sync engine that handles the synchronization logic
    /// </summary>
    public class SyncEngine(SyncOptions options)
    {
        private BunnyCDNStorage? _storage;

        public async Task ExecuteAsync()
        {
            _storage = new BunnyCDNStorage(options.StorageZone, options.AccessKey, options.Region);

            if (_storage == null)
            {
                throw new InvalidOperationException("Failed to initialize storage client.");
            }

            var direction = options.Direction.ToLower();
            var directionText = direction == "upload" ? "Local → Remote" : "Remote → Local";
            
            Console.WriteLine($"Starting sync: {directionText}");
            Console.WriteLine($"Local Path: {options.LocalPath}");
            Console.WriteLine($"Storage Zone: {options.StorageZone}");
            Console.WriteLine($"Region: {options.Region}");
            if (!string.IsNullOrWhiteSpace(options.RemotePath))
            {
                Console.WriteLine($"Remote Path: /{options.RemotePath.Trim('/')}");
            }
            
            if (options.DryRun)
            {
                Console.WriteLine("DRY RUN MODE - No changes will be made");
            }

            Console.WriteLine();

            if (direction == "download")
            {
                await SyncFromRemoteToLocalAsync();
            }
            else
            {
                await SyncFromLocalToRemoteAsync();
            }

            Console.WriteLine();
            Console.WriteLine("Sync completed successfully!");
        }

        private async Task SyncFromLocalToRemoteAsync()
        {
            var localPath = Path.GetFullPath(options.LocalPath);
            var storageZoneName = options.StorageZone;

            if (!Directory.Exists(localPath))
            {
                throw new DirectoryNotFoundException($"Local directory not found: {localPath}");
            }
            
            var localFiles = GetLocalFiles(localPath);
            Console.WriteLine($"Found {localFiles.Count} local file(s)");
            
            var remoteBasePath = string.IsNullOrWhiteSpace(options.RemotePath) 
                ? $"{storageZoneName}/" 
                : $"{storageZoneName}/{options.RemotePath.Trim('/')}/";
            
            var remoteFiles = await GetRemoteFilesRecursiveAsync(remoteBasePath);
            Console.WriteLine($"Found {remoteFiles.Count} remote file(s)");
            
            var localFileMap = BuildLocalFileMap(localFiles, localPath, remoteBasePath.TrimEnd('/'));
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
                var isCustomLast = options.UploadLastPatterns.Any(pattern => 
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
            
            int uploaded = 0;
            int skipped = 0;

            Console.WriteLine();
            Console.WriteLine($"Syncing files (parallel: {options.ParallelUploads})...");
            Console.WriteLine();
            
            var filesToUpload = otherFiles.Count + htmlFiles.Count + customLastFiles.Count;

            using (var progress = new ProgressDisplay())
            {
                progress.SetTotalFiles(filesToUpload, 0);
                
                var (uploadedOther, skippedOther) = await UploadFilesParallelAsync(otherFiles, localFileMap, remoteFileMap, "", progress);
                uploaded += uploadedOther;
                skipped += skippedOther;
                
                var (uploadedHtml, skippedHtml) = await UploadFilesParallelAsync(htmlFiles, localFileMap, remoteFileMap, "HTML/XML", progress);
                uploaded += uploadedHtml;
                skipped += skippedHtml;
                
                var (uploadedCustom, skippedCustom) = await UploadFilesParallelAsync(customLastFiles, localFileMap, remoteFileMap, "LAST", progress);
                uploaded += uploadedCustom;
                skipped += skippedCustom;
            }
            
            var filesToDelete = remoteFileMap.Keys.Except(localFileMap.Keys).ToList();
            
            Console.WriteLine();
            Console.WriteLine("Cleaning up deleted files...");

            foreach (var fileToDelete in filesToDelete)
            {
                Console.WriteLine($"[DELETE] {fileToDelete}");

                if (!options.DryRun)
                {
                    await _storage!.DeleteObjectAsync(fileToDelete);
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Summary: {uploaded} uploaded, {skipped} skipped, {filesToDelete.Count} deleted");
        }

        private async Task SyncFromRemoteToLocalAsync()
        {
            var storageZoneName = options.StorageZone;
            var localPath = Path.GetFullPath(options.LocalPath);

            if (!Directory.Exists(localPath))
            {
                Directory.CreateDirectory(localPath);
            }
            
            var remoteBasePath = string.IsNullOrWhiteSpace(options.RemotePath) 
                ? $"{storageZoneName}/" 
                : $"{storageZoneName}/{options.RemotePath.Trim('/')}/";
            
            var remoteFiles = await GetRemoteFilesRecursiveAsync(remoteBasePath);
            Console.WriteLine($"Found {remoteFiles.Count} remote file(s)");
            
            var localFiles = GetLocalFiles(localPath);
            Console.WriteLine($"Found {localFiles.Count} local file(s)");

            BuildLocalFileMap(localFiles, localPath, remoteBasePath.TrimEnd('/'));

            int downloaded;
            int skipped;

            Console.WriteLine();
            Console.WriteLine($"Syncing files (parallel: {options.ParallelUploads})...");
            Console.WriteLine();
            
            var filesToProcess = remoteFiles
                .Where(f => !f.IsDirectory)
                .Select(f =>
                {
                    var remoteBaseLength = remoteBasePath.TrimEnd('/').Length;
                    var relativePath = f.FullPath.Length > remoteBaseLength 
                        ? f.FullPath[(remoteBaseLength + 1)..].TrimStart('/')
                        : f.ObjectName;
                    return (remoteFile: f, relativePath);
                })
                .ToList();

            using (var progress = new ProgressDisplay())
            {
                progress.SetTotalFiles(filesToProcess.Count, 0);
                
                var semaphore = new SemaphoreSlim(options.ParallelUploads, options.ParallelUploads);
                var tasks = new List<Task<(bool downloaded, bool skipped)>>();

                foreach (var (remoteFile, relativePath) in filesToProcess)
                {
                    await semaphore.WaitAsync();

                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            var localFilePath = Path.Combine(localPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

                            if (File.Exists(localFilePath))
                            {
                                if (!string.IsNullOrEmpty(remoteFile.Checksum))
                                {
                                    var localHash = CalculateFileHash(localFilePath);

                                    if (localHash.Equals(remoteFile.Checksum, StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (options.Verbose)
                                        {
                                            Console.WriteLine($"[SKIP] {relativePath} (unchanged)");
                                        }
                                        return (false, true);
                                    }
                                }
                                else
                                {
                                    var localFileInfo = new FileInfo(localFilePath);
                                    if (localFileInfo.Length == remoteFile.Length)
                                    {
                                        if (options.Verbose)
                                        {
                                            Console.WriteLine($"[SKIP] {relativePath} (same size, no checksum)");
                                        }
                                        return (false, true);
                                    }
                                }
                            }
                            
                            progress.AddToTotal(remoteFile.Length);

                            if (!options.DryRun)
                            {
                                var directory = Path.GetDirectoryName(localFilePath);
                                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                                {
                                    Directory.CreateDirectory(directory);
                                }
                                
                                progress.StartFile(relativePath, remoteFile.Length);
                                
                                await _storage!.DownloadObjectAsync(remoteFile.FullPath, localFilePath, (bytesDownloaded) =>
                                {
                                    progress.UpdateFileProgress(relativePath, bytesDownloaded);
                                });
                                
                                progress.CompleteFile(relativePath);
                            }
                            else
                            {
                                if (options.Verbose)
                                {
                                    Console.WriteLine($"[DRY-RUN DOWNLOAD] {relativePath}");
                                }
                            }

                            return (true, false);
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

                if (!options.DryRun)
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
            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(stream);
            return Convert.ToHexString(hash);
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

            var semaphore = new SemaphoreSlim(options.ParallelUploads, options.ParallelUploads);
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

                        if (remoteFileMap.TryGetValue(remoteFilePath, out var remoteObject))
                        {
                            if (!string.IsNullOrEmpty(remoteObject.Checksum))
                            {
                                var localHash = CalculateFileHash(localFilePath);
                                
                                if (localHash.Equals(remoteObject.Checksum, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (options.Verbose)
                                    {
                                        Console.WriteLine($"[SKIP] {remoteFilePath} (unchanged)");
                                    }
                                    return (false, true);
                                }
                            }
                            else if (remoteObject.Length == fileInfo.Length)
                            {
                                if (options.Verbose)
                                {
                                    Console.WriteLine($"[SKIP] {remoteFilePath} (same size, no checksum)");
                                }
                                return (false, true);
                            }
                        }
                        
                        progress?.AddToTotal(fileInfo.Length);
                        progress?.StartFile(remoteFilePath, fileInfo.Length);

                        if (!options.DryRun)
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
                    finally
                    {
                        semaphore.Release();
                    }
                });

                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);
            var uploadedCount = results.Count(r => r.uploaded);
            var skippedCount = results.Count(r => r.skipped);

            return (uploadedCount, skippedCount);
        }
    }
}
