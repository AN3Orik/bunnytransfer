using System.Collections.Generic;
using System.Text.Json.Serialization;
using BunnyCDN.Net.Storage.Models;

namespace BunnyCDN.Net.Storage
{
    [JsonSerializable(typeof(StorageObject))]
    [JsonSerializable(typeof(StorageObject[]))]
    [JsonSerializable(typeof(List<StorageObject>))]
    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    internal partial class BunnyCDNStorageJsonContext : JsonSerializerContext
    {
    }
}
