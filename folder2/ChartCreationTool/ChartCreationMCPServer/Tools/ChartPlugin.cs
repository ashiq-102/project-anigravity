using Microsoft.Extensions.Configuration;
using ChartCreationMCPServer.Execution;
using ChartCreationMCPServer.Storage;
using ModelContextProtocol.Server;
using Microsoft.AspNetCore.Http;
using System.ComponentModel;
using System.Text;

namespace ChartCreationMCPServer.Tools
{
    /// <summary>
    /// The main command center. It plugs straight into the MCP server and gives the AI agent actual tools
    /// so the AI can write, check, and run Python code to draw charts.
    /// Exposes 7 MCP tools for uploading data files, previewing them, generating charts from
    /// Python/matplotlib code, and listing stored charts. All storage is scoped per team.
    /// </summary>
    [McpServerToolType]
    internal sealed class ChartPlugin
    {
        private readonly IStorageStore _store;
        private readonly PythonCodeValidator _validator;
        private readonly PythonCodeExecutor _executor;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfigurationSection _pythonConfig;
        private readonly string _outputDir;

        /// <summary>
        /// Instantiates the ChartPlugin which serves as a container for all chart creation operations and tools.
        /// All dependencies are injected by ASP.NET Core DI from Program.cs registrations.
        /// </summary>
        /// <param name="store">The storage store to handle file operations.</param>
        /// <param name="validator">The code validator to check Python script safety.</param>
        /// <param name="executor">The Python execution engine.</param>

        public ChartPlugin(
            IStorageStore store,
            PythonCodeValidator validator,
            PythonCodeExecutor executor,
            IHttpContextAccessor httpContextAccessor,
             IConfigurationSection pythonConfig)
        {
            _store = store;
            _validator = validator;
            _executor = executor;
            _httpContextAccessor = httpContextAccessor;
            _pythonConfig = pythonConfig;

            _outputDir = Path.Combine(AppContext.BaseDirectory, "output");
            Directory.CreateDirectory(_outputDir);
        }

        /// <summary>
        /// Reads the Team-Name header from the current request, falling back to "default"
        /// when absent. The server is the sole authority on team-to-prefix mapping.
        /// </summary>
        private string CurrentTeam()
        {
            var header = _httpContextAccessor.HttpContext?.Request.Headers["Team-Name"].ToString();
            return string.IsNullOrWhiteSpace(header) ? "default" : header;
        }

        // ────────────────────────────────────────────────────────────────────────────────────────────────
        // All tools, total 7 tools are available for the AI to manage files and generate charts.
        // Each tool is a public method with a Description attribute that explains to the AI how to use it.
        // ────────────────────────────────────────────────────────────────────────────────────────────────

    
        /// <summary>
        /// Tool-01
        /// Lists all data files currently tracked in Azure Blob Storage (input-files container).
        /// </summary>
        /// <param name="filter">Optional substring filter to list specific files.</param>
        /// <returns>A formatted string detailing the files present in the store.</returns>
        [McpServerTool, Description(
            "List all data files currently stored in Azure Blob Storage (input-files container). " +
            "Optionally filter by a substring of the name.")]
        public string ListUploadedFiles(
            [Description("Optional substring filter. Leave empty to list all files.")] string? filter = null)
        {
            var files = _store.List(CurrentTeam(), string.IsNullOrWhiteSpace(filter) ? null : filter).ToArray();
            return files.Length == 0
                ? "OK: No files in input store."
                : "OK: Files in input-files container:\n" + string.Join("\n", files);
        }

        /// <summary>
        /// Tool-02
        /// Removes files from Azure Blob Storage (input-files container), optionally filtered by a specific term.
        /// </summary>
        /// <param name="filter">Optional substring filter to delete only matching files.</param>
        /// <returns>A formatted string detailing the outcome of the deletion process.</returns>
        [McpServerTool, Description(
            "Delete stored files from Azure Blob Storage (input-files container). " +
            "Optionally filter by a substring of the name to delete only matching files. " +
            "Leave filter empty to delete all files.")]
        public string DeleteUploadedFiles(
            [Description("Optional substring filter. Leave empty to delete all files.")] string? filter = null)
        {
            var n = _store.Delete(CurrentTeam(), string.IsNullOrWhiteSpace(filter) ? null : filter);
            return $"OK: Deleted {n} file(s) from input-files container.";
        }

