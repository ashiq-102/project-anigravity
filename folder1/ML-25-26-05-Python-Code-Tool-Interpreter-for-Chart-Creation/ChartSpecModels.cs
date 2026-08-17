namespace ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation
{
    /// <summary>
    /// Holds the raw Python code the AI wrote, along with where we saved it and where the final picture will pop out.
    /// </summary>
    internal sealed record GeneratedScript(
        string Code,
        string ScriptPath,
        string ImagePath);

    /// <summary>
    /// A simple pass/fail report telling us if the Python code looks good to go or if it has typos.
    /// </summary>
    internal sealed record ValidationResult(
        bool IsValid,
        List<string> Errors);

    /// <summary>
    /// A single line in our mistake notebook. It records exactly what the AI did wrong and when.
    /// </summary>
    internal sealed class ErrorEntry
    {
        public string Error { get; set; } = "";
        public string CodeSnippet { get; set; } = "";
        public string Fix { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public ErrorCategory Category { get; set; } = ErrorCategory.Unknown;
    }
}
