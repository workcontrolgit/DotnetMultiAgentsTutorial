# Part 5: Claude Desktop Integration & End-to-End Demo

**Series:** AI Agents & MCP with .NET 10 | **Part 5 of 6**  
**GitHub:** [workcontrolgit/DotnetAiAgentMcp](https://github.com/workcontrolgit/DotnetAiAgentMcp)

---

## Introduction

In Part 4 we built `HrMcp.Agent` — a console app that connects to the MCP server over HTTP and holds a live AI conversation. That is one way to consume an MCP server. This part shows another: connect the server directly to Claude Desktop so that Claude's built-in AI can call your tools with no agent code at all.

By the end you will have:

- A self-contained `HrMcp.McpServer.exe` published and running under Claude Desktop
- The HR tools visible in Claude Desktop's hammer icon
- A working `.vscode/mcp.json` so VS Code Copilot Chat can also call the same server
- A debugging checklist for when things go wrong

---

## How stdio Transport Works with Claude Desktop

When Claude Desktop launches an MCP server it does not connect over HTTP. It spawns the server as a child process and communicates over stdin/stdout using JSON-RPC 2.0. This is the stdio transport.

```
Claude Desktop (host)
    │
    │  spawns child process
    ▼
HrMcp.McpServer.exe --stdio
    │
    │  stdin ← JSON-RPC requests
    │  stdout → JSON-RPC responses
    │
    ▼
MCP Tools (GetOpenPositions, GetHiringOrganizations,
           GetPositionsByOrganization, GetPositionById,
           WriteJobDescription)
    │
    ▼
SQL Server DB
```

Two rules follow from this architecture:

- **stdout must be clean.** Every byte written to stdout is treated as a JSON-RPC message. Any log line, startup banner, or debug output written to stdout will corrupt the protocol stream. This is why the `--stdio` flag clears all log providers and redirects them to stderr.
- **No HTTP listener.** In stdio mode the server does not bind a port. `builder.WebHost.UseUrls()` with no argument prevents ASP.NET Core from trying to listen on any address.

Both are already handled in `McpServer/Program.cs` from Part 3:

```csharp
if (isStdio)
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.WebHost.UseUrls(); // no HTTP listener
}
```

---

## Step 1 — Publish a Self-Contained Executable

Claude Desktop needs a single executable it can launch. Publish a self-contained win-x64 binary so the target machine does not need the .NET runtime installed.

```bash
dotnet publish src/HrMcp.McpServer \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o publish/McpServer
```

After publishing you will find `HrMcp.McpServer.exe` (and its supporting files) in `publish/McpServer/`. Note the full path — you will need it in the next step.

> **macOS / Linux:** Replace `-r win-x64` with `-r osx-arm64` (Apple Silicon), `-r osx-x64` (Intel Mac), or `-r linux-x64`. The output binary will have no `.exe` extension.

---

## Step 2 — Configure Claude Desktop

### Locate the config file

- **Windows:** `%APPDATA%\Claude\claude_desktop_config.json`
  - Paste `%APPDATA%\Claude\` into the Windows Explorer address bar to open the folder.
- **macOS:** `~/Library/Application Support/Claude/claude_desktop_config.json`
  - In Finder, press `Cmd+Shift+G` and paste the path.

If the file does not exist, create it.

### Add the server entry

```json
{
  "mcpServers": {
    "hr-mcp": {
      "command": "C:\\apps\\DotnetMcpTutorial\\publish\\McpServer\\HrMcp.McpServer.exe",
      "args": ["--stdio"]
    }
  }
}
```

Replace the `command` path with the actual path on your machine. Use double backslashes on Windows.

**macOS example:**

```json
{
  "mcpServers": {
    "hr-mcp": {
      "command": "/Users/yourname/projects/DotnetMcpTutorial/publish/McpServer/HrMcp.McpServer",
      "args": ["--stdio"]
    }
  }
}
```

### Restart Claude Desktop

Fully quit Claude Desktop (system tray → Quit, not just close the window) and relaunch it. After restart, look for the **hammer icon** in the chat input bar. Click it — you should see all five HR tools listed.

If the hammer icon is absent, see the Debugging section below.

---

## Step 3 — Live Demo

With Claude Desktop open and the tools loaded, try these prompts:

**Prompt 1 — List organizations**

> What federal agencies are currently hiring?

Claude calls `GetHiringOrganizations` and returns the four seeded organizations with their department names.

**Prompt 2 — Scoped position search**

> Show me all open positions at USCIS

Claude calls `GetHiringOrganizations` first (to resolve the org ID), then `GetPositionsByOrganization`, and presents the matching positions with salary ranges and telework eligibility.

**Prompt 3 — Full position detail**

> Give me full details on the IT Specialist SYSADMIN role at USCIS

Claude calls `GetPositionById` and returns the complete record including duty location, security clearance requirement, duties, and qualifications.

**Prompt 4 — AI-generated job announcement**

> Write a job announcement for position ID 1

Claude calls `WriteJobDescription(positionId: 1)`. The MCP server calls Ollama internally and returns a fully written USAJobs-style narrative. Claude presents it in the chat window.

No agent code. No `Program.cs`. Claude Desktop handles the conversation, tool routing, and result rendering — the same tools that `HrMcp.Agent` called in Part 4 are now accessible to a full AI host.

---

## Step 4 — VS Code Copilot Chat Integration

VS Code Copilot Chat also supports MCP servers via a workspace-level config file. Create `.vscode/mcp.json` at the solution root:

```json
{
  "servers": {
    "hr-mcp": {
      "type": "http",
      "url": "http://localhost:5100/mcp"
    }
  }
}
```

VS Code uses the HTTP transport (not stdio), so the MCP server must be running before you open Copilot Chat. Start it with:

```bash
dotnet run --project src/HrMcp.McpServer
```

In VS Code, open Copilot Chat, switch to **Agent** mode, and type `@hr-mcp` to invoke the server. The HR tools appear in the tool picker.

> **Note:** The `.vscode/mcp.json` format is specific to VS Code's MCP extension. It is separate from `claude_desktop_config.json`.

---

## Debugging

### Use MCP Inspector first

Before digging into logs, test the server in HTTP mode with MCP Inspector:

```bash
dotnet run --project src/HrMcp.McpServer
npx @modelcontextprotocol/inspector http://localhost:5100/mcp
```

If the tools appear and work in MCP Inspector, the server is healthy. Any problem is in the Claude Desktop stdio transport or configuration — not in the tool logic. This isolates the failure domain immediately.

### Common problems

**Tools not showing in Claude Desktop (no hammer icon)**

- The most likely cause is a wrong path in `claude_desktop_config.json`. Verify the path to the `.exe` is correct and the file exists at that location.
- Claude Desktop was not fully quit and relaunched. Use the system tray Quit option, not just the window close button.
- The config file has a JSON syntax error. Validate it with a JSON linter.

**Hammer icon present but tools fail silently**

- SQL Server is not running or the connection string is wrong. Start SQL Server and verify the `DefaultConnection` in `appsettings.json`.
- The database has not been migrated. Run `dotnet ef database update --project src/HrMcp.McpServer` once manually.

**Garbled output / JSON parse errors in Claude Desktop**

- stdout contamination. Something is writing to stdout in stdio mode. Check that `--stdio` is in the `args` array in the config and that `Program.cs` is clearing log providers when `isStdio` is true.
- A third-party NuGet package may be writing to stdout during startup. Add `Console.SetOut(TextWriter.Null)` as a last resort after `builder.Logging.ClearProviders()`.

**`WriteJobDescription` times out or returns empty**

- Ollama is not running. Start it with `ollama serve` and confirm `llama3.2` is pulled (`ollama list`).
- The tool calls Ollama on `http://localhost:11434`. If Ollama is on a different host or port, update the URI in `McpServer/Program.cs`.

**Server crashes immediately on launch**

- Check the MCP log file for the crash reason.
  - Windows: `%APPDATA%\Claude\logs\mcp-server-hr-mcp.log`
  - macOS: `~/Library/Logs/Claude/mcp-server-hr-mcp.log`
- Common crash causes: missing runtime (use `--self-contained true` at publish time), missing `appsettings.json` next to the exe, or a startup exception in the migration block.

---

## What We Built

Across the five parts of this series:

**Part 1 — Clean Architecture Foundation**
- `HrMcp.Core` — domain entities: `Position`, `HiringOrganization`, `PositionRemuneration`
- `HrMcp.Application` — `PositionService`, `HiringOrganizationService`
- `HrMcp.Infrastructure.Persistence` — EF Core, SQL Server, `DbSeeder` with USAJobs data

**Part 2 — Model Context Protocol Concepts**
- N×M integration problem and how MCP solves it
- Tools, Resources, Prompts primitives
- stdio vs HTTP/SSE transports

**Part 3 — MCP Server**
- `HrMcp.McpServer` — five tools registered with `[McpServerToolType]` and `[McpServerTool]`
- Dual transport: stdio for desktop clients, HTTP/SSE for programmatic clients
- Verified with MCP Inspector

**Part 4 — AI Agent**
- `HrMcp.Agent` — console agent using `IChatClient` (Microsoft.Extensions.AI) + `OllamaApiClient` (OllamaSharp)
- `UseFunctionInvocation` middleware — automatic tool dispatch
- `WriteJobDescription` upgraded from static stub to Ollama-generated narrative

**Part 5 — Claude Desktop Integration**
- Self-contained `HrMcp.McpServer.exe` published with `dotnet publish`
- `claude_desktop_config.json` configured for stdio transport
- `.vscode/mcp.json` for VS Code Copilot Chat
- End-to-end demo: HR tools callable from Claude Desktop

---

## Next Steps

The server is working end-to-end. Before exposing it beyond localhost, consider these next steps:

**Part 6 — Securing the MCP Server with OIDC**  
The HTTP/SSE endpoint on `http://localhost:5100/mcp` is unauthenticated. Part 6 adds JWT Bearer middleware to `McpServer/Program.cs`, configures an OIDC provider (Okta free tier, Duende IdentityServer, or Azure Entra), and updates the agent to acquire tokens via the client credentials flow before connecting. The `.RequireAuthorization()` call on `MapMcp` is the only server-side change.

**Additional directions to explore:**
- Replace Ollama with Azure OpenAI or Claude API for production-grade LLM output — only three lines change in `Program.cs`
- Add a `Resources` endpoint to expose job postings as MCP resources (documents the model can read without explicit tool calls)
- Add `Prompts` to pre-define common HR tasks as reusable prompt templates
- Package the MCP server as a Docker container for cloud deployment

---

## Sources

- [Model Context Protocol — Official Docs](https://modelcontextprotocol.io)
- [ModelContextProtocol C# SDK — GitHub](https://github.com/modelcontextprotocol/csharp-sdk)
- [MCP Inspector — GitHub](https://github.com/modelcontextprotocol/inspector)
- [Claude Desktop — Download](https://claude.ai/download)
- [VS Code MCP Extension Docs](https://code.visualstudio.com/docs/copilot/chat/mcp-servers)
- [Microsoft.Extensions.AI — NuGet](https://www.nuget.org/packages/Microsoft.Extensions.AI)
- [OllamaSharp — NuGet](https://www.nuget.org/packages/OllamaSharp)
- [dotnet publish — Microsoft Docs](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish)
