namespace Agent
{
    /// <summary>
    /// Entry point for the Chart Creation Agent.
    ///
    /// This is a thin MCP client application. It contains NO business logic —
    /// no Python execution, no file storage, no validation.
    /// All of that lives in ChartCreationMCPServer.
    ///
    /// To switch connection modes, comment/uncomment the appropriate line below:
    ///   - <see cref="MCP.McpHttpTransport"/>  : connects to a hosted server over HTTP (default)
    ///   - <see cref="MCP.McpStdioTransport"/> : launches the server as a local .exe via STDIO
    /// </summary>
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // HTTP mode (default): connects to the MCP server running as a hosted ASP.NET Core app.
            await MCP.McpHttpTransport.RunAsync();

            // STDIO mode (not working yet): launches ChartCreationMCPServer.exe as a child process.
            // See LocalMcpConnection.cs for the reason and steps required to enable this.
            // await MCP.McpStdioTransport.RunAsync();
        }
    }
}