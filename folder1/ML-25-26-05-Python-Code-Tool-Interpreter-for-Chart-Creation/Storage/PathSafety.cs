using System;
using System.Collections.Generic;
using System.Text;

namespace ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation.Storage
{
    /// <summary>
    /// A small helper class to make sure file names don't do anything sketchy.
    /// We use this everywhere we handle user-provided file names to prevent attacks like directory traversal (e.g., trying to read "../../../windows/system32").
    /// </summary>
    internal static class PathSafety
    {
        /// <summary>
        /// Cleans up a file reference name by stripping out bad characters and making sure it doesn't try to traverse folders.
        /// </summary>
        /// <param name="name">The raw, potentially unsafe name coming from the user or the AI.</param>
        /// <returns>A completely safe version of the string that only contains letters, numbers, dashes, underscores, or simple dots.</returns>
        public static string SanitizeReference(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0)
                throw new ArgumentException("Empty reference name.");

            // Keep it simple and safe: letters/digits/_-.
            var safe = new string(name.Select(ch =>
                char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.'
                    ? ch
                    : '_'
            ).ToArray());

            // Prevent traversal patterns
            safe = safe.Replace("..", "_");
            return safe;
        }
    }
}
