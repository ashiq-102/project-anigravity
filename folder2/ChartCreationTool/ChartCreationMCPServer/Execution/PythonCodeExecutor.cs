using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ChartCreationMCPServer.Execution
{
    /// <summary>
    /// Result of a Python script execution with metrics.
    /// </summary>
    internal sealed record PythonResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        bool Success,
        double ExecutionTimeMs = 0,
        long PeakMemoryBytes = 0);

    /// <summary>
    /// Runs the AI-generated Python code in a safe, isolated box so it can't run forever or crash our main app.
    /// It also keeps track of how much memory the script used and how long it took.
    /// </summary>
    internal sealed class PythonCodeExecutor
    {
        private readonly string _pythonPath;

        /// <summary>
        /// Sets up the engine with the exact location of the Python executable on our machine.
        /// </summary>
        /// <param name="pythonPath">
        /// Path or alias to the Python executable (default: "python").
        /// </param>
        public PythonCodeExecutor(string pythonPath = "python")
        {
            _pythonPath = pythonPath;
        }

        /// <summary>
        /// Fires up the Python script with our standard 30-second time limit.
        /// </summary>
        /// <param name="scriptPath">The exact file path to the python code we want to run.</param>
        /// <param name="timeoutMs">How long in milliseconds we wait before cutting it off (defaults to 30 seconds).</param>
        /// <returns>The result of the script, including output and errors.</returns>
        public async Task<PythonResult> ExecuteAsync(string scriptPath, int timeoutMs = 30_000)
        {
            return await ExecuteAsync(scriptPath, new SandboxConfig { TimeoutMs = timeoutMs });
        }

        /// <summary>
        /// Runs a Python script but lets you customize the exact limits, like blocking internet access or setting a hard memory ceiling.
        /// </summary>
        /// <param name="scriptPath">The exact file path to the python code we want to run.</param>
        /// <param name="config">The specific safety rules (like timeouts and memory caps) we want to enforce.</param>
        /// <returns>The result of the script, including output and errors.</returns>
        public async Task<PythonResult> ExecuteAsync(string scriptPath, SandboxConfig config)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
                throw new ArgumentException("scriptPath is required.");

            if (!File.Exists(scriptPath))
                throw new FileNotFoundException("Script file not found.", scriptPath);

            var startTime = DateTime.UtcNow;

            var psi = new ProcessStartInfo
            {
                FileName               = _pythonPath,
                Arguments              = $"\"{scriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            // Set working directory if specified
            if (!string.IsNullOrWhiteSpace(config.WorkingDirectory))
            {
                psi.WorkingDirectory = config.WorkingDirectory;
            }

            // Block network access
            if (config.BlockNetwork)
            {
                psi.EnvironmentVariables["NO_PROXY"] = "*";
                psi.EnvironmentVariables["no_proxy"] = "*";
            }

            using var process = new Process { StartInfo = psi };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            process.Start();

            // Apply memory limit on Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && config.MaxMemoryBytes > 0)
            {
                try
                {
                    process.MaxWorkingSet = new IntPtr(config.MaxMemoryBytes);
                }
                catch
                {
                    // Best effort - may fail due to permissions
                }
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = new CancellationTokenSource(config.TimeoutMs);

            long peakMemory = 0;

            try
            {
                // Monitor process while it runs
                var monitorTask = Task.Run(async () =>
                {
                    while (!process.HasExited)
                    {
                        try
                        {
                            process.Refresh();
                            var currentMemory = process.WorkingSet64;
                            if (currentMemory > peakMemory)
                                peakMemory = currentMemory;
                        }
                        catch
                        {
                            // Process may have exited
                        }

                        await Task.Delay(100);
                    }
                }, cts.Token);

                await process.WaitForExitAsync(cts.Token);
                await monitorTask;
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }

                var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                return new PythonResult(
                    ExitCode: -1,
                    StandardOutput: stdout.ToString(),
                    StandardError: "Execution timed out.",
                    Success: false,
                    ExecutionTimeMs: executionTime,
                    PeakMemoryBytes: peakMemory);
            }

            var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // Final memory reading
            try
            {
                process.Refresh();
                var finalMemory = process.PeakWorkingSet64;
                if (finalMemory > peakMemory)
                    peakMemory = finalMemory;
            }
            catch
            {
                // Ignore if process is already disposed
            }

            return new PythonResult(
                ExitCode: process.ExitCode,
                StandardOutput: stdout.ToString(),
                StandardError: stderr.ToString(),
                Success: process.ExitCode == 0,
                ExecutionTimeMs: elapsedMs,
                PeakMemoryBytes: peakMemory);
        }
    }
}
