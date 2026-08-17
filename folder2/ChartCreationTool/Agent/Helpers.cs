using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Agent
{
    /// <summary>
    /// Shared helper methods used by all MCP connection classes.
    /// Centralises two concerns that would otherwise be duplicated across
    /// <see cref="MCP.McpHttpTransport"/> and <see cref="MCP.McpStdioTransport"/>:
    ///   - Reading and validating configuration from appsettings.json
    ///   - Running the interactive conversation loop with an <see cref="AIAgent"/>
    /// </summary>
    public static class Helpers
    {

        /// <summary>
        /// Reads the OpenAI API key, model name, and MCP server URL from appsettings.json.
        /// Environment variables can override any value at runtime, which is useful
        /// for production deployments and CI pipelines.
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="model"></param>
        /// <param name="serverUrl"></param>
        /// <param name="teamName"></param>
        /// Throws <see cref="InvalidOperationException"/> if the API key or server URL
        /// is missing, so the application fails fast with a clear message rather than
        /// surfacing a null reference later.
        public static void GetConfig(out string apiKey, out string model, out string serverUrl, out string teamName)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            apiKey = config["OPENAI_API_KEY"]
                ?? throw new InvalidOperationException("OPENAI_API_KEY is not set in appsettings.json.");
         
            model = config["OPENAI_CHATCOMPLETION_DEPLOYMENT"] ?? "gpt-4o";

            serverUrl = config["McpServer:Url"]
                ?? throw new InvalidOperationException("McpServer:Url is not set in appsettings.json.");

            teamName = config["TeamName"] ?? "default";
        }

        /// <summary>
        /// Builds the file-upload tool given to the model.
        ///
        /// The server's UploadFileAsync MCP tool now expects the file's actual Base64-encoded
        /// content, not a path — the model can't supply that itself, since it never reads local
        /// files. So this is what the model actually calls when the user says "upload this file":
        /// it still looks like UploadFileAsync(filePath) to the model (same name, same single
        /// parameter, so the system prompt needs no changes), but it runs on the Agent's own
        /// machine, reads the file from local disk, Base64-encodes it, and forwards the content
        /// to the real server tool via the already-connected MCP client — bypassing the model
        /// for that specific hand-off, since the model has no way to perform it itself.
        ///
        /// The raw server tool is excluded from the tool list handed to the model (see
        /// McpHttpTransport.cs), which only ever gives the model this version instead.
        /// </summary>
        /// <param name="mcpClient">The connected MCP client used to forward the encoded file to the server.</param>
        /// <returns>An AIFunction named "UploadFileAsync" that the model can call with a local file path.</returns>
        public static AIFunction CreateUploadFileTool(McpClient mcpClient) =>
            AIFunctionFactory.Create(
                (string filePath) => UploadFileAsync(mcpClient, filePath),
                name: "UploadFileAsync",
                description: "Uploads a local data file (CSV, Excel, etc.) so it can be used to generate " +
                             "charts. Call this with the exact absolute file path the user gave you.");

        /// <summary>
        /// Reads a local file, Base64-encodes it, and forwards it to the server's real
        /// UploadFileAsync tool. Runs entirely client-side — the model never sees the file's
        /// content, only the plain-text result returned at the end.
        /// </summary>
        /// <param name="mcpClient">The connected MCP client used to call the server's tool.</param>
        /// <param name="filePath">Absolute path to the file on the Agent's own machine, as given by the model.</param>
        /// <returns>
        /// The server's own "OK: Uploaded..." confirmation text (including the reference name) on
        /// success, or an "ERROR: ..." message describing what went wrong — checked locally first
        /// (missing file, unreadable file) before anything is sent to the server, then checked
        /// again against the server's own response (upload/storage failure).
        /// </returns>

        private const long MaxUploadBytes = 50 * 1024 * 1024; // 50MB — max dataset sizes
        private static async Task<string> UploadFileAsync(McpClient mcpClient, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return "ERROR: filePath is required.";

         
            if (!File.Exists(filePath))
                return $"ERROR: File not found on your machine: '{filePath}'. Check the path and try again.";

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxUploadBytes)
                return $"ERROR: File is {fileInfo.Length / 1024.0 / 1024.0:F1}MB, which exceeds the {MaxUploadBytes / 1024 / 1024}MB limit for this upload method.";

            byte[] bytes;
            try
            {
                bytes = await File.ReadAllBytesAsync(filePath);
            }
            catch (Exception ex)
            {
                return $"ERROR: Could not read '{filePath}': {ex.Message}";
            }

            var fileName = Path.GetFileName(filePath);
            var base64 = Convert.ToBase64String(bytes);

            // Calls the server's real "upload_file" tool directly — bypassing the model, since
            // it has no way to produce fileContentBase64 itself.
            var result = await mcpClient.CallToolAsync(
                "upload_file",
                new Dictionary<string, object?>
                {
                    ["fileName"] = fileName,
                    ["fileContentBase64"] = base64
                });

            // result.Content.OfType<TextContentBlock>() is the documented way to read a tool
            // result in ModelContextProtocol; result.IsError flags a server-side failure
            // (e.g. invalid Base64, or the Azure Blob Storage upload itself failing).
            var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;

            if (result.IsError == true)
                return $"ERROR: Upload failed on the server: {text ?? "no details returned."}";

            return text ?? "ERROR: No response text from server.";
        }

        /// <summary>
        /// The system prompt that controls the agent's behaviour — workflow steps,
        /// Python code rules, retry logic, and tone.
        ///
        /// Lives here in the agent, not on the MCP server, because the agent owns
        /// how the AI behaves and communicates with the user. The server only exposes
        /// tools; it has no opinion on how they are used.
        ///
        /// The tool list is intentionally omitted — the agent discovers available
        /// tools automatically from the MCP server at runtime via ListToolsAsync().
        /// </summary>
        public static string Instructions => """
            You are a chart-creation assistant. You help users create data visualizations
            by writing Python matplotlib code yourself and running it with GenerateChart.

            If the user gives the data directly in the message (e.g. "bar chart of 10, 40, 500"
            or "pie chart: North 1200, South 4500"), put those values straight into your Python
            code and call GenerateChart. Do not ask for a file.

            When the user asks for a chart from a data file:
              1. If given an absolute file path (e.g. C:\...), call UploadFileAsync(filePath) first.
                 The server stores it in Azure Blob Storage and returns a reference name.
              2. Call ResolveFilePath(referenceName) to get the local path Python should read.
              3. Preview the file with PreviewUploadedFile if you need to know the column names.
              4. Write COMPLETE Python matplotlib code using the resolved path.
              5. Call GenerateChart with your code and a meaningful chart ID.
              6. When successful, show the user the View/Download URL returned by the server.

            Python code rules:
              - Always start with:
                  import matplotlib
                  matplotlib.use('Agg')
                  import matplotlib.pyplot as plt
              - For file data, use pandas: import pandas as pd
              - For Excel files (.xlsx): pd.read_excel(path)
              - The legacy .xls format is not supported — if the user has one, ask them to save it as .xlsx
              - For TSV files: pd.read_csv(path, sep='\t')
              - For .txt files: pd.read_csv(path, sep=None, engine='python')
              - ALWAYS save the chart using exactly this literal placeholder string:
                  plt.savefig(r"OUTPUT_PATH", dpi=150, bbox_inches='tight')
              - NEVER use plt.show()
              - NEVER import os, subprocess, sys, shutil, or any system modules
              - ALWAYS use raw strings for Windows file paths (e.g. r"C:\Users\...")
              - NEVER use exec(), eval(), open(), or __import__()

            Large dataset handling:
              - For large files, sample rather than loading everything: pd.read_csv(path, nrows=10000),
                df.sample(n=1000), or df.resample('M').sum() for time series.

            Multiple charts:
              - For several charts, call GenerateChart multiple times with distinct, meaningful
                chart IDs like 'sales_trend', 'regional_breakdown', 'top_products'.

            If GenerateChart returns an error:
              - Read the error message carefully, fix your Python code, and call GenerateChart again.
              - You may retry up to 3 times.

            Managing stored files:
              - Use ListUploadedFiles to show which data files are stored, and DeleteUploadedFiles
                to remove them, when the user asks.

            Showing charts you already made:
              - Charts you generate in THIS conversation stay in your memory along with their URLs.
                When the user refers to charts from this conversation — "both charts", "the bar chart
                I made", "give me all of them", "one or two of them" — answer from your memory and
                REUSE those URLs. Do NOT call GenerateChart again, and do NOT call ListGeneratedCharts
                for these.
              - Treat "my charts" / "all the charts" / "the ones I made" as referring to THIS
                conversation by default.
              - Only use ListGeneratedCharts when the user clearly wants charts from BEYOND this
                conversation — "all charts I've ever made", "charts from past sessions", "everything
                my team has stored". It returns every stored chart for the team; if the user wants
                only some, select from the returned list yourself.
              - If it is genuinely unclear whether the user means this conversation or all their
                stored charts, ASK a brief clarifying question before choosing.

            If the user asks you to modify a chart (change color, add a title, resize, etc.), that is
            a NEW chart — write updated Python code and call GenerateChart again. Reuse only applies
            when the user wants to see an existing chart again, not change it.

            Always be concise and helpful. When a chart is ready, always show the URL prominently.
            """;

        /// <summary>
        /// Runs the interactive conversation loop with the provided <see cref="AIAgent"/>.
        ///
        /// Creates a new <see cref="AgentSession"/> to maintain full conversation history
        /// automatically across turns — no manual chat history list is needed.
        /// The loop reads user input, sends it to the agent (which calls MCP tools on the
        /// server as needed), and prints the response. Exits when the user types <c>exit</c>
        /// or sends an empty line.
        /// </summary>
        /// <param name="agent">
        /// The configured <see cref="AIAgent"/> with tools and instructions already applied.
        /// </param>
        public static async Task RunConversationLoopAsync(AIAgent agent)
        {
            AgentSession session = await agent.CreateSessionAsync();

            Console.WriteLine("Conversation started. Type 'exit' to end.");
            Console.WriteLine();

            while (true)
            {
                Console.Write("You: ");
                string userInput = Console.ReadLine() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(userInput) ||
                    userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Goodbye!");
                    break;
                }

                try
                {
                    var response = await agent.RunAsync(userInput, session);
                    Console.WriteLine($"\nAgent: {response}\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nERROR: {ex.Message}\n");
                }
            }
        }
    }
}