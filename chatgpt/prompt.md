https://chatgpt.com/c/6a838868-b394-83ed-a31d-1b9dff800836

# AI Usage Documentation - Python Code Tool Interpreter for Chart Creation

**Project ID:** ML-25-26-05  
**Authors:** Md Ashiqur Rahman and Md Sohel Rana  
**Stage 1 course:** Software Engineering  
**Stage 2 course:** Cloud Computing  
**Institution:** Frankfurt University of Applied Sciences  
**Year:** 2026

---

## 1. Purpose and evidence policy

This document records how generative AI was used during the two development stages of the project. It is structured to match the course requirement to document prompts, tools/models, AI output, manual changes, reasons for those changes, and iteration counts.

The surviving project archive does **not** contain complete exports of the original ChatGPT and Claude conversations, and it does not contain the `.git` history. Therefore, this document distinguishes three kinds of evidence:

1. **Verbatim retained prompt** - text that survives in the submitted project documentation or deployment notes and can be reproduced exactly.
2. **Reconstructed representative prompt** - a prompt reconstructed from the implemented code and the development goal. It is included to make the workflow reproducible, but it is **not claimed to be a verbatim historical prompt**.
3. **Implementation/deployment evidence** - source files, tests, reports, and terminal logs that show what was actually implemented or debugged.

The two AI services confirmed by the authors are **ChatGPT** and **Claude**. The exact ChatGPT and Claude model versions used for development were not retained and are therefore recorded as **unknown/not retained** rather than guessed. The application's runtime model is a separate matter: the Stage 1 and Stage 2 application configuration uses **GPT-4o** for the chart-creation agent.

No percentage of "AI-written" versus "human-written" lines is reported because the available evidence does not support a reliable line-by-line attribution. The project is therefore described as **AI-assisted development with manual architecture, integration, validation, testing, and debugging**.

---

# Stage 1 - AI Usage

## 2. Stage 1 development context

Stage 1 implemented a local .NET console application in which the AI agent, file handling, dataset analysis, Python validation, Python execution, error feedback, and chart history all lived inside one application. The main implementation included:

- a Microsoft Agent Framework console agent using GPT-4o;
- a direct in-process `ChartPlugin` tool interface;
- `LocalFileStore` and `StorageTool` for local data files;
- `DatasetAnalyzer` for sampling, schema/type inference, and large-file summaries;
- `PythonCodeValidator` for safety/structure checks and Python syntax validation;
- `PythonExecutor` for child-process execution with timeout/resource controls;
- `ErrorMappingStore` for persisted error records that could be summarized back to the agent;
- `ChartManifest` for generated-chart history;
- MSTest unit tests and five golden visualization scenarios.

AI was primarily used as a planning, design, code-generation, debugging, and review assistant. The surviving Stage 1 appendix provides direct evidence that ChatGPT was used during the design phase.

## 3. Verbatim retained Stage 1 prompt

**Tool:** ChatGPT  
**Exact model/version:** Not retained  
**Prompt status:** Verbatim - preserved in the Stage 1 appendix

> As part of our Software Engineering project, we are planning to develop an AI agent that can generate Python code based on user instructions. We have received some guidelines and requirements from our professor, which are attached here.
>
> Large Language Models (LLMs) have demonstrated significant potential in generating executable code from natural language instructions, particularly in the domains of data analysis and visualization. Despite these capabilities, LLM-based agents typically lack the ability to execute the generated code autonomously. This project investigates the design and implementation of an intelligent agent based on the Microsoft Agent Framework using .NET and C#, augmented by a custom Code Interpreter Tool implemented as an agent plug-in. The agent translates user prompts—such as requests for charts or diagrams—into Python code for data visualization, while the Code Interpreter Tool is responsible for executing this code and generating the corresponding visual output. A central focus of the project is the robust mapping of user-provided data to executable code, as well as the interpretation and execution of dynamically generated Python scripts using appropriate libraries. Furthermore, the project explores strategies for efficiently handling large datasets and supporting the generation of multiple visualizations from complex user inputs. Key challenges addressed include scalability, data transfer between agent components, and the reliable execution of model-generated code. The outcome of this work aims to identify best practices for integrating code execution capabilities into LLM-driven agent architectures for advanced data visualization tasks.
>
> Now please tell me how this project could be developed. What should the plan be, and how can it be done

