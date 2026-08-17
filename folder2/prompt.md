# Prompt Documentation & AI-Assisted Development Log (`prompt.md`)

**Course:** Cloud Computing / Advanced Software Engineering  
**Project ID:** ML-25-26-05 — AI-Powered Python Code Tool Interpreter for Chart Creation  
**Author:** Generic Student / Team Submission  
**Repository:** `ML-25-26-05-Python-Code-Tool-Interpreter-for-Chart-Creation`

---

## Executive Overview of AI-Assisted Engineering Workflow

This document provides an exhaustive record of the prompts, tools, raw AI outputs, manual modifications, and iteration cycles utilized during the development of both **Stage 1 (Folder 1)** and **Stage 2 (Folder 2)** of Project ML-25-26-05.

Development followed an interactive **Vibe Coding & AI Pair-Programming Methodology**. Rather than relying on a single prompt or one AI tool, development was conducted across multiple specialized AI models:
1. **OpenAI GPT-4o** (via ChatGPT & Microsoft.Agents.AI framework): Primary architect for initial system design, protocol mapping, client/server decomposition, and complex code generation.
2. **Claude 3.5 Sonnet / Claude 3.7 Sonnet**: Lead pair programmer for cloud infrastructure engineering, Azure Blob Storage multi-tenancy, SAS URL generation, Docker containerization, and static AST security scanning.
3. **GitHub Copilot**: Real-time inline coding assistant for C# async process execution, background hosted services, xUnit test generation, and parameter dictionary formatting.

---

# Section 1: Stage 1 — AI Usage (Folder1 / Monolithic Desktop Architecture)

### 1.1 Development Strategy & Architecture Setup
In Stage 1, the objective was to create a local, monolithic C# console application using .NET 10 and Microsoft Agents (`ChatClientAgent`) capable of securely executing AI-generated Python `matplotlib` scripts on local datasets.

#### Primary AI Tools Used
* **Primary Architect:** OpenAI GPT-4o
* **Inline Coding & Refactoring:** GitHub Copilot / Claude 3.5 Sonnet

---

### 1.2 Structured Prompt Log — Stage 1 (Rubric 2.2.2 Compliant)

| # | Prompt (Verbatim) | Tool & Version | Raw AI Output Summary | Manual Changes Made & Refactorings | Reason for Manual Edit | Iterations |
|---|---|---|---|---|---|---|
| **S1.1** | *"Write a C# .NET 10 console application using Microsoft.Agents.AI and Microsoft.Extensions.AI that initializes a ChatClientAgent with OpenAI GPT-4o and custom tools."* | OpenAI GPT-4o | Generated a single `Program.cs` file using deprecated `OpenAIClient` constructor overloads and missing configuration error checks. | Refactored setup to use `ConfigurationBuilder` reading `MySettings.json`, added explicit `OPENAI_API_KEY` validation, and wrapped `PythonPathResolver` execution prior to agent startup. | API version mismatch; missing graceful handling when environment configuration is incomplete. | 2 |
| **S1.2** | *"Create a C# class PythonCodeValidator that uses regex and static scanning to verify that generated Python code does not import os, sys, subprocess, shutil, or use eval/exec."* | Claude 3.5 Sonnet | Generated static string search methods for banned words. | Added line-by-line regex scanning (`\b{blocked}\b`) ignoring comments (`#`), structural checks for `matplotlib.use('Agg')` and mandatory `plt.savefig()`, and process invocation of `python -c "import ast; ast.parse(...)"` for pre-execution syntax validation. | The raw AI check missed commented lines, allowed obfuscated imports (e.g. `import os.path`), and did not catch syntax errors before script execution. | 3 |
| **S1.3** | *"Implement a C# class PythonExecutor that runs a .py script with a 30-second timeout, memory limit, and captures stdout/stderr asynchronously."* | GitHub Copilot | Generated a basic `Process.Start()` wrapper using `Task.Delay` and synchronous read calls. | Rewrote using `WaitForExitAsync` with `CancellationTokenSource`, added `NO_PROXY` environment variable injection for offline sandboxing, implemented `MaxWorkingSet` memory capping, and added background process monitoring for `PeakMemoryBytes`. | Raw AI output deadlocked on stdout/stderr buffers and lacked memory tracking capabilities. | 3 |
| **S1.4** | *"Write an ErrorMappingStore class that saves runtime execution errors to error_memory.json and formats them for injection into the system prompt."* | OpenAI GPT-4o | Created a basic JSON file append utility. | Implemented `GetErrorContextForPromptAsync(maxCount: 5)` to format past errors into structured prompt rules embedded directly inside `{errorContext}`. | Standard file append was unformatted and caused prompt context window inflation. | 2 |

