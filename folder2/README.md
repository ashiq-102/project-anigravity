# ML-25-26-05 — AI-Powered Chart Creation Tool

An AI assistant that turns plain-language requests such as *"make a bar chart of 10, 40, 500"* into real, downloadable data visualisations — powered by GPT-4o, executed with Python and matplotlib, and delivered through the cloud.

**Course:** Cloud Computing  ·  **Supervisor:** Prof. Damir Dobric  ·  **Project ID:** ML-25-26-05

---

## 1. Project Overview

### Purpose

The goal of this project is to let a user create data charts by describing what they want in natural language, instead of writing code or using a spreadsheet tool. The user talks to an AI agent; the agent writes the plotting code, runs it on a server, and returns a ready-to-view chart image.

### The problem it solves

Creating a chart normally requires either writing Python/matplotlib code yourself or clicking through a spreadsheet application. Both require skill and effort. This project removes that barrier: the user states the intent, and the system handles the code generation, safe execution, and image delivery. It also solves a cloud-engineering problem — how to run untrusted, AI-generated code safely on a server and make the output available anywhere through a URL.

### Objectives

- Let users generate charts from natural-language requests, with or without a data file.
- Cleanly separate the *reasoning* part (the AI agent) from the *execution* part (the tool server), so each can evolve independently.
- Run AI-generated Python **safely**, with validation and sandboxing.
- Store inputs and outputs in the cloud so charts are accessible from anywhere via a shareable link.
- Keep different users' or teams' data isolated from one another.

---

## 2. Project Architecture

The system is built as **two independent applications** that communicate over the **Model Context Protocol (MCP)** — an open standard for connecting AI agents to tools.

```
┌─────────────────────────────┐
│   ChartCreationAgent        │   Thin console client
│   (Console App, .NET 10)    │   - Reads user input
│                             │   - Talks to GPT-4o
│   • GPT-4o (via OpenAI)     │   - Holds the system prompt
│   • System prompt / rules   │   - NO business logic
└──────────────┬──────────────┘
               │  HTTP  (MCP protocol, Team-Name header on every call)
               ▼
┌─────────────────────────────┐
│   ChartCreationMCPServer    │   All business logic lives here
│   (ASP.NET Core, .NET 10)   │
│                             │
│   • 7 MCP tools             │
│   • Python code validation  │
│   • Sandboxed execution     │
│   • Storage orchestration   │
│   • Python + matplotlib     │
└──────────────┬──────────────┘
               │  Azure SDK
               ▼
┌─────────────────────────────┐
│   Azure Blob Storage        │
│   • input-files/{team}/...  │   Uploaded data files
│   • output-charts/{team}/.. │   Generated chart images
└─────────────────────────────┘
```

### How the components communicate

- The **Agent** never touches storage or runs code. It only decides *what* to do and calls tools on the server.
- The **Server** exposes its capabilities as MCP *tools*. The agent discovers these tools automatically at runtime — it is never hard-coded to know them.
- Communication happens over **HTTP** using the MCP standard. Every request carries a `Team-Name` header that tells the server which team's data area to use.
- The **Server** is the only component that talks to **Azure Blob Storage** and the only component that runs Python.

### Data flow, start to finish

1. User types a request into the Agent console.
2. GPT-4o interprets the request and decides which server tool(s) to call.
3. The Agent sends those tool calls to the Server over HTTP.
4. The Server validates and executes the work (e.g. runs Python to draw a chart).
5. The resulting image is uploaded to Azure Blob Storage.
6. The Server returns a time-limited, shareable URL.
7. The Agent shows that URL to the user.

---

## 3. How the Project Works

A complete walkthrough of what happens when a user asks for a chart from a data file:

