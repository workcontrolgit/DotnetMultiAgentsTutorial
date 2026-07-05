# Design: Blog Code Sync — Align 7 Posts with Current Codebase

**Date:** 2026-07-04  
**Scope:** Update 7 of 11 blog posts in `blogs/multi-agents/` to reflect the current state of the codebase after recent refactors.

---

## Problem Statement

Several blog posts contain code snippets, class names, constructor signatures, and configuration values that no longer match the actual source code. The primary sources of drift are:

1. New shared library projects (`Hr.ConsoleShared`, `Hr.Mcp.Shared`) extracted from duplicated code
2. `numCtx` parameter added to all agent constructors for context window control
3. `ChatOptionsFactory.Create()` utility replacing raw `new ChatOptions { Tools = [...] }` construction
4. `McpClientTransportFactory` + `McpServerDefinition` pattern replacing inline transport construction
5. Model changed from `llama3.2` to `gemma4` in all `appsettings.json` files
6. `ExportTools` added as a 5th tool class in `Hr.Jobs.Mcp/Program.cs`

---

## Scope

**Posts to update (7):** Parts 1, 2, 3, 6, 8, 9, 10  
**Posts to skip (4):** Preface, Part 4, Part 5, Part 7 — already accurate, no code snippets affected

---

## Implementation Approach

**Two-pass strategy:**

**Pass 1 — Canonical source reads**  
Before updating any blog, read the authoritative source for each shared concept once:
- `Hr.Mcp.Shared/Client/McpClientTransportFactory.cs` — canonical factory code
- `Hr.Mcp.Shared/Client/McpServerDefinition.cs` — canonical record definition
- `Hr.ConsoleShared/Ai/ChatOptionsFactory.cs` — canonical options factory
- Each orchestrator `Program.cs` — canonical `numCtx` extraction and agent instantiation patterns

**Pass 2 — Per-post updates (parallel)**  
Apply canonical forms + post-specific fixes to each of the 7 posts simultaneously.

---

## Shared Canonical Fixes

These patterns appear in multiple posts and must be applied consistently.

### 1. Transport factory pattern (Parts 1, 6, 8, 9, 10)

Replace inline transport construction with:

```csharp
var hrServer = new McpServerDefinition("Hr", "McpServers:Hr",
    configuration["McpServers:Hr:Transport:Type"] ?? "stdio");

await using var hrMcpClient = await McpClient.CreateAsync(
    await McpClientTransportFactory.CreateAsync(configuration, hrServer));
```

`McpServerDefinition` is a record with three fields: `Name`, `ConfigPath`, `TransportType`. `McpClientTransportFactory.CreateAsync` reads the transport type from config and constructs either `StdioClientTransport` or `HttpClientTransport` (StreamableHttp mode).

### 2. `numCtx` parameter (Parts 6, 8, 9, 10)

All agent constructors include `int? numCtx` as the last parameter:

```csharp
int? numCtx = int.TryParse(configuration["AI:Ollama:NumCtx"], out var parsed) ? parsed : null;

new SpecialistAgent(
    name: "PositionSearch",
    systemPrompt: "...",
    chatClient: agentClient,
    tools: positionTools,
    numCtx: numCtx)
```

### 3. `ChatOptionsFactory.Create()` (Parts 6, 8, 9, 10)

Replace any raw `ChatOptions` construction with:

```csharp
var options = ChatOptionsFactory.Create([tool1, tool2], numCtx);
```

This utility is in the `Hr.ConsoleShared.Ai` namespace.

---

## Per-Post Specific Changes

### Part 1 — The .NET Agent Framework

**Changes:**
- Add new subsection *"Transport Abstraction"* after the `HttpClientTransport` example (~100 words + factory snippet)
- Explain `McpServerDefinition` record and `McpClientTransportFactory` factory
- Update `SpecialistAgent` constructor call to include `numCtx`

**New subsection content:**
The transport abstraction allows orchestrators to switch between stdio (MCP server spawned as child process) and streamHttp (pre-running server over HTTP) via a single config value. The `McpClientTransportFactory` reads `McpServers:{Name}:Transport:Type` from `appsettings.json` and constructs the appropriate `IClientTransport`.

### Part 2 — Clean Architecture for AI Applications

**Changes:**
- Add new subsection *"Shared Infrastructure Libraries"* introducing `Hr.ConsoleShared` and `Hr.Mcp.Shared`

**Content:**
- `Hr.ConsoleShared` — console rendering helpers: `StartupBannerWriter`, `ExportFileSaver`, `ChatOptionsFactory`
- `Hr.Mcp.Shared` — MCP client infrastructure: `McpClientTransportFactory`, `McpServerDefinition`, `WorkspaceRootLocator`, `PortConflictHelper`
- Both projects are referenced by all orchestrators and the agent to avoid duplication

