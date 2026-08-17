using Microsoft.Extensions.AI;
using ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation.Storage;
using System.ComponentModel;
using System.Text;

namespace ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation
{
    /// <summary>
    /// The main command center. It plugs straight into the AI system and gives it actual tools so the AI can write, check, and run Python code to draw charts.
    /// </summary>
    internal sealed class ChartPlugin
    {
        private readonly StorageTool _storage;
        private readonly PythonCodeValidator _validator;
        private readonly PythonExecutor _executor;
        private readonly ErrorMappingStore _errorStore;
        private readonly ChartManifest _manifest;
        private readonly string _outputDir;

        private const int MaxRetries = 3;

        /// <summary>
        /// Instantiates the ChartPlugin which serves as a container for all chart creation operations and tools.
        /// </summary>
        /// <param name="storage">The storage tool to handle file operations.</param>
        /// <param name="validator">The code validator to check Python script safety.</param>
        /// <param name="executor">The Python execution engine.</param>
        /// <param name="errorStore">The storage for logging generated errors.</param>
        /// <param name="manifest">The manifest indicating the history of charts created.</param>
        /// <param name="outputDir">The directory path for chart outputs.</param>
        public ChartPlugin(
            StorageTool storage,
            PythonCodeValidator validator,
            PythonExecutor executor,
            ErrorMappingStore errorStore,
            ChartManifest manifest,
            string outputDir)
        {
            _storage = storage;
            _validator = validator;
            _executor = executor;
            _errorStore = errorStore;
            _manifest = manifest;
            _outputDir = outputDir;
            Directory.CreateDirectory(_outputDir);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Storage tools (unchanged)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Uploads a data file from a specified disk location into the safe internal storage for AI usage.
        /// </summary>
        /// <param name="filePath">Absolute path to the source file on disk.</param>
        /// <param name="referenceName">Short reference name used later to refer to this file, e.g. 'BLUE'.</param>
        /// <returns>A string confirming success or returning an error message.</returns>
        [Description("Upload a data file (e.g. a CSV) from disk into the in-app store under a short reference name. " +
                     "Example: filePath='C:\\data\\sales.csv', referenceName='BLUE'.")]
        public async Task<string> UploadFileAsync(
            [Description("Absolute path to the source file on disk.")] string filePath,
            [Description("Short reference name used later to refer to this file, e.g. 'BLUE'.")] string referenceName)
        {
            try
            {
                return await _storage.UploadAsync(filePath, referenceName);
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        /// <summary>
        /// Lists all data files currently tracked in the application's secure storage store.
        /// </summary>
        /// <param name="filter">Optional substring filter to list specific files.</param>
        /// <returns>A formatted string detailing the files present in the store.</returns>
        [Description("List all data files currently stored in the in-app store. " +
                     "Optionally filter by a substring of the name.")]
        public string ListFiles(
            [Description("Optional substring filter. Leave empty to list all files.")] string? filter = null)
        {
            return _storage.List(string.IsNullOrWhiteSpace(filter) ? null : filter);
        }

        /// <summary>
        /// Removes files from the application's secure storage, optionally filtered by a specific term.
        /// </summary>
        /// <param name="filter">Optional substring filter to delete only matching files.</param>
        /// <returns>A formatted string detailing the outcome of the deletion process.</returns>
        [Description("Delete stored files. Optionally filter by a substring of the name to delete only matching files. " +
                     "Leave filter empty to delete all files.")]
        public string DeleteFiles(
            [Description("Optional substring filter. Leave empty to delete all files.")] string? filter = null)
        {
            return _storage.Delete(string.IsNullOrWhiteSpace(filter) ? null : filter);
        }

        /// <summary>
        /// Reads a snippet of a specific stored file to understand its data layout before utilizing it for charts.
        /// </summary>
        /// <param name="referenceName">Reference name of the stored file to preview.</param>
        /// <returns>A partial string representation of the file's text contents.</returns>
        [Description("Preview the contents of a stored file (first ~2000 characters). " +
                     "Use this to inspect CSV column names and sample data before generating a chart.")]
        public async Task<string> PreviewFileAsync(
            [Description("Reference name of the file to preview, e.g. 'BLUE'.")] string referenceName)
        {
            try
            {
                return await _storage.ReadAsync(referenceName, maxChars: 2000);
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        /// <summary>
        /// Retrieves the real absolute file path on disk corresponding to a file reference name.
        /// This path is intended to be fed directly into Python code.
        /// </summary>
        /// <param name="referenceName">Reference name of the stored file.</param>
        /// <returns>The true disk path of the requested file or an error message if it cannot be found.</returns>
        [Description("Resolve a file reference name to its absolute disk path. " +
                     "Use this to get the real file path to use in your Python code for loading data.")]
        public string ResolveFilePath(
            [Description("Reference name of the stored file, e.g. 'BLUE'.")] string referenceName)
        {
            try
            {
                var path = _storage.ResolvePath(referenceName);
                // Return forward-slash path for Python compatibility
                return path.Replace("\\", "/");
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        /// <summary>
        /// Generates a summary text report of all charts rendered so far in the active session.
        /// </summary>
        /// <returns>A string block organizing history of charts made and their validation results.</returns>
        [Description("List all charts generated in the current session with their status and metadata.")]
        public string ListGeneratedCharts()
        {
            return _manifest.FormatEntries();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Code execution tool (NEW — LLM writes Python directly)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Validates, saves, and executes AI-generated Python code representing a visual chart, outputting an image.
        /// </summary>
        /// <param name="pythonCode">The complete Python code for the chart.</param>
        /// <param name="chartId">A short unique identifier for this chart.</param>
        /// <returns>A string outlining whether execution succeeded and displaying logs/errors if applicable.</returns>
        [Description(
            "Validate and execute a matplotlib Python script that creates a chart image (.png). " +
            "You must write the COMPLETE Python code yourself. " +
            "Requirements:\n" +
            "  - Always start with: import matplotlib\\nmatplotlib.use('Agg')\\nimport matplotlib.pyplot as plt\n" +
            "  - Always end with: plt.savefig(r\"OUTPUT_PATH\", dpi=150, bbox_inches='tight')\n" +
            "  - NEVER use plt.show()\n" +
            "  - NEVER import os, subprocess, sys, shutil, or any system modules\n" +
            "  - To load data files, use the ResolveFilePath tool first to get the absolute path, " +
            "then use pandas: import pandas as pd; df = pd.read_csv(r\"path\")\n" +
            "  - For Excel files use pd.read_excel(), for TSV use pd.read_csv(sep='\\t')\n" +
            "Returns SUCCESS with image path, or an error message for you to fix and retry.")]
        public async Task<string> GenerateAndRunChart(
            [Description("The complete Python code for the chart.")] string pythonCode,
            [Description("A short unique identifier for this chart, e.g. 'c1', 'sales_bar'.")] string chartId)
        {
            var startTime = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(pythonCode))
                return "ERROR: pythonCode is empty. Please provide complete Python code.";

            if (string.IsNullOrWhiteSpace(chartId))
                return "ERROR: chartId is required. Provide a short unique name like 'c1'.";

            // Sanitise chartId for safe file naming
            var safeId = new string(chartId.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
            if (string.IsNullOrWhiteSpace(safeId)) safeId = "chart";

            var imagePath = Path.Combine(_outputDir, $"{safeId}.png");
            var scriptPath = Path.Combine(_outputDir, $"{safeId}.py");

            // Inject the real image path into the LLM's placeholder
            pythonCode = pythonCode.Replace("OUTPUT_PATH", imagePath.Replace("\\", "/"));

            // ── 1. Validate the code ───────────────────────────────────────
            var validation = await _validator.ValidateAsync(pythonCode, imagePath);
            if (!validation.IsValid)
            {
                var errorMsg = "VALIDATION ERROR — fix the following issues and try again:\n" +
                               string.Join("\n", validation.Errors.Select(e => $"  • {e}"));

                // Record validation errors for future learning
                await _errorStore.RecordErrorAsync(
                    error: string.Join("; ", validation.Errors),
                    codeSnippet: pythonCode,
                    fix: "Code failed pre-execution validation");

                // Record failed attempt in manifest
                await _manifest.AddEntryAsync(new ChartEntry
                {
                    ChartId = safeId,
                    Timestamp = DateTime.UtcNow,
                    ScriptPath = scriptPath,
                    ImagePath = imagePath,
                    InputFiles = new List<string>(),
                    ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                    Success = false,
                    ErrorMessage = string.Join("; ", validation.Errors)
                });

                return errorMsg;
            }

            // ── 2. Save and execute ────────────────────────────────────────
            Console.WriteLine($"\n──────────────── Generated Python Code ({scriptPath}) ────────────────");
            Console.WriteLine(pythonCode);
            Console.WriteLine("────────────────────────────────────────────────────────\n");

            await File.WriteAllTextAsync(scriptPath, pythonCode, Encoding.UTF8);

            var result = await _executor.ExecuteAsync(scriptPath);
            var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            if (result.Success)
            {
                // Record success in manifest
                await _manifest.AddEntryAsync(new ChartEntry
                {
                    ChartId = safeId,
                    Timestamp = DateTime.UtcNow,
                    ScriptPath = scriptPath,
                    ImagePath = imagePath,
                    InputFiles = ExtractInputFiles(pythonCode),
                    ExecutionTimeMs = executionTime,
                    Success = true
                });

                // Try to open the chart image in the default viewer
                if (File.Exists(imagePath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = imagePath,
                            UseShellExecute = true
                        });
                    }
                    catch { /* best effort */ }
                }

                return $"SUCCESS: Chart saved to {imagePath}\n" +
                       $"Script saved to {scriptPath}\n" +
                       $"Execution time: {result.ExecutionTimeMs:F0}ms\n" +
                       $"Peak memory: {FormatBytes(result.PeakMemoryBytes)}\n" +
                       $"Output: {result.StandardOutput.Trim()}";
            }
            else
            {
                var errMsg = string.IsNullOrWhiteSpace(result.StandardError)
                    ? "Unknown execution error"
                    : result.StandardError.Trim();

                // Record execution error for future learning
                await _errorStore.RecordErrorAsync(
                    error: errMsg,
                    codeSnippet: pythonCode,
                    fix: "");

                // Record failed attempt in manifest
                await _manifest.AddEntryAsync(new ChartEntry
                {
                    ChartId = safeId,
                    Timestamp = DateTime.UtcNow,
                    ScriptPath = scriptPath,
                    ImagePath = imagePath,
                    InputFiles = ExtractInputFiles(pythonCode),
                    ExecutionTimeMs = executionTime,
                    Success = false,
                    ErrorMessage = errMsg
                });

                return $"EXECUTION ERROR — the code ran but failed. Fix the error and try again:\n{errMsg}";
            }
        }

        /// <summary>
        /// Looks at the Python code and finds the names of any data files the AI tried to read so we can keep track of them.
        /// </summary>
        /// <param name="pythonCode">The python code string.</param>
        /// <returns>A list of file paths found.</returns>
        private List<string> ExtractInputFiles(string pythonCode)
        {
            var files = new List<string>();

            // Look for common pandas read patterns
            var patterns = new[]
            {
                @"pd\.read_csv\(['""]([^'""]+)['""]",
                @"pd\.read_excel\(['""]([^'""]+)['""]",
                @"pd\.read_table\(['""]([^'""]+)['""]"
            };

            foreach (var pattern in patterns)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(pythonCode, pattern);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        files.Add(match.Groups[1].Value);
                    }
                }
            }

            return files.Distinct().ToList();
        }

        /// <summary>
        /// Turns raw byte numbers into something easier to read, like KB or MB.
        /// </summary>
        /// <param name="bytes">The number of bytes.</param>
        /// <returns>A formatted string representing the size.</returns>
        private static string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helper: build the list of AITool instances for agent registration
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Packs up all our useful features and hands them to the central AI brain, letting it know exactly what commands it can use.
        /// </summary>
        /// <returns>A list of AITools to register with the agent.</returns>
        public IList<AITool> GetTools()
        {
            return new List<AITool>
            {
                AIFunctionFactory.Create(UploadFileAsync),
                AIFunctionFactory.Create(ListFiles),
                AIFunctionFactory.Create(DeleteFiles),
                AIFunctionFactory.Create(PreviewFileAsync),
                AIFunctionFactory.Create(ResolveFilePath),
                AIFunctionFactory.Create(ListGeneratedCharts),
                AIFunctionFactory.Create(GenerateAndRunChart),
            };
        }
    }
}
