# ML-25/26-05: Python Code Tool Interpreter for Chart Creation.

## Problem Statement

<p align="justify">
Current Large Language Models (LLMs) excel at generating code but struggle contextually with performing deep data manipulation and visualization autonomously on users' local hardware. When users upload datasets (e.g., CSV or Excel) that exceed the model's context window, the AI cannot simply read the data to generate accurate visualizations. Furthermore, running AI-generated Python code directly on local systems poses significant security risks.
</p>

**Full Project Documentation & Developer Guide**. <a href="https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/FileForge/Source/ML-25-26-05-Python-Code-Tool-Interpreter-for-Chart-Creation/Documentation/DEVELOPER_DOCUMENTATION.md">Read this document.</a>

## What Has to be Done?

The goal is to build an intelligent AI agent using the Microsoft Agent Framework in C# / .NET that can act as a secure, local, auto-correcting orchestrator capable of interpreting natural language into fully functional data visualizations. The agent must:

1. Handle large data files safely via a local sandboxed file store.
2. Sample and infer schemas from massive datasets without overwhelming the LLM.
3. Translate user intent into executable Python scripts.
4. Validate generated Python code for safety, blocking malicious or destructive commands before execution.
5. Provide a closed-loop execution environment that analyzes errors and feeds crashes back into the AI to "self-correct" its next attempt.

## How it Will be Done?

1. **Core Architecture**: The application will use C# 13 and .NET 10 to host a ChatClientAgent connected to OpenAI (GPT-4o).
2. **AI Tooling**: An abstraction layer (`ChartPlugin.cs`) will be given to the AI containing tools for discovering files, sampling data (`PreviewFileAsync`), executing scripts (`GenerateAndRunChart`), and returning the rendered chart images.
3. **Execution Sandbox**: A dedicated `PythonExecutor.cs` orchestrator will invoke the local Python executable asynchronously with bounded memory and process timeouts, ensuring runaway scripts don't consume host resources.
4. **Resiliency System**: The `ErrorMappingStore` will intercept exceptions (e.g., syntax errors or incorrect column names) and persist them to modify the LLM's system prompt dynamically, preventing the same mistake twice.

---

## Personal Project Plan (13 Weeks)

**Project Start:** January 1st, 2026  
**Project End:** March 31st, 2026

I have distributed the workload evenly across the required 6 sprints throughout the 13-week period:

### 1st Sprint (January 1 – January 14)

- **Objective:** Foundation & Environment Setup
- Setup the .NET 10 console application.
- Integrate the Microsoft.Extensions.AI package and establish connection with the OpenAI GPT-4o API.
- Setup cross-platform detection for local Python environments (`PythonPathResolver.cs`).

### 2nd Sprint (January 15 – January 28)

- **Objective:** Storage & Data Ingestion
- Implement the local sandboxed file storage subsystem (`LocalFileStore.cs`).
- Build the `DatasetAnalyzer.cs` to handle chunked reading of CSV/Excel files.
- Develop the type inference engine to analyze data schemas efficiently for the LLM.

### 3rd Sprint (January 29 – February 11)

- **Objective:** AI Tool Interfacing & Agent Definition
- Build the core `ChartPlugin.cs` logic exposing file upload/analysis tools to the AI.
- Refine the System Prompts and Agent configuration to enforce `.py` plotting best practices.
- Achieve the first successful end-to-end "Natural Language to Python String" test.

### 4th Sprint (February 12 – February 25)

- **Objective:** Code Validation & Security
- Develop the `PythonCodeValidator.cs`. Implement Abstract Syntax Tree (AST) scanning rules blocking OS, Shell, and Networking modules.
- Build structural verifications to guarantee `matplotlib` uses the `Agg` backend and saves files properly.

### 5th Sprint (February 26 – March 11)

- **Objective:** The Execution Sandbox & Self-Healing
- Build the `PythonExecutor.cs` to launch local scripts with execution timeouts and memory limits.
- Create the `ErrorMappingStore.cs` to capture runtime failures and feed them back to the AI context.
- Conduct extensive load-testing on massive (>100MB) files.

### 6th Sprint (March 12 – March 31)

- **Objective:** Quality Assurance & Final Polish
- Complete the xUnit Test Suite (`UnitTestProject`) focusing on "Golden Scenarios".
- Finalize the JSON `ChartManifest` logging architecture.
- Wrap up documentation, code cleanup, and final integration testing ahead of the March 31st deadline.

---

**Full Project Documentation & Developer Guide**. <a href="https://github.com/UniversityOfAppliedSciencesFrankfurt/se-cloud-2025-2026/blob/FileForge/Source/ML-25-26-05-Python-Code-Tool-Interpreter-for-Chart-Creation/Documentation/DEVELOPER_DOCUMENTATION.md">Read this document.</a>