        /// <summary>
        /// Tool-03
        /// Reads a snippet of a specific stored blob to understand its data layout before utilizing it for charts.
        /// </summary>
        /// <param name="referenceName">Reference name of the stored file to preview.</param>
        /// <returns>A partial string representation of the file's text contents.</returns>
        [McpServerTool, Description(
           "Preview the contents of a stored file (first ~2000 characters) from Azure Blob Storage. " +
           "Call this before GenerateChart when you need to know the column names or data structure of a file. " +
           "Use this to inspect CSV column names and sample data before writing your Python code.")]
        public async Task<string> PreviewUploadedFile(
            [Description("Reference name of the file to preview, e.g. 'sales' ")] string referenceName)
        {
            try
            {
                return await _store.ReadTextAsync(referenceName, CurrentTeam(), maxChars: 2000);
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        /// <summary>
        /// Tool-04
        /// Uploads a file's content into Azure Blob Storage (input-files container).
        /// The reference name is automatically derived from the original filename so the user always
        /// recognises it — e.g. "sales.csv" is stored and referred to as "sales".
        ///
        /// Takes the file's content directly (Base64-encoded) rather than a disk path, since the
        /// caller may be a different machine than this server — a path on the caller's machine
        /// means nothing to this server's own filesystem.
        /// </summary>
        /// <param name="fileName">The original file name, including its extension (e.g. "Sales.csv").</param>
        /// <param name="fileContentBase64">The file's full content, Base64-encoded.</param>
        /// <returns>A string confirming success with the reference name, or an error message.</returns>
        [McpServerTool(Name = "upload_file"), Description(
             "Upload a data file's content into Azure Blob Storage. " +
            "The reference name is automatically derived from the original filename — " +
            "e.g. uploading 'sales.csv' stores it as 'sales'.")]
        public async Task<string> UploadFileAsync(
            [Description("The original file name, including its extension (e.g. 'Sales.csv').")] string fileName,
            [Description("The file's full content, Base64-encoded.")] string fileContentBase64)
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(fileContentBase64);
            }
            catch (FormatException)
            {
                return "ERROR: fileContentBase64 is not valid Base64.";
            }

            var safeName = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(safeName))
                return "ERROR: fileName is required.";

            var tempDir = Path.Combine(Path.GetTempPath(), "chart-uploads", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var tempPath = Path.Combine(tempDir, safeName);

            try
            {
                await File.WriteAllBytesAsync(tempPath, bytes);
                var referenceName = await _store.UploadAsync(tempPath, CurrentTeam());

                return $"OK: Uploaded '{safeName}' to Azure Blob Storage.\n" +
                       $"Reference name: '{referenceName}'\n" +
                       $"Use '{referenceName}' in future requests to refer to this file.";
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
            }
        }

