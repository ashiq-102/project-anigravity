using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace ChartCreationMCPServer.Execution
{
    /// <summary>
    /// The security guard. Before we run any AI-generated Python code, it checks to make sure the code looks right and doesn't try to hack into our system (like trying to delete random files).
    /// </summary>
    internal sealed class PythonCodeValidator
    {
        private readonly string _pythonPath;

        // ── Blocked imports ────────────────────────────────────────────────
        private static readonly HashSet<string> BlockedImports = new(StringComparer.OrdinalIgnoreCase)
        {
            "os", "subprocess", "sys", "shutil", "socket", "http",
            "requests", "pathlib", "ctypes", "signal", "threading",
            "multiprocessing", "webbrowser", "tempfile", "glob"
        };

        // ── Blocked function calls ─────────────────────────────────────────
        private static readonly string[] BlockedCalls =
        {
            "exec(", "eval(", "__import__(", "compile(",
            "globals(", "locals(", "getattr(", "setattr(",
            "delattr(", "open("
        };

        /// <summary>
        /// Initializes a new instance of the validator with the given Python executable path.
        /// </summary>
        /// <param name="pythonPath">The path to the Python executable.</param>
        public PythonCodeValidator(string pythonPath = "python")
        {
            _pythonPath = pythonPath;
        }

        /// <summary>
        /// Runs the Python code through our three-step security checkpoint: 
        /// 1. Makes sure it isn't trying to do anything illegal or hack us. 
        /// 2. Checks that it's actually drawing a chart. 
        /// 3. Asks Python directly if there are any typos.
        /// </summary>
        /// <param name="code">The code to validate.</param>
        /// <param name="expectedOutputPath">The output path we expect.</param>
        /// <returns>A ValidationResult.</returns>
        public async Task<ValidationResult> ValidateAsync(string code, string expectedOutputPath)
        {
            var errors = new List<string>();

            // ── 1. Safety check ────────────────────────────────────────────
            CheckSafety(code, errors);

            // ── 2. Structure check ─────────────────────────────────────────
            CheckStructure(code, expectedOutputPath, errors);

            // Return early if static checks already failed — no need to invoke Python
            if (errors.Count > 0)
                return new ValidationResult(false, errors);

            // ── 3. Syntax check via Python ast.parse ───────────────────────
            await CheckSyntaxAsync(code, errors);

            return new ValidationResult(errors.Count == 0, errors);
        }

        // ──────────────────────────────────────────────────────────────────────
        // 1. Safety validation
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Scans the Python code line by line to ensure it doesn't import forbidden system libraries or call malicious functions.
        /// </summary>
        /// <param name="code">The Python code string to inspect.</param>
        /// <param name="errors">A list to append any safety violations found.</param>
        internal void CheckSafety(string code, List<string> errors)
        {
            // Check each line for blocked imports
            var lines = code.Split('\n');
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.StartsWith('#')) continue; // skip comments

                // "import X" or "from X import ..."
                if (line.StartsWith("import ") || line.StartsWith("from "))
                {
                    foreach (var blocked in BlockedImports)
                    {
                        // Match "import os", "import os.path", "from os import ...", etc.
                        if (Regex.IsMatch(line, $@"\b{Regex.Escape(blocked)}\b", RegexOptions.IgnoreCase))
                        {
                            errors.Add($"Forbidden import detected: '{blocked}' in line: {line.Trim()}");
                        }
                    }
                }
            }

            // Check for blocked function calls
            foreach (var blocked in BlockedCalls)
            {
                if (code.Contains(blocked, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Forbidden call detected: '{blocked.TrimEnd('(')}'");
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // 2. Structure validation
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Checks the logic of the Python code to make sure it functions as a proper script for generating charts, 
        /// such as verifying it imports matplotlib and saves directly to a file without trying to display a window.
        /// </summary>
        /// <param name="code">The Python code string to inspect.</param>
        /// <param name="expectedOutputPath">The expected output path for the chart.</param>
        /// <param name="errors">A list to append any structural errors found.</param>
        internal void CheckStructure(string code, string expectedOutputPath, List<string> errors)
        {
            // Must import matplotlib
            if (!code.Contains("matplotlib", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Code must import matplotlib (e.g. 'import matplotlib.pyplot as plt').");
            }

            // Must contain plt.savefig
            if (!code.Contains("plt.savefig(", StringComparison.OrdinalIgnoreCase) &&
                !code.Contains("savefig(", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Code must contain plt.savefig() to save the chart image.");
            }

            // Must NOT contain plt.show() — blocks in headless mode
            if (Regex.IsMatch(code, @"plt\.show\s*\(", RegexOptions.IgnoreCase))
            {
                errors.Add("Code must not contain plt.show() — use plt.savefig() instead (headless mode).");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // 3. Syntax validation via Python
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Asks the actual Python engine to read the code and tell us if there are any typos or syntax errors 
        /// before we try executing the full script.
        /// </summary>
        /// <param name="code">The Python code string to inspect.</param>
        /// <param name="errors">A list to append any syntax errors returned by Python.</param>
        internal async Task CheckSyntaxAsync(string code, List<string> errors)
        {
            // Write code to a temp file, then run: python -c "import ast; ast.parse(open('file').read())"
            var tempFile = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tempFile, code, new UTF8Encoding(false));

                var pyTempPath  = tempFile.Replace("\\", "/");
                var checkScript = $"import ast; ast.parse(open(r'{pyTempPath}', encoding='utf-8').read())";

                var psi = new ProcessStartInfo
                {
                    FileName               = _pythonPath,
                    Arguments              = $"-c \"{checkScript}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = new Process { StartInfo = psi };
                var stderr        = new StringBuilder();
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

                process.Start();
                process.BeginErrorReadLine();

                using var cts = new CancellationTokenSource(10_000);
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    errors.Add("Syntax check timed out.");
                    return;
                }

                if (process.ExitCode != 0)
                {
                    var errMsg = stderr.ToString().Trim();
                    errors.Add($"Python syntax error: {errMsg}");
                }
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    /// <summary>
    /// A simple pass/fail report telling us if the Python code looks good to go or if it has typos.
    /// </summary>
    internal sealed record ValidationResult(bool IsValid,List<string> Errors);
}
