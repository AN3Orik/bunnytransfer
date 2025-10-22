namespace BunnyCDN.Net.Storage
{
    public class BunnyCDNStorageAuthenticationException : BunnyCDNStorageException
    {
        public BunnyCDNStorageAuthenticationException(string storageZoneName, string accessKey) 
            : base($"Authentication failed for storage zone '{storageZoneName}' with access key '{accessKey}'.") { }
    }
}