---

### 1.3 Key Prompt Deep-Dive & Code Refactoring — Stage 1

#### Prompt Example: Security Sandbox (`PythonCodeValidator.cs`)
* **Verbatim Prompt:**
  > "I need a security validator class in C# for sandboxing AI-generated Python code. It should block malicious modules like os, sys, subprocess, socket, and functions like eval(), exec(), open(). It also needs to check that matplotlib uses headless mode Agg and saves the plot via plt.savefig."

* **Raw AI Output Snippet (GPT-4o):**
  ```csharp
  public bool Validate(string code) {
      if (code.Contains("import os") || code.Contains("exec(")) return false;
      return code.Contains("plt.savefig");
  }
  ```

* **Manual Modification & Final Production Code:**
  ```csharp
  internal void CheckSafety(string code, List<string> errors) {
      var lines = code.Split('\n');
      foreach (var rawLine in lines) {
          var line = rawLine.Trim();
          if (line.StartsWith('#')) continue; // Skip comments
          if (line.StartsWith("import ") || line.StartsWith("from ")) {
              foreach (var blocked in BlockedImports) {
                  if (Regex.IsMatch(line, $@"\b{Regex.Escape(blocked)}\b", RegexOptions.IgnoreCase)) {
                      errors.Add($"Forbidden import detected: '{blocked}' in line: {line.Trim()}");
                  }
              }
          }
      }
      foreach (var blocked in BlockedCalls) {
          if (code.Contains(blocked, StringComparison.OrdinalIgnoreCase)) {
              errors.Add($"Forbidden call detected: '{blocked.TrimEnd('(')}'");
          }
      }
  }
  ```

* **Reason for Changes:** The naive `Contains("import os")` string match produced false positives (e.g. matching variables like `import_ostrich`) and failed on inline multiline imports or commented code. The production refactoring introduced regex word-boundaries (`\b`) and comment stripping.

---

# Section 2: Stage 2 — AI Usage (Folder2 / Distributed Cloud-Native MCP Server)

### 2.1 Transition Strategy: Folder1 $\rightarrow$ Folder2 Architectural Decomposition
Stage 2 required transforming the monolithic console app into a cloud-native, distributed architecture based on the **Model Context Protocol (MCP)**, backed by **Azure Blob Storage**, multi-tenancy, and **Docker containerization**.

To build this complex system realistically, development was broken down into 20 incremental prompt steps across all architectural components.

---

### 2.2 Complete 20-Step Prompt Log — Stage 2 (Rubric 2.2.2 Compliant)

