package com.god.webadmin.model.cdn.bunnycdn;

import com.fasterxml.jackson.annotation.JsonProperty;
import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import lombok.Getter;
import lombok.ToString;
import org.jetbrains.annotations.NotNull;

import java.io.IOException;
import java.io.InputStream;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;

/**
 * A client for interacting with the BunnyCDN Edge Storage API.
 * This class provides methods for file and directory operations such as listing,
 * uploading, downloading, and deleting objects in a BunnyCDN storage zone.
 *
 * @author ANZO
 * @since 20.09.2025
 */
public class BunnyCDNClient {
    private final String apiKey;
    private final HttpClient httpClient;
    private final String baseUri;
    private final ObjectMapper objectMapper;

    private static final String ACCESS_KEY_HEADER = "AccessKey";

    /**
     * Initializes a new instance of the BunnyCDNClient.
     *
     * @param storageZoneName The name of your storage zone.
     * @param apiKey          Your API key, which is the password for your storage zone.
     * @param region          The storage region (e.g., StorageRegion.FALKENSTEIN). If null, defaults to Falkenstein.
     */
    public BunnyCDNClient(String storageZoneName, String apiKey, StorageRegion region) {
        if (storageZoneName == null || storageZoneName.trim().isEmpty()) {
            throw new IllegalArgumentException("storageZoneName cannot be null or empty.");
        }
        if (apiKey == null || apiKey.trim().isEmpty()) {
            throw new IllegalArgumentException("apiKey cannot be null or empty.");
        }
        this.apiKey = apiKey;
        this.httpClient = HttpClient.newHttpClient();
        this.objectMapper = new ObjectMapper();

        if (region == null) {
            this.baseUri = "https://storage.bunnycdn.com/" + storageZoneName + "/";
        } else {
            this.baseUri = "https://" + region.getUri() + "/" + storageZoneName + "/";
        }
    }

    /**
     * Initializes a new instance of the BunnyCDNClient for Falkenstein region storage.
     *
     * @param storageZoneName The name of your storage zone.
     * @param apiKey          Your API key, which is the password for your storage zone.
     */
    public BunnyCDNClient(String storageZoneName, String apiKey) {
        this(storageZoneName, apiKey, null);
    }

    /**
     * Retrieves a list of storage objects from a specific directory. This method is not recursive.
     *
     * @param path The path to the directory (e.g., "my-folder/").
     * @return A {@code List<StorageObject>} containing the files and directories in the specified path.
     * @throws IOException          if an I/O error occurs when sending or receiving.
     * @throws InterruptedException if the operation is interrupted.
     */
    public List<StorageObject> listFiles(@NotNull String path) throws IOException, InterruptedException {
        if (!path.endsWith("/") && !path.isEmpty()) {
            path += "/";
        }

        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(baseUri + path))
                .header(ACCESS_KEY_HEADER, apiKey)
                .GET()
                .build();

