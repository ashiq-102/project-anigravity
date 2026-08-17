using System.Text;

namespace ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation
{
    /// <summary>
    /// Column type enumeration for dataset analysis.
    /// </summary>
    internal enum ColumnType
    {
        Integer,
        Float,
        String,
        DateTime,
        Boolean,
        Unknown
    }

    /// <summary>
    /// Metadata about a dataset column.
    /// </summary>
    internal sealed record ColumnInfo(
        string Name,
        ColumnType Type,
        int UniqueValueCount,
        List<string> SampleValues,
        string? MinValue = null,
        string? MaxValue = null);

    /// <summary>
    /// Complete metadata about a dataset file.
    /// </summary>
    internal sealed record DatasetMetadata(
        string FilePath,
        long FileSizeBytes,
        int RowCount,
        int ColumnCount,
        List<ColumnInfo> Columns,
        string FileType,
        bool IsLargeFile);

    /// <summary>
    /// Looks at data files closely to figure out what kind of data is inside them (like text, numbers, or dates).
    /// If you give it a huge file, it's smart enough to only check a few small parts to avoid freezing the computer.
    /// </summary>
    internal sealed class DatasetAnalyzer
    {
        private const long LargeFileThreshold = 100 * 1024 * 1024; // 100 MB
        private const int MaxPreviewRows = 20;
        private const int MaxSampleValues = 10;

        /// <summary>
        /// Reads a file and sends back a helpful summary showing how many rows it has and what the columns look like.
        /// </summary>
        /// <param name="filePath">The absolute path to the data file.</param>
        /// <returns>A DatasetMetadata object containing the summary data.</returns>
        public static async Task<DatasetMetadata> AnalyzeAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found.", filePath);

            var fileInfo = new FileInfo(filePath);
            var fileType = GetFileType(filePath);
            var isLarge = fileInfo.Length > LargeFileThreshold;

            // For CSV files, perform detailed analysis
            if (fileType == "CSV")
                return await AnalyzeCsvAsync(filePath, fileInfo, isLarge);

            // For other types, return basic info
            return new DatasetMetadata(
                FilePath: filePath,
                FileSizeBytes: fileInfo.Length,
                RowCount: 0,
                ColumnCount: 0,
                Columns: new List<ColumnInfo>(),
                FileType: fileType,
                IsLargeFile: isLarge);
        }

        /// <summary>
        /// Opens up a CSV file and grabs small bites of data to figure out the file structure.
        /// </summary>
        /// <param name="filePath">The exact file path to the CSV file.</param>
        /// <param name="fileInfo">Details about the file, like its size.</param>
        /// <param name="isLarge">A flag that tells us if this file is so huge we need to be careful.</param>
        /// <returns>A summary of the CSV's contents.</returns>
        private static async Task<DatasetMetadata> AnalyzeCsvAsync(string filePath, FileInfo fileInfo, bool isLarge)
        {
            var lines = new List<string>();
            var rowCount = 0;

            // Read file with sampling strategy
            using (var reader = new StreamReader(filePath))
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    rowCount++;

                    // Always keep header + first 5 rows
                    if (rowCount <= 6)
                    {
                        lines.Add(line);
                    }
                    // For large files, sample every Nth row
                    else if (isLarge && rowCount % 100 == 0 && lines.Count < MaxPreviewRows)
                    {
                        lines.Add(line);
                    }
                    // For small files, keep more rows
                    else if (!isLarge && lines.Count < MaxPreviewRows)
                    {
                        lines.Add(line);
                    }

                    // Stop reading after reasonable sample
                    if (lines.Count >= MaxPreviewRows && rowCount > 1000)
                        break;
                }