### Part 3 — Building the HR Data MCP Server

**Changes:**
- Add `.WithTools<ExportTools>()` as the 3rd registration in the `Program.cs` tool registration snippet
- Update model name from `llama3.2` to `gemma4` in any `appsettings.json` examples
- Note that `ExportTools` provides `ExportPositionToWord`, `ExportDraftToWord`, `ExportPositionsToExcel`

### Part 6 — The Selector Pattern

**Changes:**
- Update all 5 `SpecialistAgent` constructor calls to include `numCtx`
- Update `AgentRouter` system prompt text to match actual code (minor wording)
- Show `ChatOptionsFactory.Create()` usage where agent options are created
- Add `numCtx` extraction from config at the top of the code example

### Part 8 — The Pipe Pattern

**Changes:**
- Update `DraftAgent` and `ComplianceAgent` constructor calls to include `numCtx`
- Update `HrPipeline` constructor call to include `numCtx`
- Replace raw `ChatOptions` with `ChatOptionsFactory.Create()` where shown

### Part 9 — The Group Chat Pattern

**Changes:**
- Update all `ReviewerAgent` constructor calls to include `numCtx`
- Correct all 3 expert system prompt texts to match actual `Program.cs` (HR Specialist, Legal Reviewer, Budget Analyst)
- Update `ReviewAsync` signature to include `CancellationToken ct`
- Replace raw `ChatOptions` with `ChatOptionsFactory.Create()` where shown

### Part 10 — The Evaluator-Optimizer Pattern

**Changes:**
- Update `GeneratorAgent` and `EvaluatorAgent` constructor calls to include `numCtx`
- Update `EvaluatorOptimizerLoop` constructor call to include `numCtx`
- Replace raw `ChatOptions` with `ChatOptionsFactory.Create()` where shown

---

## Constraints

- Do NOT change blog narrative, pattern explanations, or section structure
- Do NOT update posts 4, 5, 7, or the preface
- All code snippets must compile against the current codebase
- Model name `gemma4` (not `gemma4:latest` — keep it short in blog context)
- Keep blog tone and Medium.com formatting conventions (no markdown tables in prose)

---

## Files Modified

```
blogs/multi-agents/part-1-dotnet-agent-framework.md
blogs/multi-agents/part-2-clean-architecture-for-ai.md
blogs/multi-agents/part-3-hr-data-mcp-server.md
blogs/multi-agents/part-6-selector-pattern.md
blogs/multi-agents/part-8-pipe-pattern.md
blogs/multi-agents/part-9-group-chat-pattern.md
blogs/multi-agents/part-10-evaluator-optimizer-pattern.md
```

## Source Files Referenced

```
DotnetMultiAgents/src/Hr.Mcp.Shared/Client/McpClientTransportFactory.cs
DotnetMultiAgents/src/Hr.Mcp.Shared/Client/McpServerDefinition.cs
DotnetMultiAgents/src/Hr.ConsoleShared/Ai/ChatOptionsFactory.cs
DotnetMultiAgents/src/Hr.ConsoleShared/Exports/ExportFileSaver.cs
DotnetMultiAgents/src/Hr.SelectorOrchestrator/Program.cs
DotnetMultiAgents/src/Hr.SelectorOrchestrator/Agents/SpecialistAgent.cs
DotnetMultiAgents/src/Hr.SelectorOrchestrator/Orchestration/AgentRouter.cs
DotnetMultiAgents/src/Hr.PipeOrchestrator/Program.cs
DotnetMultiAgents/src/Hr.PipeOrchestrator/Agents/DraftAgent.cs
DotnetMultiAgents/src/Hr.PipeOrchestrator/Agents/ComplianceAgent.cs
DotnetMultiAgents/src/Hr.PipeOrchestrator/Pipeline/HrPipeline.cs
DotnetMultiAgents/src/Hr.GroupChatOrchestrator/Program.cs
DotnetMultiAgents/src/Hr.GroupChatOrchestrator/Agents/ReviewerAgent.cs
DotnetMultiAgents/src/Hr.GroupChatOrchestrator/Chat/HrGroupChat.cs
DotnetMultiAgents/src/Hr.EvaluatorOrchestrator/Program.cs
DotnetMultiAgents/src/Hr.EvaluatorOrchestrator/Agents/GeneratorAgent.cs
DotnetMultiAgents/src/Hr.EvaluatorOrchestrator/Agents/EvaluatorAgent.cs
DotnetMultiAgents/src/Hr.EvaluatorOrchestrator/Loop/EvaluatorOptimizerLoop.cs
DotnetMultiAgents/src/Hr.Jobs.Mcp/Program.cs
```
