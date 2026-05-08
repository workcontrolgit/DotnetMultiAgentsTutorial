# Part 3 — Building the HR Data MCP Server

*Part 3 of: Building Multi-Agent Systems with .NET 10*

---

The HR data MCP server (`Hr.Jobs.Mcp`) is the workhorse of the system. It exposes 9 tools across three categories: position queries, job description generation, and announcement persistence. It runs on port 5100, and every specialist agent that touches HR data connects to it.

This post walks through how it is built — from project setup through tool definitions — and shows how to test every tool with MCP Inspector before writing a single line of orchestrator code.

---

## Project Setup

The server is an ASP.NET Core application with three NuGet packages beyond the standard SDK:

```xml
<!-- src/Hr.Jobs.Mcp/Hr.Jobs.Mcp.csproj -->
<PackageReference Include="ModelContextProtocol" Version="1.*" />
<PackageReference Include="OllamaSharp" Version="5.*" />
<PackageReference Include="Serilog.AspNetCore" Version="9.*" />
```

`ModelContextProtocol` provides `[McpServerToolType]`, `[McpServerTool]`, and `.AddMcpServer()`. `OllamaSharp` provides the `IChatClient` used inside `WriteJobDescription`. Serilog handles structured logging with a file sink (stdout is suppressed in stdio mode to keep the JSON-RPC channel clean).

The MCP server is registered in `Program.cs`:

```csharp
builder.Services
    .AddMcpServer()
    .WithTools<PositionTools>()
    .WithTools<HiringOrganizationTools>()
    .WithTools<JobDescriptionTools>()
    .WithTools<JobAnnouncementTools>()
    .WithHttpTransport();
```

Each tool class is registered separately. MCP Inspector and any MCP client will see all 9 tools from all 4 classes as a flat list.

---

## Tool Class Pattern

Every tool class follows the same pattern: a record with constructor-injected services and methods decorated with `[McpServerTool]`:

```csharp
[McpServerToolType]
public sealed class PositionTools(PositionService positions)
{
    [McpServerTool(Name = "GetOpenPositions"),
     Description("Returns all currently open federal job positions.")]
    public async Task<string> GetOpenPositions(CancellationToken ct = default)
    {
        var all = await positions.GetOpenPositionsAsync(ct);
        // format and return
    }
}
```

`[McpServerToolType]` marks the class for discovery. `[McpServerTool]` marks individual methods. The `Description` attribute text is what the LLM reads when deciding whether to call the tool — write it for the model, not for humans.

Parameter descriptions matter equally:

```csharp
[McpServerTool(Name = "GetPositionById"),
 Description("Returns full details for a specific position including duties, qualifications, pay grade, and salary.")]
public async Task<string> GetPositionById(
    [Description("The numeric database ID of the position")] int positionId,
    CancellationToken ct = default)
```

When the LLM sees `"The numeric database ID of the position"` it knows not to pass a title string.

---

## WriteJobDescription: An LLM Tool Inside an MCP Server

`WriteJobDescription` is unusual: it is a tool that internally calls a language model. The MCP server is both a tool server (from the orchestrator's perspective) and an LLM client (from Ollama's perspective).

```csharp
[McpServerToolType]
public sealed class JobDescriptionTools(PositionService positions, IChatClient chatClient)
{
    [McpServerTool(Name = "WriteJobDescription"),
     Description("Generates a USAJobs-style job announcement using AI.")]
    public async Task<string> WriteJobDescription(
        [Description("The numeric ID of the position")] int positionId,
        CancellationToken ct = default)
    {
        var p = await positions.GetPositionByIdAsync(positionId, ct);
        if (p is null) return $"Position {positionId} not found.";

        var ladder = await BuildGradeLadderAsync(p, ct);

        var systemPrompt = """
            You are a senior federal HR specialist. Synthesize duties into 5–8 active-voice
            bullet points. Qualifications must state specialized experience at the next lower
            grade level. Never copy source text verbatim.
            """;

        var userPrompt = $"""
            Write a USAJobs announcement for:
            Title: {p.Title} | Series: {p.OccupationalSeries} | Grade: {p.PayGradeMin}–{p.PayGradeMax}
            Salary: ${p.PositionRemuneration?.MinimumRange:N0}–${p.PositionRemuneration?.MaximumRange:N0}
            ...
            {ladder}

            Sections: ## Summary | ## Duties | ## Qualifications Required | ## How to Apply
            """;

        var response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.System, systemPrompt),
             new ChatMessage(ChatRole.User, userPrompt)], ct);

        return response.Text ?? $"Unable to generate description for position {positionId}.";
    }
}
```

The `IChatClient` is registered as a singleton in `Program.cs`:

```csharp
services.AddSingleton<IChatClient>(
    new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.2"));
```

---