### Raw AI output retained for this prompt

The Stage 1 appendix preserves the corresponding raw ChatGPT response. The response proposed, among other items:

- a .NET/C# AI visualization agent with a Python execution plug-in;
- a pipeline from user prompt and dataset through an agent, prompt builder/dataset profiler, LLM code generation, validation, sandboxed execution, artifacts, and logs;
- components corresponding to a dataset profiler, code validator, code interpreter, Python sandbox runner, and artifact manager;
- phased implementation and evaluation ideas.

**Raw output location:** `Folder1/Documentation/Appendix.pdf` (AI planning section).  
**Commit hash:** unavailable because the supplied archive contains no `.git` history.

### Manual changes made after the planning output

The ChatGPT response was used as a design starting point, not as a complete implementation. The final Stage 1 project was manually adapted to the actual course requirements and .NET solution. Examples include:

- translating the high-level architecture into concrete C# classes and interfaces;
- integrating the Microsoft Agent Framework and the `ChartPlugin` tool methods;
- implementing local storage and safe reference-name handling;
- building dataset sampling/schema inference around the actual CSV workflow;
- adding project-specific Python validation rules such as disallowed operations and required `savefig` behavior;
- implementing execution timeout and resource handling around a Python child process;
- adding persistent error-history and chart-manifest files;
- writing and maintaining MSTest coverage and golden scenarios.

**Reason for manual changes:** the AI response was architectural guidance and did not know the final class structure, framework APIs, test constraints, file formats, course requirements, or bugs encountered during integration.  
**Iteration count:** not retained.

---

## 4. Stage 1 significant AI-assisted contributions

The following table separates the one preserved verbatim planning prompt from later reconstructed prompts. Reconstructed prompts describe the kind of request used to move the implementation forward but are not presented as historical quotations.

| # | Contribution | Prompt / prompt status | Tool/model | AI output / evidence | Manual changes made | Reason | Iterations |
|---|---|---|---|---|---|---|---|
| 1 | Initial system architecture | **Verbatim prompt shown in Section 3.** | ChatGPT; exact model not retained | Raw response preserved in `Folder1/Documentation/Appendix.pdf` | Converted the proposed pipeline into the actual .NET solution, classes, tools, storage, validation, execution, error feedback, and tests | AI output was high-level and needed to match the real course project | Not retained |
| 2 | Large-dataset handling and schema discovery | **Reconstructed representative prompt:** "Design a C# dataset analyzer for CSV files that can inspect columns and types without sending a very large file to the LLM. Include safe sampling and a concise schema summary for the agent." | ChatGPT/Claude attribution not retained | Resulting implementation: `DatasetAnalyzer.cs` and agent instructions | Adjusted parsing, sampling, inferred metadata, and how the summary was exposed to the agent | Real CSV behavior and model-context constraints required project-specific handling | Not retained |
| 3 | Python-code validation | **Reconstructed representative prompt:** "Review the generated-Python execution pipeline and design validation checks that reject dangerous imports/functions, require non-interactive Matplotlib output, and verify Python syntax before execution." | ChatGPT/Claude attribution not retained | Resulting implementation: `PythonCodeValidator.cs` | Added/adjusted blocked patterns, chart-structure rules, `savefig` requirement, and syntax-validation behavior to fit generated scripts | AI-generated validation suggestions required alignment with actual allowed chart code and false-positive behavior | Not retained |
| 4 | Python execution and resource handling | **Reconstructed representative prompt:** "Implement a C# Python executor that runs generated code in a child process, captures stdout/stderr, times out long-running scripts, and reports useful errors back to the agent." | ChatGPT/Claude attribution not retained | Resulting implementation: `PythonExecutor.cs`, `SandboxConfig.cs` | Integrated executable-path resolution, timeout handling, output capture, temporary scripts, and platform-specific resource behavior | Generic snippets were insufficient for the project's cross-process and cross-platform needs | Not retained |
| 5 | Runtime error feedback | **Reconstructed representative prompt:** "Create an error-memory component that classifies common Python failures, stores recent errors, and gives a concise summary to the agent so it can avoid repeating the same failure." | ChatGPT/Claude attribution not retained | Resulting implementation: `ErrorMappingStore.cs` | Defined project-specific categories, JSON persistence, truncation, grouping, and prompt formatting | Needed deterministic storage and concise context rather than unlimited raw logs | Not retained |
| 6 | Testing and debugging | **Reconstructed representative prompt:** "Generate edge-case tests for path sanitization, storage, Python validation, execution failures, chart generation, and representative visualization scenarios. Review failures and suggest minimal fixes." | ChatGPT/Claude attribution not retained | Stage 1 MSTest project: 38 test methods plus a dynamic golden-scenario test driven by five scenarios | Tests were edited to match real interfaces and expected behavior; failing assumptions were corrected during implementation | AI-generated tests often need adaptation to actual constructors, mocks, and filesystem/process behavior | Not retained |

