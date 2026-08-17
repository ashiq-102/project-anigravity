using System;
using System.Collections.Generic;
using System.Text;

namespace ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation.Storage
{
    /// <summary>
    /// The physical file system implementation of our storage layer. 
    /// Instead of saving to a cloud bucket, this saves everything into a local sandbox folder right next to the executable.
    /// </summary>
    internal sealed class LocalFileStore : IStorageStore
    {
        /// <summary>
        /// The absolute folder path where all our sandboxed files live.
        /// </summary>
        public string RootPath { get; }

        /// <summary>
        /// Creates a new store and makes sure the physical folder actually exists on disk.
        /// </summary>
        /// <param name="rootPath">Where you want the files to sit. Typically this is the 'input_store' folder.</param>
        public LocalFileStore(string rootPath)
        {
            RootPath = rootPath;
            Directory.CreateDirectory(RootPath);
        }

        /// <summary>
        /// Grabs a file from somewhere on the user's computer and securely copies it into our isolated sandbox.
        /// We keep the original extension so pandas doesn't get confused later!
        /// </summary>
        /// <param name="sourcePath">Where the file is currently sitting.</param>
        /// <param name="referenceName">What we want to call it once it's locked in the sandbox.</param>
        /// <returns>Just a task you can await until the copy finishes.</returns>
        public async Task UploadAsync(string sourcePath, string referenceName)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("sourcePath is required.");

            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Source file not found.", sourcePath);

            var safe = PathSafety.SanitizeReference(referenceName);
            var ext = Path.GetExtension(sourcePath); // e.g. ".csv"
            var destPath = Path.Combine(RootPath, safe + ext);

            await using var src = File.OpenRead(sourcePath);
            await using var dst = File.Create(destPath);
            await src.CopyToAsync(dst);
        }
        /// <summary>
        /// Looks through the sandbox folder and returns the names of the files we have. 
        /// You can pass a filter if you only want files with a specific word in their name.
        /// </summary>
        /// <param name="nameFilter">Optional. A word to search for in the file names.</param>
        /// <returns>A list of file names (not full paths, just the names).</returns>
        public IEnumerable<string> List(string? nameFilter = null)
        {
            var files = Directory.GetFiles(RootPath)
                .Select(Path.GetFileName)
                .Where(x => !string.IsNullOrWhiteSpace(x))!;

            if (!string.IsNullOrWhiteSpace(nameFilter))
            {
                var f = nameFilter.Trim();
                files = files.Where(n => n!.Contains(f, StringComparison.OrdinalIgnoreCase));
            }

            return files!;
        }
        /// <summary>
        /// Cleans house by deleting files from the sandbox. 
        /// If you don't pass a filter, watch out—it deletes everything!
        /// </summary>
        /// <param name="nameFilter">Optional. Only deletes files that have this word in their name.</param>
        /// <returns>The total number of files we threw in the trash.</returns>
        public int Delete(string? nameFilter = null)
        {
            var targets = List(nameFilter).ToList();
            foreach (var t in targets)
            {
                var p = Path.Combine(RootPath, t);
                if (File.Exists(p)) File.Delete(p);
            }
            return targets.Count;
        }

        /// <summary>
        /// Opens a file and reads its text. We cap this at a maximum number of characters so the AI doesn't choke on a 50GB file.
        /// </summary>
        /// <param name="referenceName">The safe name of the file to read.</param>
        /// <param name="maxChars">Our hard limit on how many characters to read before truncating. Defaults to 4000.</param>
        /// <returns>The text payload from the file.</returns>
        public async Task<string> ReadTextAsync(string referenceName, int maxChars = 4000)
        {
            var safe = PathSafety.SanitizeReference(referenceName);
            var path = Path.Combine(RootPath, safe);

            if (!File.Exists(path))
                return $"ERROR: file '{safe}' not found in store.";

            var text = await File.ReadAllTextAsync(path, Encoding.UTF8);
            if (text.Length <= maxChars) return text;
            return text.Substring(0, maxChars) + "\n...[truncated]...";
        }

        /// <summary>
        /// Figures out the exact, real-world drive path (like C:\...) for a sandboxed file.
        /// The tricky part is we have to scan the folder to figure out the file extension since reference names don't use extensions.
        /// </summary>
        /// <param name="referenceName">The short name of the sandboxed file.</param>
        /// <returns>The absolute path to that file string.</returns>
        public string GetAbsolutePath(string referenceName)
        {
            var safe = PathSafety.SanitizeReference(referenceName);

            // Try to find the stored file with any extension (e.g. sample_sales.csv)
            var match = Directory.GetFiles(RootPath, safe + ".*").FirstOrDefault();
            return match ?? Path.Combine(RootPath, safe);
        }
    }
}