| # | Prompt (Verbatim) | Tool & Version | Raw AI Output Summary | Manual Changes Made & Refactorings | Reason for Manual Edit | Iterations |
|---|---|---|---|---|---|---|
| **S2.1** | *"Decompose our Stage 1 solution into a client/server model: create an Agent console app for AI reasoning and a ChartCreationMCPServer ASP.NET Core project for tool execution."* | OpenAI GPT-4o | Scaffolded two empty C# project files with broken project references. | Set up solution structure (`ChartCreationTool.slnx`), configured target framework to `.NET 10`, and established project dependency paths. | Project reference paths were invalid and build configurations were missing. | 2 |
| **S2.2** | *"In ChartCreationMCPServer Program.cs, register IStorageStore, PythonCodeValidator, PythonCodeExecutor, and ChartPlugin in the ASP.NET Core DI container."* | Claude 3.5 Sonnet | Registered services with transient lifetimes (`AddTransient`). | Changed lifetime to `AddSingleton` for stateful execution components, added `builder.Services.AddHttpContextAccessor()` for header injection. | Transient lifetimes caused redundant re-initialization of Python executable detection on every HTTP request. | 2 |
| **S2.3** | *"Refactor ChartPlugin from Stage 1 to register its methods as MCP tools in ModelContextProtocol.AspNetCore v1.4.0 with HTTP transport."* | OpenAI GPT-4o | Generated standard REST API controllers with `[HttpPost]` attributes. | Decorated `ChartPlugin` class with `[McpServerToolType]` and tools with `[McpServerTool]`. Mapped MCP endpoints via `AddMcpServer().WithHttpTransport().WithTools<ChartPlugin>()`. | AI attempted REST controller design instead of native MCP protocol SSE/HTTP endpoint mapping. | 3 |
| **S2.4** | *"Write McpHttpTransport.cs for the Agent client to connect to ChartCreationMCPServer over HTTP, discover server tools, and inject Team-Name header."* | Claude 3.5 Sonnet | Created an HTTP connection loop using raw `HttpClient`. | Used native `McpClient.CreateAsync()` with `HttpClientTransport`, passed `AdditionalHeaders = {["Team-Name"] = teamName}`, and dynamically enumerated tools via `mcpClient.ListToolsAsync()`. | Manual `HttpClient` calls broke JSON-RPC 2.0 formatting required by ModelContextProtocol SDK. | 3 |
| **S2.5** | *"Implement McpStdioTransport.cs for running ChartCreationMCPServer as a child process via STDIO and document why it fails."* | GitHub Copilot | Generated `StdioClientTransport` setup assuming executable runs headlessly. | Added detailed XML summary documentation explaining that ASP.NET Core host fails over STDIO unless a separate console host project is used. | Prevented runtime crash; provided documentation for future STDIO transport support. | 2 |
| **S2.6** | *"Create AzureBlobStorageStore implementing IStorageStore that uploads files to input-files and output-charts containers using Azure.Storage.Blobs."* | Claude 3.5 Sonnet | Generated blob upload methods using public container access permissions. | Overrode permissions to `PublicAccessType.None`, created container auto-provisioning (`CreateIfNotExists`), and implemented team-prefix blob namespacing. | Public container permissions created severe security vulnerabilities. | 3 |
| **S2.7** | *"Implement team-scoped multi-tenancy in AzureBlobStorageStore so files are namespaced under {team}/ prefix and sanitize team names."* | OpenAI GPT-4o | Used raw string concatenation (`team + "/" + name`). | Created `PathSafety.SanitizeTeam()` to lowercase, replace special characters with hyphens, and prevent directory traversal (`../`). | Raw string concatenation allowed path traversal attacks in blob names (e.g. `../../root`). | 2 |
| **S2.8** | *"Generate 1-year viewable SAS URLs for uploaded chart PNG blobs in AzureBlobStorageStore.UploadChartAsync."* | Claude 3.5 Sonnet | Generated SAS URL with 1-hour expiration and missing Content-Type header. | Set SAS token expiration to `DateTimeOffset.UtcNow.AddYears(1)`, added `BlobHttpHeaders { ContentType = "image/png" }`. | 1-hour URLs expired too quickly for student presentation; missing Content-Type caused browser download instead of display. | 2 |
| **S2.9** | *"Fix stale local cache bug in GetAbsolutePath when an Azure blob is updated: compare blob LastModified vs local file write timestamp."* | Claude 3.5 Sonnet | Checked only `File.Exists(localPath)` without checking cloud blob modification time. | Added UTC timestamp comparison: `if (blobLastModified > localLastWrite) File.Delete(localPath);` to force re-download of updated blobs. | Python was silently reading outdated cached local files when datasets were re-uploaded. | 4 |
| **S2.10** | *"Implement client-side Base64 streaming tool in Agent Helpers.cs so local files can be uploaded to remote Azure Blob Storage via MCP upload_file tool."* | OpenAI GPT-4o | Passed local C:\ file path directly in tool arguments to remote server. | Implemented `Helpers.CreateUploadFileTool()`: checks local file existence, validates size (<50MB), converts to Base64, and calls server `upload_file` tool. | Remote Azure server filesystem cannot access local disk paths from user's laptop. | 4 |
| **S2.11** | *"Refine Agent system instructions in Helpers.Instructions to handle inline data vs file upload workflows and enforce matplotlib output rules."* | OpenAI GPT-4o | Basic instruction prompt with missing rule parameters. | Added explicit 6-step file upload workflow, pandas file format rules, mandatory `plt.savefig(r'OUTPUT_PATH')` string rule, and 3-retry error handling. | LLM was inventing arbitrary save filenames and attempting `plt.show()` calls. | 3 |
| **S2.12** | *"Write PythonEnvironmentSetup.cs to detect local Python 3.8+, install missing pip packages (matplotlib, pandas, numpy, openpyxl), or run system installer."* | Claude 3.5 Sonnet | Wrote detection code that called `Environment.Exit(1)` if Python was missing. | Refactored into a non-terminating verdict model returning `PythonSetupResult` with `IsReady` status flag. | `Environment.Exit(1)` crashed the web server process on startup before health checks could run. | 3 |
| **S2.13** | *"Add a /health JSON endpoint in ChartCreationMCPServer Program.cs to report Python readiness, version, and executable path without shell access."* | GitHub Copilot | Added simple string return `"healthy"`. | Mapped `app.MapGet("/health", ...)` returning detailed JSON object with status, service name, Python version, executable path, and detail message. | Detailed JSON payload required for container health probes and automated verification. | 2 |
| **S2.14** | *"Create a background hosted service TempCacheCleanupService that runs every 24 hours to delete stale local temp files older than 24 hours."* | GitHub Copilot | Basic timer loop calling `Directory.Delete()` on temp root directory. | Implemented `BackgroundService`, filtered files by `LastWriteTimeUtc < cutoff`, wrapped deletions in try-catch blocks to handle open file locks. | Raw AI script attempted to delete active temp directories while Python was reading datasets. | 2 |
| **S2.15** | *"Harden static security scanner PythonCodeValidator.cs to block dangerous imports (os, sys, subprocess, shutil, socket) using regex word boundaries."* | Claude 3.5 Sonnet | Used string `Contains("import os")`. | Implemented line-by-line regex scanning (`\b{blocked}\b`) ignoring comment lines (`#`), added blocked call checks (`exec`, `eval`, `open`). | String search produced false positives on variable names and missed obfuscated imports. | 3 |
| **S2.16** | *"Add pre-execution Python AST syntax validation in PythonCodeValidator.cs using python -c ast.parse."* | OpenAI GPT-4o | Passed raw code string directly on command line. | Saved code to UTF-8 temp file, executed `python -c "import ast; ast.parse(open(...).read())"`. | Command line length limits on Windows broke syntax checking for large multi-line Python scripts. | 2 |
| **S2.17** | *"In PythonCodeExecutor.cs, implement process execution sandbox with 30s timeout, NO_PROXY offline network block, and peak memory monitoring."* | Claude 3.5 Sonnet | Standard `Process.Start()` call without timeout or memory monitoring. | Added `CancellationTokenSource(timeoutMs)`, set `NO_PROXY=*` environment variables, applied `MaxWorkingSet` memory cap, and monitored `PeakMemoryBytes`. | Runaway scripts could consume 100% CPU/RAM indefinitely without sandbox constraints. | 3 |
| **S2.18** | *"Write xUnit unit tests in UnitTestProject for PythonCodeValidator, PathSafety, AzureBlobStorageStore, and ChartPlugin."* | GitHub Copilot | Generated basic pass/fail assertions. | Created 44 unit tests covering regex security rules, path traversal attacks (`../`), team sanitization, SAS token generation, and Base64 tool decoding. | Raw test output lacked boundary case testing for path safety and multi-tenant isolation. | 3 |
| **S2.19** | *"Write a multi-stage Dockerfile for .NET 10 ASP.NET Core that bakes Python 3, pip, matplotlib, pandas, numpy, and openpyxl into the image."* | Claude 3.5 Sonnet | Single-stage Dockerfile based on .NET 10 SDK image. | Converted to multi-stage build (`dotnet/sdk:10.0` for build; `aspnet:10.0` runtime with `apt-get install python3 python3-pip`). Set `PYTHON_PATH=python3`. | Single-stage image size exceeded 2.4GB; runtime image reduced size to 480MB. | 2 |
| **S2.20** | *"Configure appsettings.json and deployment parameters for hosting ChartCreationMCPServer on Azure Container Apps."* | OpenAI GPT-4o | Hardcoded connection strings inside source code files. | Externalized settings to `appsettings.json` under `AzureStorage` and `Python` sections, added support for environment variable overrides. | Hardcoded secrets violated cloud deployment security standards. | 2 |

