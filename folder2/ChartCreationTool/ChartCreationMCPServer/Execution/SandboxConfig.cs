namespace ChartCreationMCPServer.Execution
{
    /// <summary>
    /// The strict safety rules for our Python sandbox. 
    /// We use this to stop the AI's code from using too much memory, running forever, or touching the internet.
    /// </summary>
    internal sealed class SandboxConfig
    {
        /// <summary>
        /// The absolute maximum amount of time we'll let the script run before we pull the plug (usually 30 seconds).
        /// </summary>
        public int TimeoutMs { get; set; } = 30_000;

        /// <summary>
        /// Exactly how much RAM the script is allowed to eat before we stop it (usually 2 GB).
        /// </summary>
        public long MaxMemoryBytes { get; set; } = 2L * 1024 * 1024 * 1024;

        /// <summary>
        /// The temporary, isolated folder where we force the script to run so it doesn't mess with our main files.
        /// </summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>
        /// A switch to completely turn off the internet for the script. Safety first!
        /// </summary>
        public bool BlockNetwork { get; set; } = true;

        /// <summary>
        /// Creates a standard set of safety rules that we use most of the time.
        /// </summary>
        /// <returns>A new standard SandboxConfig.</returns>
        public static SandboxConfig Default() => new();

        /// <summary>
        /// Gives the script a little extra time before timing out if we notice it has to read a massive data file.
        /// </summary>
        /// <param name="fileSizeBytes">The exact size of the file it needs to read.</param>
        /// <returns>A new SandboxConfig with an extended timeout.</returns>
        public static SandboxConfig ForFileSize(long fileSizeBytes)
        {
            // Base 30s + 1s per 100MB
            var extraTime = (int)(fileSizeBytes / (100 * 1024 * 1024)) * 1000;
            var timeout   = Math.Min(30_000 + extraTime, 300_000); // Cap at 5 minutes

            return new SandboxConfig
            {
                TimeoutMs = timeout
            };
        }
    }
}
