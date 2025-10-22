using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using BunnyCDN.Net.Storage.Models;

namespace BunnyCDN.Net.Storage
{
    public class BunnyCDNStorage
    {
        public string ApiAccessKey { get; private set; }
        public string StorageZoneName { get; private set; }
        private HttpClient _http = null!;

        /// <summary>Creates a new BunnyCDN Storage client</summary>
        public BunnyCDNStorage(string storageZoneName, string apiAccessKey, string mainReplicationRegion = "de", HttpMessageHandler? handler = null)
        {
            this.ApiAccessKey = apiAccessKey;
            this.StorageZoneName = storageZoneName;

            _http = handler != null ? new HttpClient(handler) : new HttpClient();
            _http.Timeout = new TimeSpan(0, 0, 120);
            _http.DefaultRequestHeaders.Add("AccessKey", this.ApiAccessKey);
            _http.BaseAddress = new Uri(this.GetBaseAddress(mainReplicationRegion));
        }

        /// <summary>Deletes an object or directory (including all contents)</summary>
        public async Task<bool> DeleteObjectAsync(string path)
        {
            var normalizedPath = NormalizePath(path);
            try
            {
                var response = await _http.DeleteAsync(normalizedPath);
                return response.IsSuccessStatusCode;
            }
            catch(WebException ex)
            {
                throw this.MapResponseToException((HttpStatusCode)(int)ex.Status, path);
            }
        }

        /// <summary>Lists all storage objects at the given path</summary>
        public async Task<List<StorageObject>> GetStorageObjectsAsync(string path)
        {
            var normalizedPath = this.NormalizePath(path, true);
            var response = await _http.GetAsync(normalizedPath);
            if (!response.IsSuccessStatusCode)
                throw this.MapResponseToException(response.StatusCode, normalizedPath);

            var responseJson = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseJson))
                return new List<StorageObject>();