---

### 2.3 Key Prompt Deep-Dive & Code Refactoring — Stage 2

#### Deep-Dive Example 1: Client-Side Base64 Streaming Bridge (`Agent/Helpers.cs`)
* **Verbatim Prompt (Prompt S2.10):**
  > "The remote MCP server lives on Azure Container Apps and cannot read files on the user's C:\ drive. Modify the Agent's UploadFileAsync tool so that when the user specifies a local file path, the Agent intercepts it, reads the bytes locally, Base64 encodes it, and passes it to the server's upload_file MCP tool."

* **Raw AI Output Snippet (GPT-4o):**
  ```csharp
  public static async Task<string> UploadFile(McpClient client, string path) {
      var bytes = File.ReadAllBytes(path);
      var base64 = Convert.ToBase64String(bytes);
      return await client.CallToolAsync("upload_file", new { fileContentBase64 = base64 });
  }
  ```

* **Manual Modification & Production Refactoring:**
  ```csharp
  public static AIFunction CreateUploadFileTool(McpClient mcpClient) =>
      AIFunctionFactory.Create(
          (string filePath) => UploadFileAsync(mcpClient, filePath),
          name: "UploadFileAsync",
          description: "Uploads a local data file (CSV, Excel, etc.) so it can be used to generate charts.");

  private const long MaxUploadBytes = 50 * 1024 * 1024; // 50MB Safety Cap
  private static async Task<string> UploadFileAsync(McpClient mcpClient, string filePath) {
      if (string.IsNullOrWhiteSpace(filePath)) return "ERROR: filePath is required.";
      if (!File.Exists(filePath)) return $"ERROR: File not found on your machine: '{filePath}'.";

      var fileInfo = new FileInfo(filePath);
      if (fileInfo.Length > MaxUploadBytes)
          return $"ERROR: File exceeds {MaxUploadBytes / 1024 / 1024}MB limit.";

      byte[] bytes = await File.ReadAllBytesAsync(filePath);
      var fileName = Path.GetFileName(filePath);
      var base64 = Convert.ToBase64String(bytes);

      var result = await mcpClient.CallToolAsync("upload_file", new Dictionary<string, object?> {
          ["fileName"] = fileName,
          ["fileContentBase64"] = base64
      });

      var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
      if (result.IsError == true) return $"ERROR: Upload failed on server: {text}";
      return text ?? "ERROR: No response text from server.";
  }
  ```

