using System;
using System.Collections.Generic;
using System.Text;

namespace ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation.Storage
{
    internal class StorageTool
    {
        private readonly IStorageStore _store;

        /// <summary>
        /// Initializes a new instance of the StorageTool.
        /// </summary>
        /// <param name="store">The underlying content delivery storage interface.</param>
        public StorageTool(IStorageStore store)
        {
            _store = store;
        }

        /// <summary>
        /// Uploads a file asynchronously into the storage system.
        /// </summary>
        /// <param name="sourcePath">The source file path.</param>
        /// <param name="referenceName">The target reference name.</param>
        /// <returns>A formatted status string indicating success.</returns>
        public async Task<string> UploadAsync(string sourcePath, string referenceName)
        {
            await _store.UploadAsync(sourcePath, referenceName);
            return $"OK: Uploaded '{sourcePath}' as '{referenceName}'.";
        }

        /// <summary>
        /// Lists files in the storage system matching the optional filter.
        /// </summary>
        /// <param name="filter">Optional search string.</param>
        /// <returns>A formatted string with the list of files.</returns>
        public string List(string? filter = null)
        {
            var files = _store.List(filter).ToArray();
            return files.Length == 0 ? "OK: No files." : "OK: Files:\n" + string.Join("\n", files);
        }

        /// <summary>
        /// Returns the count of files in the storage system matching the optional filter.
        /// </summary>
        /// <param name="filter">Optional search string.</param>
        /// <returns>A formatted string indicating the count of matches.</returns>
        public string Count(string? filter = null)
        {
            var n = _store.List(filter).Count();
            return $"OK: Count = {n}";
        }

        /// <summary>
        /// Deletes the files in the storage system matching the optional filter.
        /// </summary>
        /// <param name="filter">Optional search string.</param>
        /// <returns>A formatted string indicating the deletion output.</returns>
        public string Delete(string? filter = null)
        {
            var n = _store.Delete(filter);
            return $"OK: Deleted {n} file(s).";
        }

        /// <summary>
        /// Asynchronously fetches text from the storage store up to a threshold.
        /// </summary>
        /// <param name="referenceName">The target reference name.</param>
        /// <param name="maxChars">The maximum number of characters allowed.</param>
        /// <returns>The data payload retrieved.</returns>
        public Task<string> ReadAsync(string referenceName, int maxChars = 4000)
            => _store.ReadTextAsync(referenceName, maxChars);

        /// <summary>
        /// Fetches the internal absolute path for the targeted file.
        /// </summary>
        /// <param name="referenceName">The file reference name.</param>
        /// <returns>The absolute storage path string.</returns>
        public string ResolvePath(string referenceName)
            => _store.GetAbsolutePath(referenceName);
    }
}
