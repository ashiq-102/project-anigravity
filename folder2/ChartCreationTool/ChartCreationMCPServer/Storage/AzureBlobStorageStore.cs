using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using System.Text;

namespace ChartCreationMCPServer.Storage
{
    /// <summary>
    /// Azure Blob Storage implementation of IStorageStore.
    ///
    /// Two containers are used:
    ///   - inputContainer  (e.g. "input-files")  : user-uploaded data files (CSV, Excel, etc.)
    ///   - outputContainer (e.g. "output-charts") : generated chart PNG images
    ///
    /// Key behaviours:
    ///   - Reference names are auto-derived from the original filename (e.g. sales.csv -> "sales").
    ///   - If a file with the same name already exists, it is overwritten.
    ///   - All blobs are namespaced under a team prefix: {team}/filename.
    ///   - Input blobs are cached locally so Python can read them as local files.
    ///     The cache is checked against the blob's LastModified timestamp — if the blob
    ///     was updated, the local cache is refreshed automatically.
    ///   - Local temp cache files older than 24 hours are cleaned up by a background timer.
    ///     Azure Blob Storage is NEVER touched by cleanup — files survive indefinitely in blob.
    /// </summary>
    internal sealed class AzureBlobStorageStore : IStorageStore
    {
        private readonly BlobContainerClient _inputContainer;
        private readonly BlobContainerClient _outputContainer;

        // Local temp directory where input blobs are cached so Python can read them as local files.
        // This folder is cleaned every 24 hours — blobs in Azure are untouched.
        private readonly string _tempDir;

        /// <summary>
        /// Creates the Azure Blob store and ensures both containers exist.
        /// </summary>
        /// <param name="connectionString">Azure Storage account connection string.</param>
        /// <param name="inputContainerName">Container name for uploaded input files.</param>
        /// <param name="outputContainerName">Container name for generated chart images.</param>
        public AzureBlobStorageStore(
            string connectionString,
            string inputContainerName = "input-files",
            string outputContainerName = "output-charts")
        {
            var serviceClient = new BlobServiceClient(connectionString);

            _inputContainer = serviceClient.GetBlobContainerClient(inputContainerName);
            _outputContainer = serviceClient.GetBlobContainerClient(outputContainerName);

            // Ensure containers exist (private access — SAS URLs used for sharing)
            _inputContainer.CreateIfNotExists(PublicAccessType.None);
            _outputContainer.CreateIfNotExists(PublicAccessType.None);

            // Temp folder on the server where input blobs are cached for Python.
            // This is a local server folder only — NOT in Azure Blob Storage.
            _tempDir = Path.Combine(Path.GetTempPath(), "chart_input_cache");
            Directory.CreateDirectory(_tempDir);
        }

        // ── Upload input file ───────────────────────

        /// <summary>
        /// Uploads a file into the Azure Blob input container.
        /// The reference name is automatically derived from the original filename (without extension),
        /// so the user always recognises it — e.g. sales.csv is stored as "sales".
        /// </summary>
        /// <param name="sourcePath">The local path of the file to upload.</param>
        /// <returns>The reference name derived from the uploaded filename.</returns>
        public async Task<string> UploadAsync(string sourcePath, string team)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("sourcePath is required.");
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Source file not found.", sourcePath);

            var teamPrefix = PathSafety.SanitizeTeam(team);
            var ext = Path.GetExtension(sourcePath);
            var baseName = PathSafety.SanitizeReference(Path.GetFileNameWithoutExtension(sourcePath));
            var blobName = $"{teamPrefix}/{baseName}{ext}";
            var blobClient = _inputContainer.GetBlobClient(blobName);

            await using var stream = File.OpenRead(sourcePath);
            await blobClient.UploadAsync(stream, overwrite: true);

            return baseName;
        }

        // ── List blobs ────────────────────────────────────────────────────────

        /// <summary>
        /// Looks through the Azure Blob input container and returns the names of the blobs we have.
        /// You can pass a filter if you only want blobs with a specific word in their name.
        /// Only files under the given team's prefix are returned.
        /// </summary>
        /// <param name="nameFilter">Optional. A word to search for in the blob names.</param>
        /// <returns>A list of blob names (not full URLs, just the names).</returns>
        public IEnumerable<string> List(string team, string? nameFilter = null)
        {
            var scan = $"{PathSafety.SanitizeTeam(team)}/";

            var names = _inputContainer
                .GetBlobs(BlobTraits.None, BlobStates.All, prefix: scan, CancellationToken.None)
                .Select(b => b.Name[scan.Length..]);

            if (!string.IsNullOrWhiteSpace(nameFilter))
                names = names.Where(n => n.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));