1. **User request** — *"Create a sales trend chart from C:\data\sales.csv"*.
2. **The Agent's AI (GPT-4o)** recognises a file is involved and calls the `UploadFileAsync` tool with the file path.
3. **The Server** uploads the file to `input-files/{team}/sales.csv` in Azure Blob Storage and returns a short reference name, `sales`.
4. The AI then calls `ResolveFilePath("sales")`. The Server downloads the blob to a local temporary cache and returns a local path Python can read.
5. If the AI needs to know the column names, it calls `PreviewUploadedFile("sales")`, which returns the first part of the file as text.
6. The AI writes complete Python/matplotlib code using the resolved path and calls `GenerateChart` with that code.
7. **The Server validates the code** (blocks dangerous operations), then **executes it in a sandbox** with time and memory limits.
8. The generated PNG is uploaded to `output-charts/{team}/`, and a **1-year shareable URL (SAS URL)** is returned.
9. Temporary files on the server (`.py` script and `.png` image) are deleted immediately after upload.
10. The Agent presents the URL to the user, who can view or download the chart.

For a simple inline request like *"bar chart of 10, 40, 500"*, steps 2–5 are skipped — the AI puts the numbers directly into the Python code and goes straight to `GenerateChart`.

---

## 4. Core Features

Rather than listing features, here is the reasoning behind each one.

### Natural-language chart creation
The AI writes the matplotlib code itself. This means the user never needs to know Python. The system prompt teaches the AI exactly how to structure the code, which imports to use, and how to save the output — so its generated code is consistent and executable.

### Safe execution of AI-generated code
Because the code is written by an AI and could contain mistakes or unsafe operations, the Server never blindly runs it. A **validator** first rejects code that imports system modules (`os`, `subprocess`) or uses dangerous calls (`exec`, `eval`, `open`). An **executor** then runs the approved code inside a sandbox with a timeout and a memory ceiling, so a runaway script cannot harm the server.

### Cloud storage with shareable links
Inputs and outputs live in Azure Blob Storage, not on the server's disk. Charts are returned as **SAS URLs** — secure, time-limited links that let anyone with the link view the image without needing an account. This makes charts accessible and shareable from anywhere.

### Team-based data isolation
Every user configures a team name. All their files and charts are stored under a folder prefix for that team (`{team}/...`). Two different teams can upload a file called `sales.csv` without ever seeing each other's data. This turns a single shared server into a safe multi-user system.

### Smart file caching
When a file is needed for a chart, it is downloaded once and cached locally. Follow-up charts on the same file reuse the cache instead of re-downloading. A background service clears stale cache files after 24 hours so the server disk never fills up.

### Self-preparing Python environment
On startup, the Server checks whether Python and the required libraries are present. Locally, it can install anything missing automatically. In the cloud, everything is already baked into the container image. Either way, the Server reports its readiness through a `/health` endpoint rather than crashing.

---

## 5. Technologies and Tools

| Technology | Role in the project | Why it was chosen |
|---|---|---|
| **.NET 10 / C#** | Language and runtime for both apps | Strong typing, first-class async, and the platform the course is built around |
| **ASP.NET Core** | Hosts the MCP server | Production-grade HTTP hosting with built-in dependency injection |
| **Model Context Protocol (MCP)** | Communication standard between agent and server | Lets the agent discover and call tools without being hard-wired to them; decouples the two apps |
| **Microsoft.Agents.AI** | Builds and runs the AI agent | Provides the agent/session abstraction and automatic tool invocation |
| **OpenAI GPT-4o** | The reasoning engine | Interprets user intent and writes the Python plotting code |
| **Python + matplotlib** | Draws the actual charts | The de-facto standard for data visualisation; far richer than any C# charting equivalent |
| **pandas / numpy / openpyxl** | Reads and processes data files | Handle CSV, Excel, and tabular data cleanly |
| **Azure Blob Storage** | Stores input files and output charts | Durable, scalable cloud storage with built-in secure sharing via SAS URLs |
| **Docker** | Packages the server with Python inside | Guarantees the server runs identically on any machine and in the cloud |
| **Azure App Service** | Hosts the deployed server | Runs the Docker container as a public web service |

---

## 6. Project Workflow

The complete chronological flow, from a cold start to a delivered chart:

```
Startup
   │
   ├─ Server boots → checks Python environment → reports health
   ├─ Agent boots → connects to Server over HTTP → discovers 7 tools
   │
Conversation
   │
   ├─ User types a request
   ├─ GPT-4o decides which tool(s) to call
   │
Tool execution (on the Server)
   │
   ├─ (file case) UploadFileAsync → ResolveFilePath → PreviewUploadedFile
   ├─ GenerateChart → validate code → sandbox-execute → upload PNG
   │
Delivery
   │
   ├─ Server returns SAS URL
   ├─ Agent shows URL to the user
   └─ Temp files deleted; cache swept after 24h
```

