using System;
using System.Collections.Generic;
using System.Text.Json;
using BunnyCDN.Net.Storage.Models;

namespace BunnyCDN.Net.Storage
{
    internal class Serializer
    {
        /// <summary>Deserializes JSON data using AOT-compatible source generator</summary>
        public static T? Deserialize<T>(string data)
        {
            if (typeof(T) == typeof(StorageObject[]))
                return (T)(object)JsonSerializer.Deserialize(data, BunnyCDNStorageJsonContext.Default.StorageObjectArray)!;
            else if (typeof(T) == typeof(List<StorageObject>))
                return (T)(object)JsonSerializer.Deserialize(data, BunnyCDNStorageJsonContext.Default.ListStorageObject)!;
            else if (typeof(T) == typeof(StorageObject))
                return (T)(object)JsonSerializer.Deserialize(data, BunnyCDNStorageJsonContext.Default.StorageObject)!;
            
            throw new NotSupportedException($"Type {typeof(T)} is not supported for deserialization");
        }

        /// <summary>Serializes an object using AOT-compatible source generator</summary>
        public static string Serialize<T>(object value)
        {
            if (value is StorageObject[] arrayValue)
                return JsonSerializer.Serialize(arrayValue, BunnyCDNStorageJsonContext.Default.StorageObjectArray);
            else if (value is List<StorageObject> listValue)
                return JsonSerializer.Serialize(listValue, BunnyCDNStorageJsonContext.Default.ListStorageObject);
            else if (value is StorageObject objectValue)
                return JsonSerializer.Serialize(objectValue, BunnyCDNStorageJsonContext.Default.StorageObject);
            
            throw new NotSupportedException($"Type {value.GetType()} is not supported for serialization");
        }
    }
}