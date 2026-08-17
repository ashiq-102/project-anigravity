using System.Text.Json;

namespace ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation
{
    /// <summary>
    /// A simple list of the different types of problems the AI might run into.
    /// </summary>
    internal enum ErrorCategory
    {
        Unknown,
        ColumnNotFound,
        TypeError,
        SyntaxError,
        RuntimeError,
        ValidationError,
        ImportError
    }

    /// <summary>
    /// A notebook where we write down every mistake the AI makes. 
    /// We show this notebook to the AI later so it learns not to make the exact same mistake twice.
    /// </summary>
    internal sealed class ErrorMappingStore
    {
        private readonly string _filePath;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Prepares the error tracking notebook by ensuring the directory exists and setting up the file path where mistakes are saved.
        /// </summary>
        /// <param name="filePath">The path to the JSON file where errors will be logged.</param>
        public ErrorMappingStore(string filePath)
        {
            _filePath = filePath;

            // Ensure directory exists
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
        }

        /// <summary>
        /// Takes a fresh mistake from the AI, figures out what type of mistake it was, and writes it down in our notebook.
        /// </summary>
        /// <param name="error">The exact error text.</param>
        /// <param name="codeSnippet">The broken Python code.</param>
        /// <param name="fix">A note on how we fixed it.</param>
        /// <returns>A task tracking our progress.</returns>
        public async Task RecordErrorAsync(string error, string codeSnippet, string fix = "")
        {
            var entries = await LoadEntriesAsync();

            entries.Add(new ErrorEntry
            {
                Error = error,
                CodeSnippet = codeSnippet.Length > 500 ? codeSnippet[..500] + "..." : codeSnippet,
                Fix = fix,
                Timestamp = DateTime.UtcNow,
                Category = CategorizeError(error)
            });

            await SaveEntriesAsync(entries);
        }

        /// <summary>
        /// Grabs the latest mistakes we recorded, starting with the newest ones.
        /// </summary>
        /// <param name="maxCount">How many records to grab.</param>
        /// <returns>A list of the latest error items.</returns>
        public async Task<List<ErrorEntry>> GetRecentErrorsAsync(int maxCount = 10)
        {
            var entries = await LoadEntriesAsync();
            return entries
                .OrderByDescending(e => e.Timestamp)
                .Take(maxCount)
                .ToList();
        }

        /// <summary>
        /// Takes our list of recent mistakes and turns them into a nicely organized text block. 
        /// We feed this text directly to the AI so it remembers them.
        /// </summary>
        /// <param name="maxCount">Number of records to compile.</param>
        /// <returns>A formatted block of text summarizing the errors.</returns>
        public async Task<string> GetErrorContextForPromptAsync(int maxCount = 5)
        {
            var entries = await GetRecentErrorsAsync(maxCount);
            if (entries.Count == 0) return "";

            var grouped = entries.GroupBy(e => e.Category);
            var lines = new List<string> { "Common past errors to avoid:" };

            foreach (var group in grouped)
            {
                lines.Add($"\n{group.Key}:");
                foreach (var entry in group)
                {
                    lines.Add($"  - {entry.Error}");
                    if (!string.IsNullOrWhiteSpace(entry.Fix))
                        lines.Add($"    Fix: {entry.Fix}");
                }
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Looks at the raw error text from Python and guesses what kind of problem it actually is.
        /// </summary>
        /// <param name="error">The raw error string to categorize.</param>
        /// <returns>Our best guess at the error category.</returns>
        private static ErrorCategory CategorizeError(string error)
        {
            var lower = error.ToLowerInvariant();

            if (lower.Contains("keyerror") || lower.Contains("column") && lower.Contains("not found"))
                return ErrorCategory.ColumnNotFound;

            if (lower.Contains("typeerror") || lower.Contains("cannot convert"))
                return ErrorCategory.TypeError;

            if (lower.Contains("syntaxerror") || lower.Contains("invalid syntax"))
                return ErrorCategory.SyntaxError;

            if (lower.Contains("importerror") || lower.Contains("no module named"))
                return ErrorCategory.ImportError;

            if (lower.Contains("validation") || lower.Contains("forbidden"))
                return ErrorCategory.ValidationError;

            if (lower.Contains("error"))
                return ErrorCategory.RuntimeError;

            return ErrorCategory.Unknown;
        }

        // ── Internal helpers ───────────────────────────────────────────────

        /// <summary>
        /// Reads the history of errors from our notebook file so we can analyze them.
        /// </summary>
        /// <returns>A list of past errors, or an empty list if the file is new or broken.</returns>
        internal async Task<List<ErrorEntry>> LoadEntriesAsync()
        {
            if (!File.Exists(_filePath))
                return new List<ErrorEntry>();

            try
            {
                var json = await File.ReadAllTextAsync(_filePath);
                return JsonSerializer.Deserialize<List<ErrorEntry>>(json, _jsonOptions)
                       ?? new List<ErrorEntry>();
            }
            catch
            {
                return new List<ErrorEntry>();
            }
        }

        /// <summary>
        /// Writes the updated list of errors safely back into our notebook file.
        /// </summary>
        /// <param name="entries">The list of error entries to save.</param>
        private async Task SaveEntriesAsync(List<ErrorEntry> entries)
        {
            var json = JsonSerializer.Serialize(entries, _jsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
        }
    }
}
