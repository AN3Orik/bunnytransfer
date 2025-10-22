namespace BunnyTransfer.NET
{
    /// <summary>
    /// Options for sync command
    /// </summary>
    public class SyncOptions
    {
        // Required
        public string LocalPath { get; set; } = string.Empty; // REQUIRED: Local directory path
        public string StorageZone { get; set; } = string.Empty; // REQUIRED: BunnyCDN storage zone name
        public string AccessKey { get; set; } = string.Empty; // REQUIRED: Access key (or from env)
        
        // Optional
        public string Direction { get; set; } = "upload"; // upload (default) or download
        public string Region { get; set; } = "de";
        public string Profile { get; set; } = string.Empty;
        public string RemotePath { get; set; } = string.Empty; // Optional remote subdirectory
        public bool Verbose { get; set; }
        public bool DryRun { get; set; }
        public List<string> UploadLastPatterns { get; set; } = new List<string>();
        public int ParallelUploads { get; set; } = 16;
    }
}
