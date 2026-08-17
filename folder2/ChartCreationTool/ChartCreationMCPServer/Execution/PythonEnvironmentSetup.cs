using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace ChartCreationMCPServer.Execution
{
    /// <summary>The outcome of preparing the Python environment.</summary>
    internal enum PythonSetupStatus
    {
        /// <summary>Python and every required package were already present.</summary>
        Ready,

        /// <summary>Packages were missing and were installed successfully.</summary>
        PackagesInstalled,

        /// <summary>Python was installed but won't be visible until the process restarts (Windows PATH behavior).</summary>
        RestartRequired,

        /// <summary>Something is still missing and could not be fixed automatically.</summary>
        Incomplete
    }

    /// <summary>A detected Python installation.</summary>
    internal sealed record PythonInfo(
        string ExecutablePath,
        string Version,
        bool RequiredPackagesInstalled,
        List<string> MissingPackages);

    /// <summary>The verdict on the Python environment.</summary>
    internal sealed record PythonSetupResult(PythonInfo Info, PythonSetupStatus Status, string Detail)
    {
        /// <summary>True when chart generation can be expected to work.</summary>
        public bool IsReady => Status is PythonSetupStatus.Ready or PythonSetupStatus.PackagesInstalled;
    }

    /// <summary>
    /// Detects, installs, and caches the verdict on the Python environment. Called once at
    /// startup and again (cheaply, from cache) before every GenerateChart call.
    ///
    /// Rules: never terminates the process on failure — every path returns a verdict instead,
    /// surfaced via /health and GenerateChart's own error message. Every install command is
    /// hardcoded per platform; no caller or AI-generated input ever reaches a shell.
    /// </summary>
    internal sealed class PythonEnvironmentSetup
    {
        private static readonly Version MinPythonVersion = new(3, 8);
        private static readonly string[] RequiredPackages = { "matplotlib", "pandas", "numpy", "openpyxl" };

        private static readonly TimeSpan PythonInstallTimeout = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan PackageInstallTimeout = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DetectTimeout = TimeSpan.FromSeconds(5);

        // Guards the pipeline so concurrent callers (startup + a burst of early GenerateChart
        // calls) can't run detection/installation more than once.
        private static readonly SemaphoreSlim _gate = new(1, 1);
        private static PythonSetupResult? _cached;

        /// <summary>
        /// Returns the cached verdict if the environment is already known-good — the path every
        /// caller takes after the first successful check, at the cost of a single flag read.
        /// Otherwise runs detect → install → re-detect once, caches it, and returns it.
        /// This is the only entry point callers need; there is no separate "prepare at startup"
        /// step — the first call (from Program.cs at boot) does that work naturally.
        /// </summary>
        public static async Task<PythonSetupResult> EnsureReadyAsync(IConfiguration config)
        {
            if (_cached is { IsReady: true })
                return _cached;

            await _gate.WaitAsync();
            try
            {
                if (_cached is { IsReady: true })
                    return _cached; // another caller finished while this one waited

                _cached = await RunPipelineAsync(config);
                return _cached;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Prints a one-time summary of the environment (called by Program.cs after the startup check).</summary>
        public static void Report(PythonSetupResult result)
        {
            Console.WriteLine("=== Python Environment ===");
            Console.WriteLine($"Executable: {result.Info.ExecutablePath}");
            Console.WriteLine($"Version:    {result.Info.Version}");
            Console.WriteLine($"Packages:   {string.Join(", ", RequiredPackages)}");
            Console.WriteLine($"Status:     {result.Status}");
            Console.WriteLine($"            {result.Detail}");
            if (!result.IsReady)
                Console.WriteLine("            Chart generation will fail until this is resolved.");
            Console.WriteLine();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Pipeline: detect → install if needed → re-detect → verdict.
        // ──────────────────────────────────────────────────────────────────────

        private static async Task<PythonSetupResult> RunPipelineAsync(IConfiguration config)
        {
            var autoInstallPython = config.GetValue("AutoInstallPython", true);
            var autoInstallPackages = config.GetValue("AutoInstallPackages", true);

            var info = await DetectAsync();

            if (info is null)
            {
                if (!autoInstallPython)
                    return Incomplete("Python was not found and automatic installation is disabled (Python:AutoInstallPython is false).");

                if (!await InstallPythonAsync())
                    return Incomplete(
                        "Python was not found and could not be installed automatically. Installing system " +
                        "software usually requires administrator or sudo rights. Install Python 3.8+ manually, " +
                        "or run this server with Docker, where Python and all packages are already in the image.");

                info = await DetectAsync();

                // Linux picks up the new interpreter on the inherited PATH immediately;
                // Windows will not see it until the process restarts.
                if (info is null)
                    return new PythonSetupResult(Fallback(), PythonSetupStatus.RestartRequired,
                        "Python was installed but is not visible to this process yet. Restart the server.");
            }

            if (!info.RequiredPackagesInstalled)
            {
                if (!autoInstallPackages)
                    return new PythonSetupResult(info, PythonSetupStatus.Incomplete,
                        $"Missing packages: {string.Join(", ", info.MissingPackages)}. " +
                        "Automatic installation is disabled (Python:AutoInstallPackages is false).");

                var installed = await InstallPackagesAsync(info.ExecutablePath, info.MissingPackages);

                // Re-detect regardless of pip's exit code — pip can report success while an
                // import still fails, so the import test is what actually decides.
                info = await DetectAsync() ?? info;

                if (!info.RequiredPackagesInstalled)
                    return new PythonSetupResult(info, PythonSetupStatus.Incomplete,
                        (installed ? "pip reported success but the packages still cannot be imported. " : "Package installation failed. ") +
                        $"Install manually: {info.ExecutablePath} -m pip install {string.Join(" ", info.MissingPackages)}");

                return new PythonSetupResult(info, PythonSetupStatus.PackagesInstalled, "Missing packages were installed successfully.");
            }

            return new PythonSetupResult(info, PythonSetupStatus.Ready, "Python and all required packages are installed.");

            static PythonSetupResult Incomplete(string detail) => new(Fallback(), PythonSetupStatus.Incomplete, detail);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Detection — read-only, no side effects.
        // ──────────────────────────────────────────────────────────────────────

        private static async Task<PythonInfo?> DetectAsync()
        {
            var envPath = Environment.GetEnvironmentVariable("PYTHON_PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                var result = await TryValidateAsync(envPath);
                if (result != null)
                    return result;

                Console.WriteLine($"WARNING: PYTHON_PATH is set to '{envPath}' but validation failed. Trying auto-detection...");
            }

            var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new[] { "python", "python3", "py" }
                : new[] { "python3", "python" };

            foreach (var candidate in candidates)
            {
                var result = await TryValidateAsync(candidate);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static async Task<PythonInfo?> TryValidateAsync(string pythonPath)
        {
            var (exitCode, output) = await RunAsync(pythonPath, "--version", DetectTimeout);
            if (exitCode != 0)
                return null;

            var match = Regex.Match(output, @"Python\s+(\d+\.\d+\.\d+)", RegexOptions.IgnoreCase);
            if (!match.Success || !Version.TryParse(match.Groups[1].Value, out var version) || version < MinPythonVersion)
                return null;

            var missing = new List<string>();
            foreach (var package in RequiredPackages)
            {
                var (importExit, _) = await RunAsync(pythonPath, $"-c \"import {package}\"", DetectTimeout);
                if (importExit != 0)
                    missing.Add(package);
            }

            return new PythonInfo(pythonPath, match.Groups[1].Value, missing.Count == 0, missing);
        }

        /// <summary>
        /// A command name (never an absolute path) so a Python installed later resolves through
        /// PATH automatically after a restart, with no configuration change.
        /// </summary>
        private static PythonInfo Fallback() => new(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python" : "python3",
            "not detected", false, RequiredPackages.ToList());

        // ──────────────────────────────────────────────────────────────────────
        // Installation — the only part of this class that changes the machine.
        // ──────────────────────────────────────────────────────────────────────

        private static Task<bool> InstallPythonAsync()
        {
            var (file, args, manager) = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? ("cmd.exe", "/C winget install --id Python.Python.3.12 -e --silent --accept-package-agreements --accept-source-agreements", "winget")
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? ("/bin/bash", "-c \"brew install python@3.12\"", "Homebrew")
                    : ("/bin/bash", "-c \"apt-get update && apt-get install -y python3 python3-pip\"", "apt-get");

            Console.WriteLine($"Python not found. Installing with {manager} — this may take several minutes...");
            return InstallAndReport(file, args, PythonInstallTimeout, manager);
        }

        private static Task<bool> InstallPackagesAsync(string pythonPath, IEnumerable<string> packages)
        {
            var list = string.Join(" ", packages);
            Console.WriteLine($"Installing missing Python packages: {list} — this may take a minute...");
            return InstallAndReport(pythonPath, $"-m pip install {list}", PackageInstallTimeout, "pip");
        }

        private static async Task<bool> InstallAndReport(string file, string args, TimeSpan timeout, string label)
        {
            var (exitCode, output) = await RunAsync(file, args, timeout);
            if (exitCode != 0 && !string.IsNullOrWhiteSpace(output))
                Console.WriteLine($"  {label}: {Tail(output)}");
            return exitCode == 0;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Shared process runner — used by detection, installation, and syntax/version checks alike.
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Runs a process to completion, capturing stdout+stderr together. Never throws.</summary>
        private static async Task<(int ExitCode, string Output)> RunAsync(string file, string args, TimeSpan timeout)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = file,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };

                var output = new StringBuilder();
                process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using var cts = new CancellationTokenSource(timeout);
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                    return (-1, $"Timed out after {timeout.TotalSeconds:F0}s.");
                }

                return (process.ExitCode, output.ToString());
            }
            catch (Exception ex)
            {
                // Executable missing, permission denied, etc. — treated as failure, not thrown,
                // so the pipeline can continue and report it instead of crashing the server.
                return (-1, ex.Message);
            }
        }
        private static string Tail(string output, int lines = 3) =>
            string.Join(" | ", output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).TakeLast(lines));
    }
}