---

## 5. Stage 1 AI-assisted workflow

A reproducible approximation of the Stage 1 workflow is:

1. **Describe the problem and constraints to AI.** The initial retained ChatGPT prompt supplied the academic problem statement, Microsoft Agent Framework requirement, .NET/C# context, Python visualization requirement, large-data concern, and execution-safety concern.
2. **Use the AI response as an architecture proposal.** The response was decomposed into C# components rather than copied as a finished solution.
3. **Implement one component at a time.** Storage, dataset analysis, validation, execution, error feedback, tool methods, and agent orchestration were built and connected.
4. **Ask AI for focused code/debugging help.** Smaller requests were used for individual classes, test cases, exception handling, and integration issues.
5. **Compile and test manually.** AI suggestions were accepted only after they matched the codebase and test behavior.
6. **Refine the agent instructions.** Rules for file upload, path resolution, Matplotlib usage, large datasets, and retries were encoded into the system instructions.
7. **Document the completed system.** The final Stage 1 report, appendix, presentation, screenshots, and demonstration were prepared from the working project rather than from the original AI proposal alone.

### Stage 1 quality issues found in AI-assisted development

The implemented project also shows why AI output required review. For example, security terminology had to be treated carefully: the validator performs important text/structure checks and invokes Python AST parsing for syntax validation, but this is not equivalent to a complete AST-based security sandbox. Similarly, child-process restrictions and environment settings improve isolation but do not provide complete network isolation on every operating system. These distinctions are important when converting AI-generated explanations into accurate technical documentation.

---

# Stage 2 - AI Usage

## 6. Stage 2 development goal: Folder1 to Folder2

Stage 2 did not simply add more chart types. The main change was architectural: the local Stage 1 application was refactored into a **thin Agent client plus a remotely hosted MCP server**.

The main Stage 2 changes were:

- direct in-process tool calls -> **Model Context Protocol (MCP) over HTTP**;
- one local application -> **separate Agent and ASP.NET Core server applications**;
- local file storage -> **Azure Blob Storage**;
- local chart paths -> **blob-hosted charts returned through SAS URLs**;
- single-user local storage -> **team-prefixed blob namespaces using the `Team-Name` header**;
- local-only execution -> **Dockerized server deployment to Azure Container Apps**;
- basic Python path resolution -> **Python environment readiness/setup component**;
- local input paths across process boundaries -> **client-side Base64 upload adapter**;
- server local cache -> **24-hour background cleanup with re-download from Blob Storage when needed**.

At the same time, Stage 2 reused the core Python validation/execution approach rather than rewriting it unnecessarily. Several Stage 1 subsystems were removed or replaced, including `DatasetAnalyzer`, `ErrorMappingStore`, `ChartManifest`, `LocalFileStore`, and the golden-scenario source suite.

The authors identified **MCP architecture** as the principal area of significant manual engineering in Stage 2.

---

## 7. Retained Stage 2 AI instruction context

The two supplied deployment/debugging text files do not contain a complete Claude or ChatGPT conversation. However, `azure dep.txt` preserves the following instruction context verbatim at the beginning of the record:

**Tool:** the exact ChatGPT/Claude attribution for this retained block is not recoverable from the file  
**Exact model/version:** not retained  
**Prompt status:** verbatim retained instruction context

