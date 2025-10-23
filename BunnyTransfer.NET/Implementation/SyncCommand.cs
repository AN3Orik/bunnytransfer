namespace BunnyTransfer.NET.Implementation
{
    /// <summary>
    /// Main command handler for sync operation
    /// </summary>
    public class SyncCommand
    {
        public async Task<int> InvokeAsync(string[] args)
        {
            try
            {
                var options = ParseArguments(args);
                
                if (string.IsNullOrWhiteSpace(options.LocalPath))
                {
                    await Console.Error.WriteLineAsync("Error: --local-path is required.");
                    ShowHelp();
                    return 1;
                }
                
                if (string.IsNullOrWhiteSpace(options.StorageZone))
                {
                    await Console.Error.WriteLineAsync("Error: --storage-zone is required.");
                    ShowHelp();
                    return 1;
                }
                
                if (string.IsNullOrWhiteSpace(options.AccessKey))
                {
                    await Console.Error.WriteLineAsync("Error: --access-key is required (or set BUNNY_ACCESS_KEY environment variable).");
                    ShowHelp();
                    return 1;
                }
                
                var direction = options.Direction.ToLower();
                if (direction != "upload" && direction != "download")
                {
                    await Console.Error.WriteLineAsync("Error: --direction must be 'upload' or 'download'.");
                    ShowHelp();
                    return 1;
                }

                var syncEngine = new SyncEngine(options);
                await syncEngine.ExecuteAsync();

                return 0;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                return 1;
            }
        }

        private SyncOptions ParseArguments(string[] args)
        {
            var options = new SyncOptions();
            
            // Skip "sync" command if it's the first argument
            int startIndex = 0;
            if (args.Length > 0 && args[0].Equals("sync", StringComparison.OrdinalIgnoreCase))
            {
                startIndex = 1;
            }

            for (int i = startIndex; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg.StartsWith("--"))
                {
                    var parts = arg[2..].Split('=', 2);
                    var key = parts[0];
                    var value = parts.Length > 1 ? parts[1] : (i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[++i] : null);

                    switch (key.ToLower())
                    {
                        case "local-path":
                            options.LocalPath = value ?? string.Empty;
                            break;
                        case "storage-zone":
                            options.StorageZone = value ?? string.Empty;
                            break;
                        case "access-key":
                            options.AccessKey = value ?? string.Empty;
                            break;
                        case "direction":
                            options.Direction = value ?? "upload";
                            break;
                        case "region":
                            options.Region = value ?? "de";
                            break;
                        case "profile":
                            options.Profile = value ?? string.Empty;
                            break;
                        case "verbose":
                            options.Verbose = true;
                            if (parts.Length == 1 && i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                            {
                                // Don't consume next arg
                            }
                            break;
                        case "dry-run":
                            options.DryRun = true;
                            break;
                        case "upload-last":
                            if (value != null)
                            {
                                options.UploadLastPatterns.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                            }
                            break;
                        case "parallel":
                            if (value != null && int.TryParse(value, out var parallel))
                            {
                                options.ParallelUploads = Math.Max(1, Math.Min(parallel, 64));
                            }
                            break;
                        case "remote-path":
                            options.RemotePath = value ?? string.Empty;
                            break;
                        case "help":
                        case "h":
                            ShowHelp();
                            Environment.Exit(0);
                            break;
                    }
                }
                else if (arg.StartsWith("-") && arg.Length == 2)
                {
                    var flag = arg[1];
                    var value = i + 1 < args.Length ? args[i + 1] : null;

                    switch (flag)
                    {
                        case 'k':
                            options.AccessKey = value ?? string.Empty;
                            i++;
                            break;
                        case 'r':
                            options.Region = value ?? "de";
                            i++;
                            break;
                        case 'p':
                            options.Profile = value ?? string.Empty;
                            i++;
                            break;
                        case 'v':
                            options.Verbose = true;
                            break;
                        case 'j':
                            if (value != null && int.TryParse(value, out var parallel))
                            {
                                options.ParallelUploads = Math.Max(1, Math.Min(parallel, 64));
                                i++;
                            }
                            break;
                        case 'h':
                            ShowHelp();
                            Environment.Exit(0);
                            break;
                    }
                }
            }
            
            if (string.IsNullOrWhiteSpace(options.AccessKey))
            {
                options.AccessKey = Environment.GetEnvironmentVariable("BUNNY_ACCESS_KEY") ?? string.Empty;
            }

            return options;
        }

        private static void ShowHelp()
        {
            Console.WriteLine("""

                              BunnyTransfer.NET - Sync files to/from BunnyCDN Storage 
                              Author: ANZO © 2025

                              Usage:
                                BunnyTransfer.NET sync [options]

                              Required Options:
                                --local-path <path>       Local directory path
                                --storage-zone <name>     BunnyCDN storage zone name
                                --access-key <key>        BunnyCDN API access key (or set BUNNY_ACCESS_KEY env var)

                              Optional:
                                --direction <dir>         Sync direction: 'upload' or 'download' [default: upload]
                                --remote-path <path>      Remote subdirectory within storage zone [default: root]
                                --region <region>         Storage zone region (de, ny, sg) [default: de]
                                -j, --parallel <num>      Number of parallel operations (1-64) [default: 16]
                                --upload-last <patterns>  Comma-separated file patterns to upload last (e.g., hash.txt,manifest.json)
                                -v, --verbose             Enable verbose output
                                --dry-run                 Show what would be synced without actually syncing
                                -h, --help                Show this help message

                              Examples:
                                # Upload local files to storage zone root
                                sync --local-path ./dist --storage-zone my-zone --access-key abc123
                                
                                # Upload to subdirectory
                                sync --local-path ./dist --storage-zone my-zone --access-key abc123 --remote-path v1.2.3
                                
                                # Download from storage zone to local
                                sync --local-path ./backup --storage-zone my-zone --access-key abc123 --direction download
                                
                                # Upload with custom file order
                                sync --local-path ./dist --storage-zone my-zone --access-key abc123 --upload-last hash.txt,manifest.json
                                
                              Environment Variables:
                                BUNNY_ACCESS_KEY          BunnyCDN API access key

                              Note: 
                                - Sync will update files in destination and delete files that don't exist in source
                                - Files are compared by SHA256 checksum to skip unchanged files
                                - HTML/XML files are uploaded second-to-last, custom patterns are uploaded last

                              """);
        }
    }
}
