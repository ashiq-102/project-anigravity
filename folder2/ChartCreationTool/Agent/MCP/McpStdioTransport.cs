using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI;

namespace Agent.MCP
{
    /// <summary>
    /// Connects the agent to ChartCreationMCPServer over STDIO transport.
    ///
    /// This class is kept for future use but WILL NOT WORK with the current
    /// ChartCreationMCPServer implementation. See the reason and required steps below.
    ///
    /// WHY IT DOES NOT WORK CURRENTLY:
    ///   The current ChartCreationMCPServer is an ASP.NET Core HTTP server — it starts
    ///   Kestrel, binds to a port, and expects HTTP connections. When launched as a child
    ///   process via STDIO, it still tries to start the full HTTP host and crashes immediately
    ///   because it has no stdin/stdout MCP transport configured and cannot find its
    ///   appsettings.json in the working directory the agent uses to launch it.
    ///   The agent receives exit code 3762504530 (0xE0434352 — .NET unhandled exception),
    ///   which surfaces as a ClientTransportClosedException.
    ///
    /// WHAT NEEDS TO BE DONE TO MAKE IT WORK:
    ///   1. Create a separate console app project (e.g. ChartCreationMCPServer.Stdio) that
    ///      uses WithStdioServerTransport() instead of WithHttpTransport() in its Program.cs.
    ///   2. Register the same tools and services as the HTTP server but host them over STDIO.
    ///   3. Build that project and point <c>serverExecutablePath</c> below to its .exe output.
    ///   4. Switch Program.cs to call LocalMcpConnection.RunAsync() instead of HttpMcpConnection.
    ///
    /// CURRENT DEFAULT:
    ///   Use <see cref="MCP.McpHttpTransport"/> — start ChartCreationMCPServer manually first,
    ///   then connect via HTTP. That is the correct and working mode for this architecture.
    /// </summary>
    internal class McpStdioTransport
    {
        /// <summary>
        /// Reads configuration, launches the MCP server executable as a child process,
        /// connects via STDIO, discovers all registered tools, builds the GPT-4o agent,
        /// and starts the conversation loop.
        ///
        /// Update <c>serverExecutablePath</c> to point to your local build output before use.
        /// </summary>
        public static async Task RunAsync()
        {
            // ── Read configuration from appsettings.json ──────────────────────
            Helpers.GetConfig(out string apiKey, out string model, out _, out _);

            // Path to the ChartCreationMCPServer executable.
            // Update this to match your local build output path.
            const string serverExecutablePath =
                @"C:\se-cloud-2025-2026\Source\ML-25-26-05-Python-Code-Tool-Interpreter-for-Chart-Creation_CC\ChartCreationTool\ChartCreationMCPServer\bin\Debug\net10.0\ChartCreationMCPServer.exe"
;

            // ── Launch MCP server and connect via STDIO ───────────────────────
            // StdioClientTransport spawns the server as a child process and communicates
            // through stdin/stdout pipes using the MCP protocol.
            // 'await using' ensures the child process is terminated when RunAsync() exits.
            Console.WriteLine($"Launching local MCP server: {serverExecutablePath}...");

            await using var mcpClient = await McpClient.CreateAsync(new StdioClientTransport(new()
            {
                Name = "ChartCreationMCPServer",
                Command = serverExecutablePath,
                Arguments = [],
            }));

            Console.WriteLine("Connected to local MCP Server via STDIO.");

            // ── Discover tools registered by the server ───────────────────────
            // The server exposes tools via [McpServerTool] attributes on ChartPlugin.
            // ListToolsAsync fetches their names, descriptions, and parameter schemas.
            var tools = await mcpClient.ListToolsAsync();

            Console.WriteLine($"Discovered {tools.Count} tools from local MCP server.");

            List<AITool> aiTools = [.. tools.Cast<AITool>()];

            // ── Build the AI agent with the discovered tools ──────────────────
            // AsAIAgent() wires the system instructions and tool list into GPT-4o
            // so the agent can call MCP tools automatically during the conversation.
            AIAgent agent = new OpenAIClient(apiKey)
                .GetChatClient(model)
                .AsIChatClient()
                .AsAIAgent(
                    instructions: Helpers.Instructions,
                    tools: aiTools);

            // ── Start the conversation loop ───────────────────────────────────
            await Helpers.RunConversationLoopAsync(agent);
        }
    }
}