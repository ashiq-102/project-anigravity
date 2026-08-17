using System.Text.Json;

namespace ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation
{
    /// <summary>
    /// Holds the details for a single chart that the AI has drawn for us.
    /// Think of it as a receipt that stores what the chart is called, when it was made, and if it worked.
    /// </summary>
    internal sealed class ChartEntry
    {
        public string ChartId { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string ScriptPath { get; set; } = "";
        public string ImagePath { get; set; } = "";
        public List<string> InputFiles { get; set; } = new();
        public double ExecutionTimeMs { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Keeps track of every chart we created today. 
    /// It grabs this list from a file so we don't forget what the AI has done so far.
    /// </summary>
    internal sealed class ChartManifest
    {
        private readonly string _manifestPath;
        private readonly List<ChartEntry> _entries;
        private readonly JsonSerializerOptions _jsonOptions;

        public ChartManifest(string outputDirectory)
        {
            _manifestPath = Path.Combine(outputDirectory, "manifest.json");
            _entries = new List<ChartEntry>();
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

            LoadExistingManifest();
        }

        /// <summary>
        /// Looks to see if we already have a saved list of charts on the hard drive. 
        /// If we do, it reads them into memory.
        /// </summary>
        private void LoadExistingManifest()
        {
            if (!File.Exists(_manifestPath))
                return;

            try
            {
                var json = File.ReadAllText(_manifestPath);
                var entries = JsonSerializer.Deserialize<List<ChartEntry>>(json, _jsonOptions);
                if (entries != null)
                {
                    _entries.AddRange(entries);
                }
            }
            catch
            {
                // If manifest is corrupted, start fresh
            }
        }

        /// <summary>
        /// Adds a brand new chart to our list and immediately saves the list so we don't lose it.
        /// </summary>
        /// <param name="entry">The chart details to save.</param>
        /// <returns>A task that finishes when saving is complete.</returns>
        public async Task AddEntryAsync(ChartEntry entry)
        {
            _entries.Add(entry);
            await SaveAsync();
        }

        /// <summary>
        /// Gives you the full list of every chart we have on record.
        /// </summary>
        /// <returns>A read-only list of all charts.</returns>
        public IReadOnlyList<ChartEntry> GetAllEntries() => _entries.AsReadOnly();

        /// <summary>
        /// Gets chart entries from the current session (today).
        /// </summary>
        /// <returns>A list of charts created today.</returns>
        public IReadOnlyList<ChartEntry> GetSessionEntries()
        {
            var today = DateTime.Today;
            return _entries
                .Where(e => e.Timestamp.Date == today)
                .OrderBy(e => e.Timestamp)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Gets successful chart entries only.
        /// </summary>
        /// <returns>A list of charts that didn't crash.</returns>
        public IReadOnlyList<ChartEntry> GetSuccessfulEntries()
        {
            return _entries
                .Where(e => e.Success)
                .OrderByDescending(e => e.Timestamp)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Gets chart entries that used a specific input file.
        /// </summary>
        /// <param name="fileName">The name of the file to search for.</param>
        /// <returns>A list of matching charts.</returns>
        public IReadOnlyList<ChartEntry> GetEntriesForFile(string fileName)
        {
            return _entries
                .Where(e => e.InputFiles.Any(f =>
                    f.Contains(fileName, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(e => e.Timestamp)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Takes our list of charts and turns it into nice, clean text that you can read on the screen.
        /// </summary>
        /// <param name="entries">The list of charts to format. If you don't give one, it just uses today's charts.</param>
        /// <returns>A nicely formatted text block ready to print.</returns>
        public string FormatEntries(IReadOnlyList<ChartEntry>? entries = null)
        {
            entries ??= GetSessionEntries();

            if (entries.Count == 0)
                return "No charts generated yet in this session.";

            var lines = new List<string>
            {
                $"Generated Charts ({entries.Count} total):",
                ""
            };

            foreach (var entry in entries)
            {
                var status = entry.Success ? "✓" : "✗";
                var time = entry.ExecutionTimeMs > 0
                    ? $" ({entry.ExecutionTimeMs:F0}ms)"
                    : "";

                lines.Add($"{status} [{entry.Timestamp:HH:mm:ss}] {entry.ChartId}{time}");
                lines.Add($"   Image: {entry.ImagePath}");
                lines.Add($"   Script: {entry.ScriptPath}");

                if (entry.InputFiles.Any())
                {
                    lines.Add($"   Data: {string.Join(", ", entry.InputFiles.Select(Path.GetFileName))}");
                }

                if (!entry.Success && !string.IsNullOrWhiteSpace(entry.ErrorMessage))
                {
                    var shortError = entry.ErrorMessage.Length > 100
                        ? entry.ErrorMessage.Substring(0, 100) + "..."
                        : entry.ErrorMessage;
                    lines.Add($"   Error: {shortError}");
                }

                lines.Add("");
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Saves the manifest to disk.
        /// </summary>
        private async Task SaveAsync()
        {
            var json = JsonSerializer.Serialize(_entries, _jsonOptions);
            await File.WriteAllTextAsync(_manifestPath, json);
        }

        /// <summary>
        /// Clears all entries from the manifest.
        /// </summary>
        public async Task ClearAsync()
        {
            _entries.Clear();
            await SaveAsync();
        }

        /// <summary>
        /// Gets statistics about the manifest.
        /// </summary>
        /// <returns>A string summarizing hits and misses.</returns>
        public string GetStats()
        {
            var total = _entries.Count;
            var successful = _entries.Count(e => e.Success);
            var failed = total - successful;
            var avgTime = _entries
                .Where(e => e.Success && e.ExecutionTimeMs > 0)
                .Select(e => e.ExecutionTimeMs)
                .DefaultIfEmpty(0)
                .Average();

            return $"Total: {total}, Success: {successful}, Failed: {failed}, Avg Time: {avgTime:F0}ms";
        }
    }
}