> Act as a senior software engineer.
>
> * Never hallucinate or guess.
> * Think and analyze before answering.
> * Base your answers only on the code and information I provide.
> * If you don't have enough context, say so instead of making assumptions.
> * If you need to inspect the project, tell me exactly which file(s) you need (e.g., Program.cs, service, controller, configuration, logs, or stack trace).
> * Explain your reasoning before suggesting a solution.
> * Prefer the simplest, cleanest, and most maintainable solution.

This context is consistent with the Stage 2 workflow: source files and terminal errors were supplied to the AI, the AI was asked to reason from the actual project, and proposed fixes were checked by rebuilding/rerunning the software.

**Raw AI output for this instruction context:** not retained as a complete chat transcript.  
**Available evidence instead:** the final source tree and the two terminal/deployment logs, `azure dep.txt` and `azure dep mcp.txt`.  
**Commit hash:** unavailable because the supplied archive contains no `.git` history.  
**Iteration count:** exact AI prompt count not retained.

---

## 8. Stage 2 significant AI-assisted contributions

| # | Contribution | Prompt / prompt status | Tool/model | AI output / evidence | Manual changes made | Reason | Iterations |
|---|---|---|---|---|---|---|---|
| 1 | Stage 1 -> Stage 2 MCP architecture | **Reconstructed representative prompt:** "Analyze the current Stage 1 chart-creation agent and redesign it so the AI client is separated from execution/storage. Use MCP over HTTP: the Agent should discover tools, while an ASP.NET Core MCP server owns storage, Python validation, and execution. Keep the existing validation/execution code where it is still useful." | Claude + ChatGPT were used during Stage 2; exact per-prompt attribution/model not retained | Resulting source: `Agent/MCP/McpHttpTransport.cs`, `ChartCreationMCPServer/Program.cs`, `Tools/ChartPlugin.cs` | The authors manually designed/integrated the MCP boundary, dependency placement, tool exposure, request flow, and retained-vs-moved Stage 1 components | This was the central Stage 2 architectural task and required understanding the existing project rather than accepting a generic greenfield solution | Not retained |
| 2 | Dynamic MCP tool discovery and Agent adaptation | **Reconstructed representative prompt:** "Connect the .NET Agent to the remote MCP server, discover its registered tools at startup, and expose them to the GPT agent. Keep local-file upload usable even though the server cannot read a client machine path." | Claude/ChatGPT exact attribution not retained | `McpHttpTransport.cs`, `Helpers.CreateUploadFileTool`, `Helpers.UploadFileAsync` | The remote `upload_file` tool is not directly exposed to the model; a local wrapper reads bytes, applies a 50 MB limit, Base64-encodes the file, and then invokes the server tool | A cloud server cannot access `C:\...` on the user's computer; an adapter was required across the client/server boundary | Not retained |
| 3 | Azure Blob Storage backend | **Reconstructed representative prompt:** "Replace LocalFileStore with Azure Blob Storage. Separate input and output containers, namespace blobs by team, cache input blobs locally for Python, refresh stale cache entries, and generate shareable chart URLs." | Claude/ChatGPT exact attribution not retained | `AzureBlobStorageStore.cs`, `IStorageStore.cs`, `PathSafety.cs` | Added team sanitization/prefixing, private containers, cache-path handling, `LastModified` refresh behavior, chart uploads, and SAS URL generation | The Stage 1 filesystem model did not work for a remote multi-user server | Not retained |
| 4 | Team-scoped requests | **Reconstructed representative prompt:** "Pass a Team-Name value from Agent to server and use it to isolate each team's input/output blob prefix. Keep the fallback behavior deterministic if the header is missing." | Claude/ChatGPT exact attribution not retained | `McpHttpTransport.cs`, `ChartPlugin.CurrentTeam()`, storage prefix logic, path-safety tests | The header is read server-side and sanitized before use; tests were added for team/reference sanitization and fallback behavior | Multiple users/teams needed separate namespaces in the same storage account | Not retained |
| 5 | Docker image with .NET and Python | **Reconstructed representative prompt:** "Create a multi-stage Dockerfile for the ASP.NET Core MCP server. Build with .NET 10, run on the ASP.NET runtime image, install Python 3 plus matplotlib/pandas/numpy/openpyxl, and expose port 8080 for Azure Container Apps." | Claude/ChatGPT exact attribution not retained | `ChartCreationMCPServer/Dockerfile`; deployment logs show a completed Docker build and a successful local container startup | Adjusted package installation, environment variables, runtime image, and port binding to the actual server | Both .NET and Python must coexist in the same deployable server image | Multiple build/run cycles visible; exact AI prompt count not retained |
| 6 | Azure deployment and troubleshooting | **Task-specific historical prompt not retained.** The retained instruction context in Section 7 governed the troubleshooting session. A representative task was: "Use these exact Azure CLI errors to identify the next minimal deployment step without guessing; deploy the Docker image to Azure Container Apps." | Claude and/or ChatGPT; exact attribution/model not retained | `azure dep.txt` and `azure dep mcp.txt`: Docker build, ACR authentication failure then successful login/push, unsuccessful App Service path, Container Apps environment errors, successful environment creation, and successful Container App creation | Commands were re-run with corrected arguments/authentication; deployment approach was changed to Container Apps; a permitted/available region was used after the Germany West Central environment error | Azure CLI syntax, registry authentication, subscription policy, service choice, and regional environment limits required iterative troubleshooting | Multiple iterations visible in terminal history; exact AI prompt count not retained |
| 7 | Python readiness and health reporting | **Reconstructed representative prompt:** "Make the server self-check its Python environment at startup and expose readiness through a health endpoint. In Docker, packages should already exist, so startup installation can be disabled." | Claude/ChatGPT exact attribution not retained | `PythonEnvironmentSetup.cs`, server `/health`, Docker environment variables | Integrated structured readiness state, package checks, console report, and health payload | Cloud failures are easier to diagnose when Python/package readiness is observable without an interactive shell | Not retained |
| 8 | Stage 2 tests and regression fixes | **Reconstructed representative prompt:** "Update the tests after the MCP/cloud refactor. Cover validator, path/team sanitization, executor behavior, Azure storage behavior, Base64 upload/tool behavior, and ChartPlugin delegation without relying on real cloud resources for every unit test." | Claude/ChatGPT exact attribution not retained | Stage 2 MSTest source contains 38 test methods: 7 validator, 11 path safety, 5 executor, 1 Azure store, and 14 ChartPlugin tests. The authors confirmed all 38 passed in the final Stage 2 environment. | Tests were aligned with the new interfaces and MCP/server responsibilities; mocks/delegation checks were used where appropriate | Stage 1 tests referenced classes removed or replaced during the cloud refactor | Not retained |

