namespace BunnyCDN.Net.Storage
{
    public class BunnyCDNStorageFileNotFoundException : BunnyCDNStorageException
    {
        public BunnyCDNStorageFileNotFoundException(string path) 
            : base($"Could not find part of the object path: {path}") { }
    }
}