            return names.ToList();
        }

        // ── Delete blobs ──────────────────────────────────────────────────────

        /// <summary>
        /// Cleans house by deleting blobs from the Azure Blob input container.
        /// If you don't pass a filter, watch out — it deletes everything!
        /// Note: this deletes from Azure Blob Storage permanently.
        /// </summary>
        /// <param name="nameFilter">Optional. Only deletes blobs that have this word in their name.</param>
        /// <returns>The total number of blobs deleted.</returns>
        public int Delete(string team, string? nameFilter = null)
        {
            var teamPrefix = PathSafety.SanitizeTeam(team);
            var targets = List(team, nameFilter).ToList();

            foreach (var refName in targets)
                _inputContainer.GetBlobClient($"{teamPrefix}/{refName}").DeleteIfExists();

            return targets.Count;
        }

        // ── Preview blob text ─────────────────────────────────────────────────

        /// <summary>
        /// Downloads a blob and reads its text. We cap this at a maximum number of characters
        /// so the AI doesn't choke on a 50GB file.
        /// </summary>
        /// <param name="referenceName">The reference name of the blob to read (e.g. "sales" or "sales_v2").</param>
        /// <param name="maxChars">Our hard limit on how many characters to read before truncating. Defaults to 4000.</param>
        /// <returns>The text payload from the blob.</returns>
        public async Task<string> ReadTextAsync(string referenceName, string team, int maxChars = 4000)
        {
            var blobName = ResolveBlobName(referenceName, team);
            if (blobName == null)
                return $"ERROR: file '{referenceName}' not found in input store.";

            var blobClient = _inputContainer.GetBlobClient(blobName);

            await using var stream = await blobClient.OpenReadAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var buffer = new char[maxChars];
            var read = await reader.ReadAsync(buffer, 0, maxChars);
            var text = new string(buffer, 0, read);

            // CA2024 fix: Do not use reader.EndOfStream in an async method — it is a synchronous
            // property that can block the thread. Instead, check whether we read exactly maxChars
            // characters. If we did, there may be more content in the stream, so append the truncation marker.
            if (read == maxChars)
                text += "\n...[truncated]...";

            return text;
        }

        // ── Resolve to local temp path for Python ─────────────────────────────

        /// <summary>
        /// Figures out the exact local temp path for a blob so Python can read it.
        ///
        /// Stale cache fix:
        ///   Previously, once a blob was downloaded to the local temp folder it was never refreshed.
        ///   This meant if the user uploaded a new version under the same reference name, Python would
        ///   still read the old cached file — a silent data bug.
        ///
        ///   Now we compare the blob's LastModified timestamp against the local file's write time.
        ///   If the blob is newer than the cached file, we delete the stale cache and re-download.
        ///   This ensures Python always reads the correct and most recent version of the file.
        /// </summary>
        /// <param name="referenceName">The short reference name of the blob (e.g. "sales" or "sales_v2").</param>
        /// <returns>The absolute local temp path to that file.</returns>
        public string GetAbsolutePath(string referenceName, string team)
        {
            var blobName = ResolveBlobName(referenceName, team)
                ?? throw new FileNotFoundException($"No blob found for reference '{referenceName}'.");

            var localPath = Path.Combine(_tempDir, blobName.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

            var blobClient = _inputContainer.GetBlobClient(blobName);
            var needsDownload = true;

            if (File.Exists(localPath))
            {
                var blobLastModified = blobClient.GetProperties().Value.LastModified.UtcDateTime;
                var localLastWrite = File.GetLastWriteTimeUtc(localPath);

                if (blobLastModified <= localLastWrite) needsDownload = false;
                else
                {
                    Console.WriteLine($"[Cache] Stale cache for '{blobName}'. Re-downloading.");
                    File.Delete(localPath);
                }
            }

            if (needsDownload)
            {
                Console.WriteLine($"[Cache] Downloading '{blobName}' to local temp cache.");
                using var fs = File.Create(localPath);
                blobClient.DownloadTo(fs);
            }

            return localPath;
        }

        // ── Upload generated chart and return SAS URL ─────────────────────────

        /// <summary>
        /// Uploads a generated chart PNG to the Azure Blob output container and returns
        /// a time-limited SAS URL. The agent shows this URL to the user as a view/download link.
        /// </summary>
        /// <param name="localImagePath">Full local path to the generated PNG file.</param>
        /// <param name="chartId">Chart identifier used as the blob name (e.g. "sales_bar").</param>
        /// <returns>A SAS URL valid for 1 year.</returns>
        public async Task<string> UploadChartAsync(string localImagePath, string chartId, string team)
        {
            if (!File.Exists(localImagePath))
                throw new FileNotFoundException("Generated chart image not found.", localImagePath);

            var teamPrefix = PathSafety.SanitizeTeam(team);
            var blobName = $"{teamPrefix}/{chartId}.png";
            var blobClient = _outputContainer.GetBlobClient(blobName);

            await using var stream = File.OpenRead(localImagePath);
            await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = "image/png" });

            var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddYears(1));
            return sasUri.ToString();
        }
        /// <summary>
        /// Lists every chart in the team's output area, minting a fresh 1-year SAS URL for each
        /// (SAS URLs can't be stored because they expire).
        /// </summary>
        /// <param name="team">The team whose charts to list.</param>
        /// <returns>The chart name (prefix stripped) and a viewable SAS URL for each chart.</returns>
        public IEnumerable<(string Name, string Url)> ListCharts(string team)
        {
            var scan = $"{PathSafety.SanitizeTeam(team)}/";
            var results = new List<(string, string)>();

            foreach (var blob in _outputContainer.GetBlobs(
                         BlobTraits.None, BlobStates.None, prefix: scan, CancellationToken.None))
            {
                var name = blob.Name[scan.Length..];
                var client = _outputContainer.GetBlobClient(blob.Name);
                var url = client.GenerateSasUri(
                                 BlobSasPermissions.Read,
                                 DateTimeOffset.UtcNow.AddYears(1)).ToString();
                results.Add((name, url));
            }

            return results;
        }

        // ── Local temp cache cleanup ──────────────────────────────────────────

        /// <summary>
        /// Deletes local temp cache files that are older than the specified number of hours.
        ///
        /// IMPORTANT: This ONLY cleans the local server temp folder (/tmp/chart_input_cache/).
        /// Azure Blob Storage is NEVER touched by this operation.
        /// All blobs in the input-files container survive forever regardless of this cleanup.
        ///
        /// If a user comes back after months and requests a file that was cleaned from cache,
        /// GetAbsolutePath() will simply re-download it from Azure Blob automatically —
        /// so nothing is lost from the user's perspective.
        /// </summary>
        /// <param name="olderThanHours">Files older than this many hours are deleted from local cache. Default is 24.</param>
        /// <returns>The number of local temp files deleted.</returns>
        public Task<int> CleanupTempCacheAsync(int olderThanHours = 24)
        {
            var cutoff = DateTime.UtcNow.AddHours(-olderThanHours);
            var deleted = 0;

            // Only clean files inside our specific cache folder — never touch anything else on disk
            foreach (var file in Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories))
            {
                var lastWrite = File.GetLastWriteTimeUtc(file);

                if (lastWrite < cutoff)
                {
                    try
                    {
                        File.Delete(file);
                        deleted++;
                        Console.WriteLine($"[Cache Cleanup] Deleted stale temp file: {Path.GetFileName(file)} " +
                                          $"(last used: {lastWrite:yyyy-MM-dd HH:mm} UTC)");
                    }
                    catch (Exception ex)
                    {
                        // Log but don't throw — one failed deletion shouldn't stop the rest
                        Console.WriteLine($"[Cache Cleanup] Failed to delete {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
            }

            Console.WriteLine($"[Cache Cleanup] Complete. Deleted {deleted} file(s) older than {olderThanHours}h from local temp cache.");
            return Task.FromResult(deleted);
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Finds the actual blob name in the input container for a given reference name.
        /// Blobs are stored as "{team}/{baseName}{ext}". We search by the team prefix plus
        /// the sanitized reference name, e.g. "sohel/sales" finds "sohel/sales.csv".
        /// </summary>
        /// <param name="referenceName"></param>
        /// <param name="team"></param>
        /// <returns>The full blob name including extension, or null if not found.</returns>
        private string? ResolveBlobName(string referenceName, string team)
        {
            var scan = $"{PathSafety.SanitizeTeam(team)}/{PathSafety.SanitizeReference(referenceName)}";

            return _inputContainer
                .GetBlobs(BlobTraits.None, BlobStates.All, prefix: $"{PathSafety.SanitizeTeam(team)}/", CancellationToken.None)
                .Select(b => b.Name)
                .FirstOrDefault(b => string.Equals(
         Path.GetFileNameWithoutExtension(b),
         PathSafety.SanitizeReference(referenceName),
         StringComparison.OrdinalIgnoreCase));
        }
    }
}