Each stage feeds the next: discovery makes the tools callable, the conversation decides which tool runs, execution produces the image, and delivery hands it back as a link.

---

## 7. Key Design Decisions

**Two applications instead of one.**
The original design was a single console app. It was split into a thin *agent* and a logic-holding *server*. This separation means the server can be deployed, scaled, and secured independently, and any MCP-compatible client (not just this agent) could use it.

**Tools discovered at runtime, not hard-coded.**
The agent asks the server what tools exist rather than assuming. If a tool is added or renamed on the server, the agent adapts with no code change.

**The AI writes the code; the server guards it.**
Letting the AI generate Python is powerful but risky. The decision to always validate and sandbox before running is what makes that power safe.

**Team isolation by folder prefix, not by separate containers.**
Prefixing blobs with a team name gives complete separation without the overhead of creating and managing a container per team, and without hitting any container limits.

**Charts stored in the cloud, delivered as URLs.**
Rather than returning image files through the chat, the server uploads to Blob Storage and returns a link. This keeps the protocol lightweight and makes every chart instantly shareable.

**The server never crashes on a bad environment.**
Instead of exiting when Python is missing, it starts anyway and reports the problem through `/health`. A server that refuses to start cannot tell anyone why — so it stays up and stays honest.

---

## 8. Challenges and Solutions

| Challenge | Solution |
|---|---|
| Running a .NET server that also needs Python | A multi-stage Docker image with .NET on top of a Python-enabled base, so both live in one container |
| Safely executing AI-generated code | A two-layer defence: static validation of the code, then sandboxed execution with time and memory limits |
| Keeping multiple users' data separate on one server | A `Team-Name` header on every request, mapped to a per-team storage prefix |
| Charts and scripts filling the server disk | Immediate deletion of temp files after upload, plus a 24-hour background cache sweep |
| SAS URLs that expired too quickly | Extended chart links to a 1-year validity so users can revisit and share them |
| The agent regenerating charts it had already made | A system-prompt rule to reuse existing URLs from the conversation rather than re-running the tool |

---

## 9. Future Improvements

- **Managed identity for storage** — replace the storage connection string with Azure Managed Identity, removing the last stored secret entirely.
- **Authentication on the MCP endpoint** — add real user authentication so team isolation becomes true security rather than a naming convention.
- **A web or chat UI** — replace the console agent with a browser interface for a friendlier experience.
- **More chart libraries** — optionally support Plotly for interactive charts alongside matplotlib.
- **Persistent chart history** — a richer catalogue of past charts with friendly titles and search.
- **Auto-scaling** — allow the server to scale across multiple instances with a shared cache layer.

---

## 10. Setup and Running the Project

> This section is intentionally brief — the focus above is on understanding the system, not reproducing it.

### Prerequisites
- .NET 10 SDK
- Python 3.8+ with `matplotlib`, `pandas`, `numpy`, `openpyxl` (only if running the server outside Docker)
- An OpenAI API key
- An Azure Storage account
- Docker (for the containerised deployment)

### Configure
- In `ChartCreationMCPServer/appsettings.json`, set the Azure Storage connection string and container names.
- In `Agent/appsettings.json`, set the OpenAI API key, the MCP server URL, and a team name.

### Run locally
```bash
# 1. Start the server
cd ChartCreationMCPServer
dotnet run

# 2. In a second terminal, start the agent
cd Agent
dotnet run
```
Then type a request such as *"create a bar chart of 10, 40, 500"*.

### Run the server in Docker
```bash
cd ChartCreationMCPServer
docker build -t chartserver .
docker run -p 8080:8080 chartserver
# Verify: open http://localhost:8080/health
```

### Deploy to Azure
The server image is pushed to Azure Container Registry and run as an Azure App Service (Web App for Containers). The agent's `McpServer:Url` is then pointed at the deployed URL. Health can be confirmed at `https://<your-app>.azurewebsites.net/health`.

---

*This README serves as a condensed project report. It prioritises explaining what the system is, how its parts interact, and why each design choice was made, over step-by-step reproduction instructions.*

