namespace ChartCreationMCPServer.Execution
{
    /// <summary>
    /// Shared formatting utilities used across the application.
    /// </summary>
    internal static class FormatUtils
    {
        /// <summary>
        /// Turns raw byte numbers into something easier to read, like KB or MB.
        /// </summary>
        /// <param name="bytes">The number of bytes.</param>
        /// <returns>A formatted string representing the size.</returns>
        public static string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order  = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
