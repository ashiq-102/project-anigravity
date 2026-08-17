using Microsoft.Extensions.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation.Storage;
using OpenAI;

namespace ML_25_26_05_Python_Code_Tool_Interpreter_for_Chart_Creation
{
    /// <summary>
    /// The starting line for the entire application. It sets up the AI and gets everything running.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// It boots up the AI agent, plugs in all our tools, and listens for the user's commands.
        /// </summary>
        /// <param name="args">Any extra settings passed in from the command line.</param>
        static async Task Main(string[] args)
        {
            // ── 1. Load configuration from MySettings.json ────────────────────────
            var builder = new ConfigurationBuilder();
            builder.SetBasePath(AppContext.BaseDirectory);
            builder.AddJsonFile("MySettings.json", optional: false, reloadOnChange: true);
            builder.AddCommandLine(args);
            var config = builder.Build();

            var apiKey = config["OPENAI_API_KEY"];
            var model = config["OPENAI_CHATCOMPLETION_DEPLOYMENT"] ?? "gpt-4o";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("ERROR: OPENAI_API_KEY not found in MySettings.json");
                return;
            }

            // ── 1b. Detect and validate Python installation ────────────────────
            PythonInfo pythonInfo;
            try
            {
                pythonInfo = await PythonPathResolver.ResolveAsync();
                PythonPathResolver.PrintPythonInfo(pythonInfo);

                if (!pythonInfo.RequiredPackagesInstalled)
                {
                    Console.WriteLine("ERROR: Required Python packages are missing. Please install them first.");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                return;
            }

            // ── 2. Wire up dependencies ──────────────────────────────────────
            var storeRoot = Path.Combine(AppContext.BaseDirectory, "input_store");
            var outputRoot = Path.Combine(AppContext.BaseDirectory, "output");

            var store = new LocalFileStore(storeRoot);
            var storageTool = new StorageTool(store);
            var validator = new PythonCodeValidator(pythonInfo.ExecutablePath);
            var executor = new PythonExecutor(pythonInfo.ExecutablePath);
            var errorStore = new ErrorMappingStore(Path.Combine(outputRoot, "error_memory.json"));
            var manifest = new ChartManifest(outputRoot);

            // ── 3. Create the ChartPlugin and extract its AI tools ────────────
            var plugin = new ChartPlugin(storageTool, validator, executor, errorStore, manifest, outputRoot);
            var tools = plugin.GetTools();

            // ── 4. Load past error context for prompt injection ──────────────
            var errorContext = await errorStore.GetErrorContextForPromptAsync(maxCount: 5);

            // ── 5. Build the IChatClient ─────────────────────────────────────
            var openAiClient = new OpenAIClient(apiKey);
            IChatClient chatClient = openAiClient
                .GetChatClient(model)
                .AsIChatClient()
                .AsBuilder()
                .UseFunctionInvocation()
                .Build();

            // ── 6. Create the Microsoft Agents ChatClientAgent ────────────────
            var instructions = $"""
                You are a chart-creation assistant. You help users create data visualizations
                by writing Python matplotlib code yourself.

                You have access to these tools:
                  - UploadFileAsync: Upload data files into the store under a reference name.
                  - ListFiles / DeleteFiles: List or delete stored files.
                  - PreviewFileAsync: Preview file contents to inspect column names and data.
                  - ResolveFilePath: Get the absolute disk path for a stored file reference.
                  - ListGeneratedCharts: Show all charts created in this session with metadata.
                  - GenerateAndRunChart: Submit your Python code for validation and execution.

                When the user asks for a chart involving a data file:
                  1. If you are given an absolute file path (e.g. C:\...), you MUST first call UploadFileAsync(path, referenceName) to securely copy it into the app's internal sandbox.
                  2. NEVER write Python code that reads directly from a user's raw C:\ path.
                  3. Once uploaded, call ResolveFilePath(referenceName) to get the safe internal sandbox path.
                  4. Write COMPLETE Python matplotlib code using the safe sandbox path. (Use PreviewFileAsync first if you need to know the column names).
                  5. Call GenerateAndRunChart with your code and a chart ID.

                Python code rules:
                  - Always start with:
                      import matplotlib
                      matplotlib.use('Agg')
                      import matplotlib.pyplot as plt
                  - For file data, use pandas: import pandas as pd
                  - For Excel files (.xlsx/.xls): pd.read_excel(path)
                  - For TSV files: pd.read_csv(path, sep='\t')
                  - For .txt files: pd.read_csv(path, sep=None, engine='python')
                  - ALWAYS save the chart using exactly this literal placeholder string (do NOT invent your own filename): 
                      plt.savefig(r"OUTPUT_PATH", dpi=150, bbox_inches='tight')
                  - NEVER use plt.show()
                  - NEVER import os, subprocess, sys, shutil, or any system modules
                  - ALWAYS use raw strings for Windows file paths (e.g. r"C:\Users\...") to avoid unicode escape errors.
                  - NEVER use exec(), eval(), open(), or __import__()

                Large dataset handling:
                  - PreviewFileAsync shows file size and warns about large files (>100MB)
                  - For large files, use sampling strategies:
                      * pd.read_csv(path, nrows=10000) to load only first N rows
                      * df.sample(n=1000) or df.sample(frac=0.1) to randomly sample
                      * df[::10] to take every Nth row
                  - For very large files (>1M rows), aggregate before plotting:
                      * df.groupby('category')['value'].mean() for averages
                      * df.resample('M').sum() for time series aggregation
                  - Consider using chunksize for incremental processing

                Multiple charts and dashboards:
                  - For multiple visualizations, call GenerateAndRunChart multiple times with different chart IDs
                  - Use meaningful IDs: 'sales_trend', 'regional_breakdown', 'top_products'
                  - All charts are tracked in a manifest accessible via ListGeneratedCharts
                  - Create comprehensive dashboards by generating related visualizations together
                  - Example: "Create a sales analysis dashboard" → generate line chart (trend), bar chart (by region), and pie chart (by category)

                If GenerateAndRunChart returns an error:
                  - Read the error message carefully
                  - Fix your Python code
                  - Call GenerateAndRunChart again with the corrected code
                  - You may retry up to 3 times

                {errorContext}

                Always be concise and helpful.
                """;

            var agent = new ChatClientAgent(
                chatClient: chatClient,
                instructions: instructions,
                name: "ChartAgent",
                tools: tools
            );
            // ── 7. Create a conversation thread ──────────────────────────────
            var thread = agent.GetNewThread();

            // ── 8. Main conversation loop ────────────────────────────────────
            Console.WriteLine("=== Chart Creation Agent (powered by Microsoft Agents) ===");
            Console.WriteLine("Type your request or 'exit' to quit.\n");

            while (true)
            {
                Console.Write("You: ");
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input) || input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
                    break;

                try
                {
                    var response = await agent.RunAsync(
                        message: input,
                        thread: thread
                    );

                    Console.WriteLine($"\nAgent: {response.Text}\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nERROR: {ex.Message}\n");
                }
            }

            Console.WriteLine("Goodbye!");
        }
    }
}