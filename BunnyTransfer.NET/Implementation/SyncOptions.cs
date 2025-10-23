namespace BunnyTransfer.NET.Implementation
{
    /// <summary>
    /// Options for sync command
    /// </summary>
    public class SyncOptions
    {
        // Required
        public string LocalPath { get; set; } = string.Empty;
        public string StorageZone { get; set; } = string.Empty;
        public string AccessKey { get; set; } = string.Empty;
        
        // Optional
        public string Direction { get; set; } = "upload";
        public string Region { get; set; } = "de";
        public string Profile { get; set; } = string.Empty;
        public string RemotePath { get; set; } = string.Empty; 
        public bool Verbose { get; set; }
        public bool DryRun { get; set; }
        public List<string> UploadLastPatterns { get; set; } = [];
        public int ParallelUploads { get; set; } = 16;
    }
}