            return Serializer.Deserialize<List<StorageObject>>(responseJson) ?? new List<StorageObject>();
        }

        /// <summary>Uploads a stream to storage</summary>
        public async Task UploadAsync(Stream stream, string path, string? sha256Checksum = null, string contentTypeOverride = "")
            => await UploadAsync(stream, path, false, sha256Checksum, contentTypeOverride);

        /// <summary>Uploads a stream to storage with optional checksum validation</summary>
        public async Task UploadAsync(Stream stream, string path, bool validateChecksum, string? sha256Checksum = null, string contentTypeOverride = "")
        {
            var normalizedPath = this.NormalizePath(path, false);
            using (var content = new StreamContent(stream))
            {
                var message = new HttpRequestMessage(HttpMethod.Put, normalizedPath)
                {
                    Content = content
                };

                if (validateChecksum && string.IsNullOrWhiteSpace(sha256Checksum))
                {
                    if (!stream.CanSeek)
                        throw new BunnyCDNStorageChecksumException("Unable to generate checksum for non-seekable stream.", string.Empty);

                    long startPosition = stream.Position;
                    sha256Checksum = Checksum.Generate(stream);
                    stream.Position = startPosition;
                }

                if (!string.IsNullOrWhiteSpace(sha256Checksum))
                    message.Headers.Add("Checksum", sha256Checksum);

                if(!string.IsNullOrWhiteSpace(contentTypeOverride))
                    message.Headers.Add("Override-Content-Type", contentTypeOverride);

                var response = await _http.SendAsync(message);
                if(!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.BadRequest && !string.IsNullOrWhiteSpace(sha256Checksum))
                        throw new BunnyCDNStorageChecksumException(normalizedPath, sha256Checksum);
                    else
                        throw this.MapResponseToException(response.StatusCode, normalizedPath);
                }
            }
        }

        /// <summary>Uploads a local file to storage</summary>
        public async Task UploadAsync(string localFilePath, string path, string? sha256Checksum = null, string contentTypeOverride = "")
            => await UploadAsync(localFilePath, path, false, sha256Checksum, contentTypeOverride);

        /// <summary>Uploads a local file to storage with optional checksum validation</summary>
        public async Task UploadAsync(string localFilePath, string path, bool validateChecksum, string? sha256Checksum = null, string contentTypeOverride = "")
        {
            using (var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 64))
            {
                await UploadAsync(fileStream, path, validateChecksum, sha256Checksum, contentTypeOverride);
            }
        }

        /// <summary>Uploads a local file to storage with progress reporting</summary>
        public async Task UploadWithProgressAsync(string localFilePath, string path, bool validateChecksum, Action<long>? progressCallback, string? sha256Checksum = null, string contentTypeOverride = "")
        {
            using (var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 64))
            {
                var normalizedPath = this.NormalizePath(path, false);
                
                if (validateChecksum && string.IsNullOrWhiteSpace(sha256Checksum))
                {
                    long startPosition = fileStream.Position;
                    sha256Checksum = Checksum.Generate(fileStream);
                    fileStream.Position = startPosition;
                }

                using (var content = new ProgressHttpContent(fileStream, progressCallback))
                {
                    var message = new HttpRequestMessage(HttpMethod.Put, normalizedPath)
                    {
                        Content = content
                    };

                    if (!string.IsNullOrWhiteSpace(sha256Checksum))
                        message.Headers.Add("Checksum", sha256Checksum);

                    if (!string.IsNullOrWhiteSpace(contentTypeOverride))
                        message.Headers.Add("Override-Content-Type", contentTypeOverride);

                    var response = await _http.SendAsync(message);
                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.BadRequest && !string.IsNullOrWhiteSpace(sha256Checksum))
                            throw new BunnyCDNStorageChecksumException(normalizedPath, sha256Checksum);
                        else
                            throw this.MapResponseToException(response.StatusCode, normalizedPath);
                    }
                }
            }
        }

        /// <summary>Downloads an object to a local file</summary>
        public async Task DownloadObjectAsync(string path, string localFilePath)
        {
            var normalizedPath = this.NormalizePath(path);
            try
            {
                using (var stream = await this.DownloadObjectAsStreamAsync(normalizedPath))
                {
                    using (var bufferedStream = new BufferedStream(stream, 1024 * 64))
                    {
                        using (var fileStream = new FileStream(localFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 1024 * 64))
                        {
                            bufferedStream.CopyTo(fileStream, 1024 * 64);
                        }
                    }
                }
            }
            catch(WebException ex)
            {
                throw this.MapResponseToException((HttpStatusCode)(int)ex.Status, path);
            }
        }

        /// <summary>Downloads an object to a local file with progress reporting</summary>
        public async Task DownloadObjectAsync(string path, string localFilePath, Action<long>? progressCallback)
        {
            var normalizedPath = this.NormalizePath(path);
            try
            {
                using (var stream = await this.DownloadObjectAsStreamAsync(normalizedPath))
                {
                    using (var fileStream = new FileStream(localFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 1024 * 64))
                    {
                        byte[] buffer = new byte[64 * 1024]; // 64KB buffer
                        long totalBytesRead = 0;
                        int bytesRead;

                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;
                            progressCallback?.Invoke(totalBytesRead);
                        }
                    }
                }
            }
            catch(WebException ex)
            {
                throw this.MapResponseToException((HttpStatusCode)(int)ex.Status, path);
            }
        }

        /// <summary>Returns a stream with the contents of the object</summary>
        public async Task<Stream> DownloadObjectAsStreamAsync(string path)
        {
            try
            {
                var normalizedPath = this.NormalizePath(path, false);
                return await _http.GetStreamAsync(normalizedPath);
            }
            catch (WebException ex)
            {
                throw this.MapResponseToException((HttpStatusCode)(int)ex.Status, path);
            }
        }

        private BunnyCDNStorageException MapResponseToException(HttpStatusCode statusCode, string path)
        {
            switch (statusCode)
            {
                case HttpStatusCode.NotFound:
                    return new BunnyCDNStorageFileNotFoundException(path);
                case HttpStatusCode.Unauthorized:
                    return new BunnyCDNStorageAuthenticationException(StorageZoneName, ApiAccessKey);
                default:
                    return new BunnyCDNStorageException("An unknown error has occured during the request.");
            }
        }

        /// <summary>Normalizes a path string for API calls</summary>
        public string NormalizePath(string path, bool? isDirectory = null)
        {
            // Trim all prepending & tailing whitespace, fix windows-like paths then remove prepending slashes
            path = path.Trim()
                .Replace("\\", "/")
                .TrimStart('/');

            if (!path.StartsWith($"{StorageZoneName}/"))
                throw new BunnyCDNStorageException($"Path validation failed. File path must begin with /{StorageZoneName}/.");

            if (isDirectory.HasValue)
            {
                if (isDirectory.Value)
                    path = path.TrimEnd('/') + "/";
                else if (path.EndsWith("/"))
                    throw new BunnyCDNStorageException("The requested path is invalid, cannot be directory.");
            }

            while (path.Contains("//"))
                path = path.Replace("//", "/");

            return path;
        }

        /// <summary>Gets the base HTTP URL address of the storage endpoint</summary>
        private string GetBaseAddress(string mainReplicationRegion)
        {
            if(mainReplicationRegion == "" || mainReplicationRegion.ToLower() == "de")
            {
                return "https://storage.bunnycdn.com/";
            }

            return $"https://{mainReplicationRegion}.storage.bunnycdn.com/";
        }
    }
}