---

## 9. Deployment/debugging iteration evidenced by the logs

The deployment transcript is useful evidence of how AI-assisted debugging was used even though the full chat itself was not retained. The sequence includes several concrete failures and corrections:

1. **Docker build:** the server image was built successfully as a multi-stage .NET/Python image.
2. **Local container verification:** the image reported Python 3.12.3, the required plotting/data packages, a ready Python environment, the cache-cleanup background service, and an ASP.NET listener on port 8080.
3. **Azure Container Registry authentication:** an initial `docker push` failed with an authentication error. After an explicit `az acr login`, the image push succeeded and a registry digest was returned.
4. **Early App Service attempt:** the transcript contains an attempted App Service route and registry/plan issues. This was not the final deployment architecture.
5. **Container Apps environment:** a first environment creation attempt encountered subscription/region restrictions; Germany West Central later returned a maximum-environments error for the subscription.
6. **Successful Container Apps environment:** an environment was successfully created in an allowed region in the supplied transcript.
7. **Successful Container App:** the `chartserver:v1` image was deployed with external ingress to target port 8080, 0.5 CPU, and 1.0 GiB memory, and Azure returned an `azurecontainerapps.io` endpoint.

This sequence illustrates the practical AI-assisted loop used in Stage 2:

**error/log -> ask AI to reason from the exact output -> apply the smallest change -> rerun command/build -> inspect new output -> repeat.**

The raw deployment files contain credentials and environment-specific identifiers. Those values should be **redacted before submission or publication**, and any exposed registry/storage/API credentials should be rotated.

---

## 10. Stage 2 AI-assisted development workflow

A similar project could be developed with the following evidence-aligned workflow:

### Step 1 - Give the AI the existing Stage 1 architecture, not only the new requirement
Ask the AI to inspect `Program.cs`, `ChartPlugin`, storage classes, validator, executor, tests, and configuration. Require it to identify which components can be reused and which responsibilities must move to the server.

### Step 2 - Define the client/server boundary manually
Decide that the Agent owns conversational/model concerns and local user interaction, while the MCP server owns tools, storage, validation, execution, and chart persistence. This was the most important manual Stage 2 design decision.

### Step 3 - Introduce MCP with the smallest functional vertical slice
Start the ASP.NET Core server, expose one tool, connect the Agent over HTTP, perform MCP discovery, and invoke the tool. Only after this works should all seven chart/file operations be migrated.

### Step 4 - Solve the local-file boundary explicitly
Because a remote server cannot dereference a user's local path, add a client-side upload wrapper. Validate file existence/size locally, encode bytes, send them to the remote upload tool, and persist them in cloud storage.

### Step 5 - Replace local persistence with Azure Blob Storage
Implement `IStorageStore` using private input/output containers, team prefixes, path sanitization, download-to-cache behavior for Python, and chart upload/SAS generation.

### Step 6 - Reuse proven core code where appropriate
Move/refactor the Stage 1 validator and executor rather than rewriting them only because the deployment model changed. Re-test them in the server environment.

### Step 7 - Containerize the actual runtime stack
The Docker image must contain both .NET and Python plus plotting/data dependencies. Run the image locally and verify Python readiness and port 8080 before attempting Azure deployment.

### Step 8 - Use AI as a log-driven deployment debugger
Supply exact CLI commands and errors. Require the AI to avoid assumptions, explain the failure, and propose one minimal next command. Validate every suggestion in the terminal.

### Step 9 - Update tests after the architecture settles
Do not mechanically copy Stage 1 tests. Remove tests for deleted subsystems, add tests for team sanitization/MCP tool behavior/cloud-storage delegation, and run the full suite.

### Step 10 - Review AI output for correctness and security claims
Check generated descriptions and code against the implementation. In particular, distinguish namespace isolation from authentication, validation from complete sandboxing, and a successful unit test from a real cloud end-to-end test.

---

## 11. What was AI-generated versus manually developed

Because the original chat transcripts and Git history are unavailable, exact line-level provenance cannot be reconstructed. The strongest evidence-supported description is:

### AI-assisted areas

- initial architecture brainstorming and component decomposition;
- example C# implementations and refactoring suggestions;
- Python execution/validation patterns;
- test-case ideas and edge cases;
- MCP/Azure/Docker implementation guidance;
- Azure CLI troubleshooting and interpretation of deployment errors;
- documentation/review assistance.

### Significant manual engineering and review

- choosing the final Stage 1 and Stage 2 architectures;
- **designing and integrating the Stage 2 MCP architecture**;
- deciding what Stage 1 code to reuse, move, replace, or remove;
- integrating real framework APIs and dependency injection;
- running builds/tests and correcting AI suggestions that did not match the project;
- debugging Azure authentication, deployment service, and region/subscription issues;
- validating generated Python behavior and chart outputs;
- reviewing security and correctness claims;
- final project documentation and submission preparation.

This attribution is intentionally qualitative rather than a fabricated numerical percentage.

---

## 12. Correctness and quality issues identified during AI-assisted development

AI suggestions were not treated as automatically correct. Important review findings include:

- **MCP team isolation is not authentication.** `Team-Name` controls a sanitized blob namespace but is not a verified user identity.
- **The Python validator is a defense layer, not a complete sandbox.** Text/pattern checks can be bypassed by sufficiently creative Python constructs.
- **`NO_PROXY=*` is not network isolation.** It should not be documented as if it blocks all network access.
- **Memory enforcement differs by platform.** The existing executor does not prove a strong Linux container memory sandbox by itself.
- **Long-lived SAS URLs are bearer credentials.** One-year chart URLs are convenient but increase the exposure window if a URL is leaked.
- **The Agent upload adapter has a 50 MB limit.** This is a Stage 2 constraint and is smaller than the very-large-file scenario discussed in Stage 1.
- **Some Stage 1 capabilities were removed.** Stage 2 no longer contains the same `DatasetAnalyzer`, `ErrorMappingStore`, `ChartManifest`, or source golden-scenario suite.
- **Unit tests are not the same as cloud end-to-end evidence.** The Stage 2 source defines 38 unit tests and the authors confirmed they pass, but the supplied deployment transcript does not include the `dotnet test` output and the source does not provide a full live Azure end-to-end test suite.
- **Secrets appeared in raw logs/configuration history.** Submission copies should remove credentials and exposed secrets should be rotated.

