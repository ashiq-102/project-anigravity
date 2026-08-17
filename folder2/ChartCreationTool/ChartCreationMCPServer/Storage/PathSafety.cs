using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace ChartCreationMCPServer.Storage
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
        /// <summary>
        /// Normalises a team name into a safe blob-path prefix segment.
        /// Lowercases, collapses non-alphanumeric runs into single hyphens, trims stray
        /// hyphens, and falls back to "default" when empty. e.g. "Team Alpha!" -> "team-alpha".
        /// </summary>
        /// <param name="teamName">The raw team name from the request header.</param>
        /// <returns>A safe, lowercase prefix segment — never empty.</returns>
        public static string SanitizeTeam(string? teamName)
        {
            if (string.IsNullOrWhiteSpace(teamName))
                return "default";

            var lowered = teamName.Trim().ToLowerInvariant();
            var builder = new StringBuilder(lowered.Length);
            var lastHyphen = false;

            foreach (var ch in lowered)
            {
                if (char.IsLetterOrDigit(ch)) { builder.Append(ch); lastHyphen = false; }
                else if (!lastHyphen) { builder.Append('-'); lastHyphen = true; }
            }

            var result = builder.ToString().Trim('-');
            return result.Length == 0 ? "default" : result;
        }
    }
}