        /// <summary>
        /// Tool-05
        /// Downloads the input blob to a local temp file on the server and returns that temp path
        /// so Python code can read it via pandas. This path is intended to be fed directly into Python code.
        /// </summary>
        /// <param name="referenceName">Reference name of the stored file.</param>
        /// <returns>The local temp path of the file on the server, or an error message if it cannot be found.</returns>
        [McpServerTool, Description(
            "Resolve a file reference name to its local temp path on the server. " +
            "Always call this before GenerateChart when the chart uses a data file. " +
            "This downloads the blob from Azure to a local temp file if needed — Python cannot access Azure Blob directly. " +
            "Use the returned path in your Python code for loading data with pandas.")]
        public string ResolveFilePath(
            [Description("Reference name of the stored file, e.g. 'sales' ")] string referenceName)
        {
            try
            {
                var path = _store.GetAbsolutePath(referenceName, CurrentTeam());
                // Return forward-slash path for Python compatibility
                return path.Replace("\\", "/");
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        /// <summary>
        /// Tool-06
        /// Validates, saves, and executes AI-generated Python code representing a visual chart, outputting an image.
        /// On success the PNG is uploaded to Azure Blob Storage and a SAS URL is returned to the agent.
        /// </summary>
        /// <param name="pythonCode">The complete Python code for the chart.</param>
        /// <param name="chartId">A short unique identifier for this chart.</param>
        /// <returns>A string outlining whether execution succeeded, the SAS URL, or displaying errors if applicable.</returns>
        [McpServerTool, Description(
            "Validate and execute a matplotlib Python script that creates a chart image (.png). " +
            "You must write the COMPLETE Python code yourself. " +
            "Requirements:\n" +
            "  - Always start with: import matplotlib\nmatplotlib.use('Agg')\nimport matplotlib.pyplot as plt\n" +
            "  - Always end with: plt.savefig(r\"OUTPUT_PATH\", dpi=150, bbox_inches='tight')\n" +
            "  - NEVER use plt.show()\n" +
            "  - NEVER import os, subprocess, sys, shutil, or any system modules\n" +
            "  - To load data files, use the ResolveFilePath tool first to get the temp path, " +
            "then use pandas: import pandas as pd; df = pd.read_csv(r\"path\")\n" +
            "  - For Excel files use pd.read_excel(), for TSV use pd.read_csv(sep='\\t')\n" +
            "On success returns a SAS URL to view and download the chart from Azure Blob Storage.")]
        public async Task<string> GenerateChart(
            [Description("The complete Python code for the chart.")] string pythonCode,
            [Description("A short unique identifier for this chart, e.g. 'c1', 'sales_bar'.")] string chartId)
        {

            var pythonStatus = await PythonEnvironmentSetup.EnsureReadyAsync(_pythonConfig);
            if (!pythonStatus.IsReady)
            {
                return $"ERROR: Python environment is not ready — {pythonStatus.Detail}";
            }

            var startTime = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(pythonCode))
                return "ERROR: pythonCode is empty. Please provide complete Python code.";

            if (string.IsNullOrWhiteSpace(chartId))
                return "ERROR: chartId is required. Provide a short unique name like 'c1'.";

            // Sanitise chartId for safe file naming
            var safeId = new string(chartId.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
            if (string.IsNullOrWhiteSpace(safeId)) safeId = "chart";

            // Create a unique identifier for this execution to avoid filename collisions, using the safe chartId and a timestamp
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var uniqueId = $"{safeId}_{timestamp}";
            var imagePath = Path.Combine(_outputDir, $"{uniqueId}.png");
            var scriptPath = Path.Combine(_outputDir, $"{uniqueId}.py");

            // Inject the real image path into the LLM's placeholder
            pythonCode = pythonCode.Replace("OUTPUT_PATH", imagePath.Replace("\\", "/"));

            // Validate the code ───────────────────────────────────────
            var validation = await _validator.ValidateAsync(pythonCode, imagePath);
            if (!validation.IsValid)
            {
                var errorMsg = "VALIDATION ERROR — fix the following issues and try again:\n" +
                               string.Join("\n", validation.Errors.Select(e => $"  • {e}"));

                return errorMsg;
            }

            /* Showing the code in mcp console
            // Save and execute ────────────────────────────────────────
            Console.WriteLine($"\n──────────────── Generated Python Code ({scriptPath}) ────────────────");
            Console.WriteLine(pythonCode);
            Console.WriteLine("────────────────────────────────────────────────────────\n");
            */

            await File.WriteAllTextAsync(scriptPath, pythonCode, Encoding.UTF8);


            // Execute the Python script and upload the result to Azure Blob
            try
            {

                var result = await _executor.ExecuteAsync(scriptPath);
                var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                if (result.Success)
                {
                    string blobUrl;
                    bool uploadedToBlobSuccessfully = true;
                    try
                    {
                        blobUrl = await _store.UploadChartAsync(imagePath, uniqueId, CurrentTeam());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Blob Upload Failed] chartId={uniqueId}: {ex.Message}");
                        blobUrl = "";
                        uploadedToBlobSuccessfully = false;
                    }

                    if (!uploadedToBlobSuccessfully)
                    {
                        return $"Your chart was generated successfully but could not be saved to cloud storage at this time. " +
                               $"Please try again in a moment, or contact support if the issue persists.\n\n" +
                               $"Execution time: {result.ExecutionTimeMs:F0}ms  |  " +
                               $"Peak memory: {FormatUtils.FormatBytes(result.PeakMemoryBytes)}";
                    }

                    // Return both the chart URL and the Python code that was executed.
                    return $"Chart generated and uploaded successfully.\n\n" +
                           $"View / Download URL (valid 1 year):\n" +
                           $"{blobUrl}\n\n" +
                           $"Python code that was executed:\n" +
                           $"```python\n{pythonCode}\n```\n\n" +
                           $"Execution time: {result.ExecutionTimeMs:F0}ms  |  " +
                           $"Peak memory: {FormatUtils.FormatBytes(result.PeakMemoryBytes)}";
                }
                else
                {
                    var errMsg = string.IsNullOrWhiteSpace(result.StandardError)
                        ? "Unknown execution error"
                        : result.StandardError.Trim();

                    return $"EXECUTION ERROR — the code ran but failed. Fix the error and try again:\n{errMsg}";
                }
            }
            // delete png and .py files, we don't want to keep local files on the server disk
            // dot py — Python code is already in memory and returned to the agent.
            // dot png — either uploaded to Azure Blob (SAS URL returned) or execution failed (no PNG exists).
            finally
            {
                try { if (File.Exists(scriptPath)) File.Delete(scriptPath); } catch { /* best effort */ }
                try { if (File.Exists(imagePath)) File.Delete(imagePath); } catch { /* best effort */ }
            }
        }

        /// <summary>
        /// Tool-07
        /// Lists every chart this team has ever generated, with a viewable link for each.
        /// Reads directly from blob storage, so it includes charts from past sessions.
        /// </summary>
        /// <returns>A formatted list of chart names and URLs, or a note if none exist.</returns>
        [McpServerTool, Description(
            "List charts stored in blob storage from PREVIOUS sessions or the team's full history — " +
            "charts that are NOT part of the current conversation. Use ONLY when the user explicitly " +
            "asks for charts beyond this conversation, such as 'all charts I have ever made', " +
            "'charts from past sessions', or 'everything my team has stored'. " +
            "Do NOT use this for charts made earlier in the current conversation — those are in your " +
            "memory and should be answered by reusing their existing URLs. Returns every stored chart " +
            "for the team; if the user wants only some, select from the returned list yourself.")]
        public string ListGeneratedCharts()
        {
            var charts = _store.ListCharts(CurrentTeam()).ToList();

            if (charts.Count == 0)
                return "No charts have been generated by this team yet.";

            var lines = charts.Select((c, i) => $"{i + 1}. {c.Name}\n   {c.Url}");
            return $"All charts generated by this team ({charts.Count}):\n" + string.Join("\n", lines);
        }
    }
}