## Grade-Ladder Context: Quality Without Bloating the Prompt

When you have 100 IT Specialist positions at grades GS-07 through GS-14, which ones should inform the draft for a GS-12? The answer is at most three — and selecting the wrong ones wastes token budget.

`BuildGradeLadderAsync` implements a three-slot selection strategy:

```csharp
private async Task<string> BuildGradeLadderAsync(Position target, CancellationToken ct)
{
    var siblings = (await positions.GetPositionsBySeriesAsync(target.OccupationalSeries, ct))
        .Where(p => p.Id != target.Id && !string.IsNullOrWhiteSpace(p.PayGradeMin))
        .ToList();

    var byGrade = siblings
        .GroupBy(p => ParseGradeNumber(p.PayGradeMin))
        .Where(g => g.Key is not null)
        .ToDictionary(
            g => g.Key!.Value,
            g => g.OrderByDescending(p => Richness(p)).First()); // richest wins

    // Slot 1: next lower grade — the experience baseline
    var lowerKeys = byGrade.Keys.Where(g => g < targetGrade).ToList();
    var lowerGrade = lowerKeys.Count > 0 ? lowerKeys.Max() : 0;

    // Slot 2: next higher grade — the scope ceiling
    var higherKeys = byGrade.Keys.Where(g => g > targetGrade).ToList();
    var higherGrade = higherKeys.Count > 0 ? higherKeys.Min() : 0;

    // Slot 3: same-grade peer — only when target's own text is sparse
    var isTargetSparse = Richness(target) < SparseThreshold;
    ...
}
```

"Richness" is the combined character count of `Duties + Qualifications`. Among all GS-11 positions in the series, the one with the most content wins the slot. Each slot's text is then truncated to a fixed character limit before entering the prompt. The result: 100 sibling positions contribute at most three carefully chosen excerpts — predictable token budget regardless of database size.

---

## OIDC Feature Flag

The server supports two modes controlled by `appsettings.json`:

```json
{
  "Features": { "EnableOidc": false }
}
```

When `EnableOidc` is false (local development), the MCP endpoint is open. When true (production), JWT Bearer authentication is enforced via a Duende IdentityServer container:

```csharp
var enableOidc = builder.Configuration.GetValue<bool>("Features:EnableOidc");

if (enableOidc)
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Oidc:Authority"];
            options.Audience  = builder.Configuration["Oidc:Audience"];
        });
}

// Later:
var route = app.MapMcp("/mcp");
if (enableOidc)
    route.RequireAuthorization();
```

This means you can develop and test locally without a running identity server, and flip one setting to go to a secured production configuration.

---

## Testing with MCP Inspector

MCP Inspector is a browser-based tool that connects directly to any MCP server and lets you call tools interactively — no agent, no orchestrator, no code.

Start the HR server with OIDC disabled:

```bash
dotnet run --project src/Hr.Jobs.Mcp
```

Open MCP Inspector:

```bash
npx @modelcontextprotocol/inspector http://localhost:5100/mcp
```

You will see all 9 tools listed. Test the core workflow:

**1. List open positions**

Select `GetOpenPositions`, click Run. You get a JSON list of all open positions from the seeded database.

**2. Get a specific position**

Select `GetPositionById`, enter `positionId: 1`, click Run. Full detail including duties, qualifications, salary, and hiring organization.

**3. Generate a job description**

Select `WriteJobDescription`, enter `positionId: 1`, click Run. Wait 10–30 seconds for Ollama. You get a formatted markdown announcement with Summary, Duties, Qualifications, and How to Apply sections — synthesized from the position's actual data and grade-ladder context from sibling positions.

**4. Save the draft**

Select `SaveJobAnnouncement`, enter `positionId: 1` and paste the draft text from step 3, click Run. You get back: `Announcement saved. ID: 1 | Status: Draft | Generated: 2026-05-08 05:12 UTC`.

Test each tool before wiring up the orchestrator. Problems caught at this stage are easy to fix. Problems caught inside an agent conversation are much harder to diagnose.

---

## stdio Transport for Claude Desktop

The server also supports stdio transport, used by Claude Desktop:

```csharp
if (isStdio)
{
    hostBuilder.Services
        .AddMcpServer()
        .WithTools<PositionTools>()
        .WithTools<HiringOrganizationTools>()
        .WithTools<JobDescriptionTools>()
        .WithTools<JobAnnouncementTools>()
        .WithStdioServerTransport();
    ...
}
```

The `--stdio` flag triggers this path. In stdio mode, the Serilog console sink is suppressed (stdout carries only JSON-RPC) and all log output goes to file. Part 7 shows how Claude Desktop uses this mode.

---

**Next: Part 4 — The Compliance MCP Server: Deterministic Rules, Zero LLM**

[View the repository](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial)