                // Count remaining rows if we stopped early
                if (!reader.EndOfStream)
                {
                    while ((line = await reader.ReadLineAsync()) != null)
                        rowCount++;
                }
            }

            if (lines.Count == 0)
            {
                return new DatasetMetadata(
                    FilePath: filePath,
                    FileSizeBytes: fileInfo.Length,
                    RowCount: 0,
                    ColumnCount: 0,
                    Columns: new List<ColumnInfo>(),
                    FileType: "CSV",
                    IsLargeFile: isLarge);
            }

            // Parse header
            var header = ParseCsvLine(lines[0]);
            var columnCount = header.Count;

            // Parse data rows
            var dataRows = lines.Skip(1)
                .Select(ParseCsvLine)
                .Where(row => row.Count == columnCount)
                .ToList();

            // Analyze each column
            var columns = new List<ColumnInfo>();
            for (int i = 0; i < columnCount; i++)
            {
                var columnName = header[i];
                var values = dataRows.Select(row => i < row.Count ? row[i] : "").ToList();

                var columnInfo = AnalyzeColumn(columnName, values);
                columns.Add(columnInfo);
            }

            return new DatasetMetadata(
                FilePath: filePath,
                FileSizeBytes: fileInfo.Length,
                RowCount: rowCount - 1, // Exclude header
                ColumnCount: columnCount,
                Columns: columns,
                FileType: "CSV",
                IsLargeFile: isLarge);
        }

        /// <summary>
        /// Scans a single column of data to guess if it's dates, numbers, or normal words. 
        /// Also finds the biggest and smallest numbers if it's a number column!
        /// </summary>
        /// <param name="name">The name of the column.</param>
        /// <param name="values">A list of all the data values in that column.</param>
        /// <returns>A summary describing just this one column.</returns>
        private static ColumnInfo AnalyzeColumn(string name, List<string> values)
        {
            var nonEmpty = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            if (nonEmpty.Count == 0)
            {
                return new ColumnInfo(
                    Name: name,
                    Type: ColumnType.Unknown,
                    UniqueValueCount: 0,
                    SampleValues: new List<string>());
            }

            var uniqueValues = nonEmpty.Distinct().ToList();
            var sampleValues = uniqueValues.Take(MaxSampleValues).ToList();

            // Infer type
            var type = InferColumnType(nonEmpty);

            // Get min/max for numeric types
            string? minValue = null;
            string? maxValue = null;

            if (type == ColumnType.Integer || type == ColumnType.Float)
            {
                var numericValues = nonEmpty
                    .Select(v => double.TryParse(v, out var d) ? (double?)d : null)
                    .Where(v => v.HasValue)
                    .Select(v => v!.Value)
                    .ToList();

                if (numericValues.Any())
                {
                    minValue = numericValues.Min().ToString("F2");
                    maxValue = numericValues.Max().ToString("F2");
                }
            }
            else if (type == ColumnType.DateTime)
            {
                var dateValues = nonEmpty
                    .Select(v => DateTime.TryParse(v, out var dt) ? (DateTime?)dt : null)
                    .Where(v => v.HasValue)
                    .Select(v => v!.Value)
                    .ToList();

                if (dateValues.Any())
                {
                    minValue = dateValues.Min().ToString("yyyy-MM-dd");
                    maxValue = dateValues.Max().ToString("yyyy-MM-dd");
                }
            }

            return new ColumnInfo(
                Name: name,
                Type: type,
                UniqueValueCount: uniqueValues.Count,
                SampleValues: sampleValues,
                MinValue: minValue,
                MaxValue: maxValue);
        }

        /// <summary>
        /// Makes an educated guess about a column's data type (like guessing it's a date if 80% of the contents look like a calendar date).
        /// </summary>
        /// <param name="values">A bunch of sample values from the column.</param>
        /// <returns>The data type we guessed.</returns>
        private static ColumnType InferColumnType(List<string> values)
        {
            var sampleSize = Math.Min(values.Count, 50);
            var sample = values.Take(sampleSize).ToList();

            // Check if all are integers
            var intCount = sample.Count(v => int.TryParse(v, out _));
            if (intCount == sample.Count)
                return ColumnType.Integer;

            // Check if all are floats
            var floatCount = sample.Count(v => double.TryParse(v, out _));
            if (floatCount == sample.Count)
                return ColumnType.Float;

            // Check if all are booleans
            var boolCount = sample.Count(v => bool.TryParse(v, out _) ||
                v.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                v.Equals("1", StringComparison.OrdinalIgnoreCase));
            if (boolCount == sample.Count)
                return ColumnType.Boolean;

            // Check if all are dates
            var dateCount = sample.Count(v => DateTime.TryParse(v, out _));
            if (dateCount >= sample.Count * 0.8) // 80% threshold for dates
                return ColumnType.DateTime;

            // Default to string
            return ColumnType.String;
        }

        /// <summary>
        /// An easy way to split a line of text by commas, while being careful not to split words that have commas inside quotes.
        /// </summary>
        /// <param name="line">The single line of text from the CSV.</param>
        /// <returns>A list of the separated values.</returns>
        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString().Trim());
            return result;
        }

        /// <summary>
        /// Checks the end of the file name (like .csv or .txt) and gives us a nice recognizable label.
        /// </summary>
        /// <param name="filePath">The file path to check.</param>
        /// <returns>A simple text label for the file type.</returns>
        private static string GetFileType(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".csv" => "CSV",
                ".xlsx" or ".xls" => "Excel",
                ".tsv" or ".tab" => "TSV",
                ".txt" => "Text",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Takes all the file data we figured out and writes it out nicely so the AI can easily read and understand it.
        /// </summary>
        /// <param name="metadata">The raw file statistics we gathered.</param>
        /// <returns>A nicely formatted summary document.</returns>
        public static string FormatMetadata(DatasetMetadata metadata)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"File: {Path.GetFileName(metadata.FilePath)} ({FormatFileSize(metadata.FileSizeBytes)}, {metadata.RowCount:N0} rows, {metadata.ColumnCount} columns)");

            if (metadata.IsLargeFile)
            {
                sb.AppendLine("⚠️  LARGE FILE: Consider using sampling strategies (pd.read_csv with nrows or chunksize parameters)");
            }

            if (metadata.Columns.Any())
            {
                sb.AppendLine("\nColumns:");
                foreach (var col in metadata.Columns)
                {
                    sb.Append($"  - {col.Name} ({col.Type.ToString().ToLower()})");

                    if (col.UniqueValueCount > 0)
                    {
                        sb.Append($" - {col.UniqueValueCount} unique values");
                    }

                    if (col.MinValue != null && col.MaxValue != null)
                    {
                        sb.Append($" - range: {col.MinValue} to {col.MaxValue}");
                    }

                    if (col.SampleValues.Any() && col.Type == ColumnType.String && col.UniqueValueCount <= 20)
                    {
                        sb.Append($" - values: [{string.Join(", ", col.SampleValues.Take(5))}]");
                    }

                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats file size in human-readable format.
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
