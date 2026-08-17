using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation
{
    /// <summary>
    /// A small tool that hunts down where Python is installed on your computer.
    /// It checks to make sure the version is new enough and has the right parts like 'pandas' and 'matplotlib'.
    /// </summary>
    internal sealed class PythonPathResolver
    {
        private const string MinPythonVersion = "3.8";
        private static readonly string[] RequiredPackages = { "matplotlib", "pandas", "numpy" };

        /// <summary>
        /// Looks everywhere it can think of to find the Python program on your system.
        /// First it looks at your environment settings, then it tries common commands like 'python3' and 'py'.
        /// </summary>
        /// <returns>All the info about your Python setup. If it totally fails, it will throw an error telling you what to fix.</returns>
        public static async Task<PythonInfo> ResolveAsync()
        {
            // 1. Check PYTHON_PATH env var first
            var envPath = Environment.GetEnvironmentVariable("PYTHON_PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                var envResult = await TryValidatePythonAsync(envPath);
                if (envResult != null)
                    return envResult;

                Console.WriteLine($"WARNING: PYTHON_PATH is set to '{envPath}' but validation failed. Trying auto-detection...");
            }

            // 2. Try common Python executables
            var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new[] { "python", "python3", "py" }
                : new[] { "python3", "python" };

            foreach (var candidate in candidates)
            {
                var result = await TryValidatePythonAsync(candidate);
                if (result != null)
                    return result;
            }

            // 3. No valid Python found
            throw new InvalidOperationException(
                "No valid Python installation found. Please ensure Python ≥3.8 is installed with matplotlib, pandas, and numpy.\n" +
                $"Or set PYTHON_PATH environment variable to your Python executable path.");
        }

        /// <summary>
        /// Tries to validate a Python executable and returns PythonInfo if valid, null otherwise.
        /// </summary>
        /// <param name="pythonPath">The path to validate.</param>
        /// <returns>A PythonInfo object if valid, else null.</returns>
        private static async Task<PythonInfo?> TryValidatePythonAsync(string pythonPath)
        {
            try
            {
                // Get Python version
                var version = await GetPythonVersionAsync(pythonPath);
                if (version == null || !IsVersionValid(version))
                    return null;

                // Check required packages
                var missingPackages = await GetMissingPackagesAsync(pythonPath);

                return new PythonInfo(
                    ExecutablePath: pythonPath,
                    Version: version,
                    RequiredPackagesInstalled: missingPackages.Count == 0,
                    MissingPackages: missingPackages
                );
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the Python version by running: python --version
        /// </summary>
        /// <param name="pythonPath">The path to test.</param>
        /// <returns>The version string if successful, else null.</returns>
        private static async Task<string?> GetPythonVersionAsync(string pythonPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };
                var output = new StringBuilder();

                process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using var cts = new CancellationTokenSource(5_000);
                await process.WaitForExitAsync(cts.Token);

                if (process.ExitCode != 0)
                    return null;

                var versionText = output.ToString();
                var match = Regex.Match(versionText, @"Python\s+(\d+\.\d+\.\d+)", RegexOptions.IgnoreCase);
                return match.Success ? match.Groups[1].Value : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Checks if the Python version meets the minimum requirement (≥3.8).
        /// </summary>
        /// <param name="version">The version string.</param>
        /// <returns>True if it meets requirements, false otherwise.</returns>
        private static bool IsVersionValid(string version)
        {
            try
            {
                var parts = version.Split('.');
                if (parts.Length < 2)
                    return false;

                var major = int.Parse(parts[0]);
                var minor = int.Parse(parts[1]);

                var minParts = MinPythonVersion.Split('.');
                var minMajor = int.Parse(minParts[0]);
                var minMinor = int.Parse(minParts[1]);

                return major > minMajor || (major == minMajor && minor >= minMinor);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks which required packages are missing by running: python -c "import package"
        /// </summary>
        /// <param name="pythonPath">The path to test.</param>
        /// <returns>A list of missing packages.</returns>
        private static async Task<List<string>> GetMissingPackagesAsync(string pythonPath)
        {
            var missing = new List<string>();

            foreach (var package in RequiredPackages)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = pythonPath,
                        Arguments = $"-c \"import {package}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = new Process { StartInfo = psi };
                    process.Start();

                    using var cts = new CancellationTokenSource(5_000);
                    await process.WaitForExitAsync(cts.Token);

                    if (process.ExitCode != 0)
                        missing.Add(package);
                }
                catch
                {
                    missing.Add(package);
                }
            }

            return missing;
        }

        /// <summary>
        /// Prints a user-friendly summary of the detected Python installation.
        /// </summary>
        /// <param name="info">The Python info object.</param>
        public static void PrintPythonInfo(PythonInfo info)
        {
            Console.WriteLine("=== Python Environment ===");
            Console.WriteLine($"Executable: {info.ExecutablePath}");
            Console.WriteLine($"Version:    Python {info.Version}");

            if (info.RequiredPackagesInstalled)
            {
                Console.WriteLine($"Packages:   All required packages installed ✓");
            }
            else
            {
                Console.WriteLine($"Packages:   MISSING: {string.Join(", ", info.MissingPackages)}");
                Console.WriteLine($"            Install with: pip install {string.Join(" ", info.MissingPackages)}");
            }

            Console.WriteLine();
        }
    }

    /// <summary>
    /// Information about a detected Python installation.
    /// </summary>
    internal sealed record PythonInfo(
        string ExecutablePath,
        string Version,
        bool RequiredPackagesInstalled,
        List<string> MissingPackages);
}
