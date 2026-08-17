using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI;

namespace Agent.MCP
{
    /// <summary>
    /// Connects the agent to ChartCreationMCPServer over HTTP transport.
    /// Communication happens over HTTP using the Model Context Protocol (MCP) standard.
    /// </summary>
    internal class McpHttpTransport
    {
        /// <summary>
        /// Reads configuration, connects to the MCP server over HTTP, discovers all
        /// registered tools, builds the GPT-4o agent, and starts the conversation loop.
        /// The MCP client is disposed automatically when the session ends via
        /// <c>await using</c>, ensuring the HTTP connection is cleanly closed.
        /// </summary>
        public static async Task RunAsync()
        {
            // ── Read configuration from appsettings.json ──────────────────────
            Helpers.GetConfig(out string apiKey, out string model, out string serverUrl, out string teamName);

            // ── Connect to the MCP server over HTTP ───────────────────────────
            // HttpClientTransport implements the MCP protocol over standard HTTP.
            // McpClient.CreateAsync performs the MCP handshake and returns a ready-to-use client.
            // 'await using' ensures the connection is disposed when RunAsync() exits.
            Console.WriteLine($"Connecting to MCP server at {serverUrl}...");

            await using var mcpClient = await McpClient.CreateAsync(new HttpClientTransport(new()
            {
                Name = "ChartCreationMCPServer",
                Endpoint = new Uri(serverUrl),
                AdditionalHeaders = new Dictionary<string, string> { ["Team-Name"] = teamName }
            }));

            Console.WriteLine("Connected to MCP Server.");

            // ── Discover tools registered by the server ───────────────────────
            // The server exposes tools via [McpServerTool] attributes on ChartPlugin.
            // ListToolsAsync fetches their names, descriptions, and parameter schemas.
            var tools = await mcpClient.ListToolsAsync();

            Console.WriteLine($"Discovered {tools.Count} tools from MCP server.");

            //foreach (var t in tools)
            //    Console.WriteLine($"  - '{t.Name}'");

            List<AITool> aiTools = [.. tools.Where(t => t.Name != "upload_file").Cast<AITool>()];
            aiTools.Add(Helpers.CreateUploadFileTool(mcpClient));

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