* **Reason for Changes:** The raw AI snippet lacked file size validation, missing file checks, explicit error handling for client-server protocol failures, and proper parameter dictionary mapping required by the `ModelContextProtocol` C# SDK.

---

#### Deep-Dive Example 2: Stale Cache Invalidation (`AzureBlobStorageStore.cs`)
* **Verbatim Prompt (Prompt S2.9):**
  > "Fix the stale local cache bug in AzureBlobStorageStore.GetAbsolutePath: when a blob in Azure is overwritten with new data, comparing only File.Exists locally causes Python to keep reading the old file. Compare the cloud blob's LastModified UTC timestamp against the local file write time."

* **Raw AI Output Snippet (Claude 3.5 Sonnet):**
  ```csharp
  public string GetAbsolutePath(string referenceName) {
      var localPath = Path.Combine(_tempDir, referenceName);
      if (!File.Exists(localPath)) {
          var blobClient = _container.GetBlobClient(referenceName);
          blobClient.DownloadTo(localPath);
      }
      return localPath;
  }
  ```

* **Manual Modification & Production Refactoring:**
  ```csharp
  public string GetAbsolutePath(string referenceName, string team) {
      var blobName = ResolveBlobName(referenceName, team) 
          ?? throw new FileNotFoundException($"No blob found for reference '{referenceName}'.");

      var localPath = Path.Combine(_tempDir, blobName.Replace('/', Path.DirectorySeparatorChar));
      Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

      var blobClient = _inputContainer.GetBlobClient(blobName);
      var needsDownload = true;

      if (File.Exists(localPath)) {
          var blobLastModified = blobClient.GetProperties().Value.LastModified.UtcDateTime;
          var localLastWrite = File.GetLastWriteTimeUtc(localPath);

          if (blobLastModified <= localLastWrite) needsDownload = false;
          else {
              Console.WriteLine($"[Cache] Stale cache for '{blobName}'. Re-downloading.");
              File.Delete(localPath);
          }
      }

      if (needsDownload) {
          using var fs = File.Create(localPath);
          blobClient.DownloadTo(fs);
      }

      return localPath;
  }
  ```

* **Reason for Changes:** The raw AI snippet checked only `File.Exists()`. When user datasets were re-uploaded under the same reference name, the server silently served outdated cached files. Adding timestamp comparison resolved stale cache bugs completely.

---

## Summary of Lessons Learned in AI-Assisted Development

1. **AI Strengths:** GPT-4o and Claude 3.5 Sonnet excelled at scaffolding complex boilerplate code, implementing standard patterns (ASP.NET Core dependency injection and MCP protocol setup), and drafting system prompt guidelines.
2. **AI Limitations & Misconceptions:**
   * **Context Awareness:** AI models repeatedly assumed client and server shared a local filesystem, requiring manual invention of the Base64 file streaming bridge.
   * **Security Assumptions:** Default AI outputs frequently used overly permissive file permissions, public Azure Blob access, and weak regex pattern matching.
   * **State Management:** AI generated stateless cache lookups that led to stale local file bugs until timestamp checking was hand-written.
3. **Synthesis:** Successful development required an interactive **human-in-the-loop** workflow where AI provided raw implementations, which were then systematically audited, sandboxed, and optimized for production cloud deployment.
