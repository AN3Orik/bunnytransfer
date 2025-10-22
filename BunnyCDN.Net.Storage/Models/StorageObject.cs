using System;

namespace BunnyCDN.Net.Storage.Models
{
    public class StorageObject
    {
        public string Guid { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; }
        public DateTime LastChanged { get; set; }
        public string StorageZoneName { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string ObjectName { get; set; } = string.Empty;
        public long Length { get; set; }
        public bool IsDirectory { get; set; }
        public int ServerId { get; set; }
        public long StorageZoneId { get; set; }
        public string? Checksum { get; set; }
        public string FullPath => Path + ObjectName;
    }
}
