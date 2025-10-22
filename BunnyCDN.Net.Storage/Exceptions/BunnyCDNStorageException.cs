using System;

namespace BunnyCDN.Net.Storage
{
    public class BunnyCDNStorageException : Exception
    {
        public BunnyCDNStorageException(string message) : base(message) { }
    }
}