        HttpResponse<String> response = httpClient.send(request, HttpResponse.BodyHandlers.ofString());
        if (response.statusCode() != 200) {
            throw new IOException("Failed to list files: " + response.statusCode() + " " + response.body());
        }
        return objectMapper.readValue(response.body(), new TypeReference<>() {});
    }

    /**
     * Recursively lists all files starting from a given path, without any filtering.
     *
     * @param path The starting directory path (e.g., "dumps/").
     * @return A flat list of all file {@code StorageObject}s found recursively.
     * @throws IOException          if an I/O error occurs during any API call.
     * @throws InterruptedException if the operation is interrupted.
     */
    public List<StorageObject> listFilesRecursive(String path) throws IOException, InterruptedException {
        return listFilesRecursive(path, null);
    }

    /**
     * Recursively lists all files starting from a given path that match the extension filter.
     *
     * @param path            The starting directory path (e.g., "dumps/").
     * @param extensionFilter The filter for file extensions (e.g., "*.txt", "*.json").
     *                        A {@code null} or {@code "*"} value means no filtering.
     *                        A {@code "*.*"} value means only files with an extension.
     * @return A flat list of all matching file {@code StorageObject}s found recursively.
     * @throws IOException          if an I/O error occurs during any API call.
     * @throws InterruptedException if the operation is interrupted.
     */
    public List<StorageObject> listFilesRecursive(String path, String extensionFilter) throws IOException, InterruptedException {
        List<StorageObject> allFiles = new ArrayList<>();
        listRecursively(path, allFiles, extensionFilter);
        return allFiles;
    }

    private void listRecursively(String currentPath, List<StorageObject> allFiles, String filter) throws IOException, InterruptedException {
        List<StorageObject> itemsInCurrentPath = listFiles(currentPath);

        for (StorageObject item : itemsInCurrentPath) {
            if (item.isDirectory) {
                String subDirectoryPath = item.path.replace("/" + item.storageZoneName, "") + item.objectName;
                listRecursively(subDirectoryPath, allFiles, filter);
            } else {
                if (matchesFilter(item.objectName, filter)) {
                    allFiles.add(item);
                }
            }
        }
    }

    private boolean matchesFilter(String fileName, String filter) {
        if (filter == null || filter.trim().isEmpty() || filter.equals("*")) {
            return true;
        }

        if (filter.equals("*.*")) {
            return fileName.contains(".");
        }

        if (filter.startsWith("*.")) {
            String extension = filter.substring(1).toLowerCase();
            return fileName.toLowerCase().endsWith(extension);
        }

        return fileName.equalsIgnoreCase(filter);
    }

    /**
     * Downloads a file from the storage zone to a local file path.
     *
     * @param storagePath  The full path to the file in the storage zone (e.g., "folder/file.txt").
     * @param downloadPath The local {@code Path} where the file will be saved.
     * @throws IOException          if an I/O error occurs or the server returns a non-200 status.
     * @throws InterruptedException if the operation is interrupted.
     */
    public void downloadFile(String storagePath, Path downloadPath) throws IOException, InterruptedException {
        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(baseUri + storagePath))
                .header(ACCESS_KEY_HEADER, apiKey)
                .GET()
                .build();

        HttpResponse<Path> response = httpClient.send(request, HttpResponse.BodyHandlers.ofFile(downloadPath));
        if (response.statusCode() != 200) {
            throw new IOException("Failed to download file: " + response.statusCode());
        }
    }

    /**
     * Downloads a file from the storage zone as an {@code InputStream}.
     * The caller is responsible for closing the stream.
     *
     * @param storagePath The path to the file in the storage zone.
     * @return An {@code InputStream} of the file content.
     * @throws IOException          if an I/O error occurs or the server returns a non-200 status.
     * @throws InterruptedException if the operation is interrupted.
     */
    public InputStream downloadFileAsStream(String storagePath) throws IOException, InterruptedException {
        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(baseUri + storagePath))
                .header(ACCESS_KEY_HEADER, apiKey)
                .GET()
                .build();

        HttpResponse<InputStream> response = httpClient.send(request, HttpResponse.BodyHandlers.ofInputStream());
        if (response.statusCode() != 200) {
            throw new IOException("Failed to download file: " + response.statusCode());
        }
        return response.body();
    }

    /**
     * Downloads a file from the storage zone and returns its content as a UTF-8 encoded String.
     *
     * @param storagePath The path to the file in the storage zone.
     * @return The content of the file as a {@code String}.
     * @throws IOException          if an I/O error occurs.
     * @throws InterruptedException if the operation is interrupted.
     */
    public String downloadFileAsText(String storagePath) throws IOException, InterruptedException {
        try (InputStream stream = downloadFileAsStream(storagePath)) {
            return new String(stream.readAllBytes(), StandardCharsets.UTF_8);
        }
    }

    /**
     * Downloads a file from the storage zone and returns its content as a byte array.
     *
     * @param storagePath The path to the file in the storage zone.
     * @return The content of the file as a byte array.
     * @throws IOException          if an I/O error occurs.
     * @throws InterruptedException if the operation is interrupted.
     */
    public byte[] downloadFileAsBytes(String storagePath) throws IOException, InterruptedException {
        try (InputStream stream = downloadFileAsStream(storagePath)) {
            return stream.readAllBytes();
        }
    }

    /**
     * Uploads a local file to the storage zone.
     *
     * @param localPath   The {@code Path} of the local file to upload.
     * @param storagePath The destination path in the storage zone, including the file name.
     * @throws IOException          if an I/O error occurs or the server returns a non-201 status.
     * @throws InterruptedException if the operation is interrupted.
     */
    public void uploadFile(Path localPath, String storagePath) throws IOException, InterruptedException {
        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(baseUri + storagePath))
                .header(ACCESS_KEY_HEADER, apiKey)
                .PUT(HttpRequest.BodyPublishers.ofFile(localPath))
                .build();

        HttpResponse<String> response = httpClient.send(request, HttpResponse.BodyHandlers.ofString());
        if (response.statusCode() != 201) {
            throw new IOException("Failed to upload file: " + response.statusCode() + " " + response.body());
        }
    }

    /**
     * Uploads data from an {@code InputStream} to the storage zone.
     *
     * @param storagePath The destination path in the storage zone, including the file name.
     * @param dataStream  The {@code InputStream} containing the data to upload.
     * @throws IOException          if an I/O error occurs or the server returns a non-201 status.
     * @throws InterruptedException if the operation is interrupted.
     */
    public void uploadFile(String storagePath, InputStream dataStream) throws IOException, InterruptedException {
        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(baseUri + storagePath))
                .header("AccessKey", apiKey)
                .PUT(HttpRequest.BodyPublishers.ofInputStream(() -> dataStream))
                .build();

        HttpResponse<String> response = httpClient.send(request, HttpResponse.BodyHandlers.ofString());
        if (response.statusCode() != 201) {
            throw new IOException("Failed to upload file: " + response.statusCode() + " " + response.body());
        }
    }

    /**
     * Deletes a file or an empty directory from the storage zone.
     *
     * @param path The path to the object to delete.
     * @throws IOException          if an I/O error occurs or the server returns a non-200 status.
     * @throws InterruptedException if the operation is interrupted.
     */
    public void deleteFile(String path) throws IOException, InterruptedException {
        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(baseUri + path))
                .header(ACCESS_KEY_HEADER, apiKey)
                .DELETE()
                .build();

        HttpResponse<String> response = httpClient.send(request, HttpResponse.BodyHandlers.ofString());
        if (response.statusCode() != 200) {
            throw new IOException("Failed to delete file: " + response.statusCode() + " " + response.body());
        }
    }

    /**
     * Represents a file or directory object in a BunnyCDN Storage Zone.
     * This class maps to the JSON response from the BunnyCDN API list operation.
     */
    @ToString
    public static class StorageObject {
        /**
         * The unique GUID of the file.
         */
        @JsonProperty("Guid")
        public String guid;

        /**
         * The name of the storage zone where the object is located.
         */
        @JsonProperty("StorageZoneName")
        public String storageZoneName;

        /**
         * The full path to the directory containing the object.
         */
        @JsonProperty("Path")
        public String path;

        /**
         * The name of the object (file or directory).
         */
        @JsonProperty("ObjectName")
        public String objectName;

        /**
         * The total length of the object in bytes.
         */
        @JsonProperty("Length")
        public long length;

        /**
         * The timestamp of when the object was last modified.
         */
        @JsonProperty("LastChanged")
        public String lastChanged;

        /**
         * The ID of the server where the object is stored.
         */
        @JsonProperty("ServerId")
        public int serverId;

        /**
         * The internal array number of the storage server.
         */
        @JsonProperty("ArrayNumber")
        public int arrayNumber;

        /**
         * A flag indicating if the object is a directory.
         */
        @JsonProperty("IsDirectory")
        public boolean isDirectory;

        /**
         * The ID of the user that uploaded the file.
         */
        @JsonProperty("UserId")
        public String userId;

        /**
         * The content type of the object.
         */
        @JsonProperty("ContentType")
        public String contentType;

        /**
         * The timestamp of when the object was created.
         */
        @JsonProperty("DateCreated")
        public String dateCreated;

        /**
         * The ID of the storage zone where the object is located.
         */
        @JsonProperty("StorageZoneId")
        public long storageZoneId;

        /**
         * The SHA256 checksum of the file.
         */
        @JsonProperty("Checksum")
        public String checksum;

        /**
         * A comma-separated list of replicated zone codes.
         */
        @JsonProperty("ReplicatedZones")
        public String replicatedZones;

        /**
         * Constructs the full relative path of the object within the storage zone.
         * It removes the storage zone prefix from the 'Path' and appends the 'ObjectName'.
         * For example, if Path is '/my-zone/folder/' and ObjectName is 'file.txt',
         * this will return 'folder/file.txt'.
         *
         * @return The full relative path of the object, or an empty string if essential path fields are null.
         */
        public String getFullPath() {
            if (path == null || storageZoneName == null || objectName == null) {
                return "";
            }

            String prefix = "/" + storageZoneName + "/";
            String relativePath = path;

            if (path.startsWith(prefix)) {
                relativePath = path.substring(prefix.length());
            }

            return relativePath + objectName;
        }
    }

    /**
     * Represents the available storage regions for BunnyCDN.
     * The region determines the base URI for API requests.
     */
    public @Getter enum StorageRegion {
        FALKENSTEIN("storage.bunnycdn.com"),
        NEW_YORK("ny.storage.bunnycdn.com"),
        LOS_ANGELES("la.storage.bunnycdn.com"),
        SINGAPORE("sg.storage.bunnycdn.com"),
        SYDNEY("syd.storage.bunnycdn.com");

        private final String uri;

        StorageRegion(String uri) {
            this.uri = uri;
        }
    }
}