These findings are examples of manual review correcting overly confident or incomplete AI-generated explanations.

---

## 13. Reconstructed representative prompt set for reproducibility

The following prompts are provided so that another student/developer could follow a similar AI-assisted approach. They are **representative prompts, not historical verbatim prompts** unless explicitly marked otherwise elsewhere in this document.

### A. Compare Stage 1 before refactoring

> Analyze the complete existing chart-creation project before proposing changes. Identify the responsibilities of the Agent, ChartPlugin, storage layer, dataset analyzer, validator, Python executor, error memory, chart manifest, and tests. Tell me what can be reused in a cloud/MCP version and what must change. Do not invent missing behavior.

### B. Design the MCP boundary

> Redesign the local monolithic application into two applications: a thin .NET Agent and an ASP.NET Core MCP server. The Agent should connect over HTTP, discover server tools, and let the LLM invoke them. The server should own storage, validation, Python execution, and chart generation. Show the request/data flow and the minimum code changes needed.

### C. Move storage to Azure Blob Storage

> Implement the current IStorageStore contract using Azure Blob Storage. Use separate private containers for input files and output charts, scope all blobs under a sanitized team prefix, cache input files locally for Python, refresh stale cache entries from blob metadata, and return a SAS URL for generated charts. Preserve existing method behavior where possible.

### D. Adapt local uploads to a remote server

> The LLM currently receives a local Windows file path, but the MCP server runs in Azure and cannot access that path. Design a safe client-side adapter that verifies the file, limits upload size, reads bytes, Base64-encodes them, calls the server upload tool, and keeps the tool interface simple for the model.

### E. Containerize .NET plus Python

> Create a multi-stage Dockerfile for a .NET 10 ASP.NET Core MCP server that also runs generated Python chart code. The runtime image must include python3, pip, matplotlib, pandas, numpy, and openpyxl. The app listens on port 8080. Keep the final image as small and reproducible as reasonably possible.

### F. Debug deployment from real CLI output

> Act as a senior software engineer. Do not guess. I will paste the exact Azure CLI command and error. Explain the root cause from the output, then give me the smallest next command to verify or fix it. Do not change multiple unrelated things at once.

### G. Review security claims

> Review this implementation and documentation for claims that are stronger than the code proves. Check authentication, team isolation, Python sandboxing, network restrictions, resource limits, SAS lifetime, secrets, cache behavior, and test coverage. Separate implemented controls from future improvements.

### H. Update the test suite after the refactor

> Compare the Stage 1 tests with the Stage 2 architecture. Remove tests for components that no longer exist, keep regression tests for reused validator/executor behavior, and add tests for team/reference sanitization, ChartPlugin delegation, Base64 upload behavior, and Azure-store logic. Use mocks or fakes where a unit test should not call real Azure resources.

---

## 14. Summary of AI use across both stages

Stage 1 used AI mainly to turn the academic problem into a workable local architecture and to accelerate the implementation/debugging of dataset analysis, Python validation/execution, error feedback, and tests. The strongest surviving evidence is the original ChatGPT planning prompt and its raw response in the Stage 1 appendix.

Stage 2 used ChatGPT and Claude as coding and troubleshooting assistants while the project was re-architected from a local monolith into a client/server MCP system with Azure Blob Storage, Docker, and Azure Container Apps. The main manual contribution was the MCP architecture and integration. The supplied deployment logs show iterative debugging of Docker, Azure Container Registry, service selection, authentication, and Container Apps environment constraints.

Where original AI transcripts no longer exist, this document does not present reconstructed text as verbatim. This makes the AI-usage record reproducible while keeping a clear boundary between retained evidence and reconstruction.
