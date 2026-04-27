# Part 3: Building an MCP Server in .NET 10

**Series:** AI Agents & MCP with .NET 10 | **Part 3 of 5**  
**GitHub:** [workcontrolgit/DotnetAiAgentMcp](https://github.com/workcontrolgit/DotnetAiAgentMcp)

---

## Introduction

In Part 2 we built the mental model for MCP. In this part we make it real.

By the end of this post you will have a running MCP server that exposes four tools over HTTP/SSE, supports stdio transport for Claude Desktop, and can be tested interactively with MCP Inspector — without any AI host involved.

The server lives in `HrMcp.McpServer`, the project we scaffolded in Part 1. All we are adding is the MCP SDK and three tool classes.

![Part 3 steps overview — swim lane across Developer, MCP Server, and MCP Inspector](diagrams/part-3-diagram-1-build-steps.png)

---

## Step 1 — Install the SDK

Add the official ASP.NET Core MCP package to `HrMcp.McpServer`:

```bash
dotnet add src/HrMcp.McpServer package ModelContextProtocol.AspNetCore --version 1.*
```

This pulls in two transitive packages automatically:

- **`ModelContextProtocol`** — core protocol, types, attributes
- **`ModelContextProtocol.Core`** — low-level JSON-RPC primitives

You only need to reference `ModelContextProtocol.AspNetCore` directly. The rest comes along for free.

---

## Step 2 — Tool Classes (`HrMcp.McpServer/Tools/`)

Create the `Tools/` folder inside `HrMcp.McpServer`. Each class carries a `[McpServerToolType]` attribute; each method that becomes an MCP tool carries `[McpServerTool]`. The `[Description]` attributes are what the AI reads to understand when and how to call each tool — write them as if you are explaining the tool to a developer who has never seen your schema.

```text
src/HrMcp.McpServer/
  Tools/
    PositionTools.cs
    HiringOrganizationTools.cs
    JobDescriptionTools.cs
```

### `PositionTools.cs`

Three tools: list open positions, get a single position by ID, and filter by organization.

```csharp
// src/HrMcp.McpServer/Tools/PositionTools.cs
using System.ComponentModel;
using HrMcp.Application.Services;
using ModelContextProtocol.Server;

namespace HrMcp.McpServer.Tools;

[McpServerToolType]
public sealed class PositionTools(PositionService positions)
{
    [McpServerTool(Name = "GetOpenPositions"),
     Description("Returns all currently open federal job positions including title, pay grade, duty location, and security clearance requirements.")]
    public async Task<IEnumerable<object>> GetOpenPositions(CancellationToken ct = default)
    {
        var list = await positions.GetOpenPositionsAsync(ct);
        return list.Select(p => (object)new
        {
            p.Id,
            p.Title,
            p.Description,
            p.OccupationalSeries,
            p.PayGradeMin,
            p.PayGradeMax,
            MinimumRange     = p.PositionRemuneration?.MinimumRange,
            MaximumRange     = p.PositionRemuneration?.MaximumRange,
            RateIntervalCode = p.PositionRemuneration?.RateIntervalCode,
            p.DutyLocation,
            p.TeleworkEligible,
            SecurityClearance  = p.SecurityClearance.ToString(),
            p.WhoMayApply,
            OrganizationName   = p.HiringOrganization?.OrganizationName,
            DepartmentName     = p.HiringOrganization?.DepartmentName
        });
    }

    [McpServerTool(Name = "GetPositionById"),
     Description("Returns full details for a specific position by its ID, including duties, qualifications, and pay information.")]
    public async Task<object?> GetPositionById(
        [Description("The numeric ID of the position to retrieve")] int positionId,
        CancellationToken ct = default)
    {
        var p = await positions.GetPositionByIdAsync(positionId, ct);
        if (p is null) return null;

        return new
        {
            p.Id,
            p.Title,
            p.Description,
            p.Duties,
            p.Qualifications,
            p.OccupationalSeries,
            p.PayGradeMin,
            p.PayGradeMax,
            MinimumRange         = p.PositionRemuneration?.MinimumRange,
            MaximumRange         = p.PositionRemuneration?.MaximumRange,
            RateIntervalCode     = p.PositionRemuneration?.RateIntervalCode,
            p.IsOpen,
            p.DutyLocation,
            p.TeleworkEligible,
            SecurityClearance    = p.SecurityClearance.ToString(),
            TravelRequired       = p.TravelRequired.ToString(),
            AppointmentType      = p.AppointmentType.ToString(),
            WorkSchedule         = p.WorkSchedule.ToString(),
            p.WhoMayApply,
            p.SupervisoryStatus,
            p.RelocationAuthorized,
            p.DrugTestRequired,
            OpenDate             = p.OpenDate.ToString("yyyy-MM-dd"),
            CloseDate            = p.CloseDate?.ToString("yyyy-MM-dd"),
            OrganizationName     = p.HiringOrganization?.OrganizationName,
            DepartmentName       = p.HiringOrganization?.DepartmentName
        };
    }

    [McpServerTool(Name = "GetPositionsByOrganization"),
     Description("Returns all positions for a specific federal hiring organization. Use GetHiringOrganizations first to get valid organization IDs.")]
    public async Task<IEnumerable<object>> GetPositionsByOrganization(
        [Description("The numeric ID of the hiring organization")] int organizationId,
        CancellationToken ct = default)
    {
        var list = await positions.GetPositionsByOrganizationAsync(organizationId, ct);
        return list.Select(p => (object)new
        {
            p.Id,
            p.Title,
            p.Description,
            p.OccupationalSeries,
            p.PayGradeMin,
            p.PayGradeMax,
            p.IsOpen,
            p.DutyLocation,
            p.TeleworkEligible,
            SecurityClearance = p.SecurityClearance.ToString(),
            p.WhoMayApply
        });
    }
}
```

A few design notes:

- **Return anonymous projections, not entities.** Domain entities have navigation properties that create circular references and bloat JSON. Project only the fields the AI needs.
- **Enums as strings.** `p.SecurityClearance.ToString()` gives the AI `"Secret"` instead of `3`. Self-explanatory values require no schema lookup.
- **`CancellationToken` as the last parameter.** The SDK passes the request cancellation token automatically when a method accepts it. Always include it for async tools.

### `HiringOrganizationTools.cs`

One tool: list all hiring organizations with open position counts.

```csharp
// src/HrMcp.McpServer/Tools/HiringOrganizationTools.cs
using System.ComponentModel;
using HrMcp.Application.Services;
using ModelContextProtocol.Server;

namespace HrMcp.McpServer.Tools;

[McpServerToolType]
public sealed class HiringOrganizationTools(HiringOrganizationService organizations)
{
    [McpServerTool(Name = "GetHiringOrganizations"),
     Description("Returns all federal hiring organizations in the database with their department affiliations, IDs, and open position count.")]
    public async Task<IEnumerable<object>> GetHiringOrganizations(CancellationToken ct = default)
    {
        var list = await organizations.GetAllOrganizationsAsync(ct);
        return list.Select(o => (object)new
        {
            o.Id,
            o.OrganizationName,
            o.DepartmentName,
            o.AgencyDescription,
            OpenPositionCount = o.Positions.Count(p => p.IsOpen)
        });
    }
}
```

### `JobDescriptionTools.cs`

One tool: generate a USAJobs-format job description. In this part it returns a structured template. In Part 4 we replace the template with real LLM output.

```csharp
// src/HrMcp.McpServer/Tools/JobDescriptionTools.cs
using System.ComponentModel;
using HrMcp.Application.Services;
using ModelContextProtocol.Server;

namespace HrMcp.McpServer.Tools;

[McpServerToolType]
public sealed class JobDescriptionTools(PositionService positions)
{
    [McpServerTool(Name = "WriteJobDescription"),
     Description("Generates a formatted USAJobs-style job description for the specified position. Returns a structured template in Part 3; upgraded to LLM-generated narrative in Part 4.")]
    public async Task<string> WriteJobDescription(
        [Description("The numeric ID of the position to write a description for")] int positionId,
        CancellationToken ct = default)
    {
        var p = await positions.GetPositionByIdAsync(positionId, ct);
        if (p is null) return $"Position {positionId} not found.";

        return $"""
            ## {p.Title}

            **Department:** {p.HiringOrganization?.DepartmentName}
            **Organization:** {p.HiringOrganization?.OrganizationName}
            **Series & Grade:** {p.OccupationalSeries} | {p.PayGradeMin}–{p.PayGradeMax}
            **Salary:** ${p.PositionRemuneration?.MinimumRange:N0} – ${p.PositionRemuneration?.MaximumRange:N0} per year
            **Location:** {p.DutyLocation}
            **Telework:** {(p.TeleworkEligible ? "Eligible" : "Not eligible")}
            **Security Clearance:** {p.SecurityClearance}
            **Who May Apply:** {p.WhoMayApply}

            ### Summary
            {p.Description}

            ### Duties
            {p.Duties}

            ### Qualifications
            {p.Qualifications}

            ---
            *[Stub — LLM-generated narrative added in Part 4]*
            """;
    }
}
```

---

## Step 3 — Update `Program.cs`

The updated `Program.cs` handles both transports from a single binary. The `--stdio` flag switches the server into stdio mode, which is required by Claude Desktop and VS Code Copilot Chat.

```csharp
// src/HrMcp.McpServer/Program.cs
using HrMcp.Application.Services;
using HrMcp.Infrastructure.Persistence;
using HrMcp.McpServer.Tools;
using Microsoft.EntityFrameworkCore;

var isStdio = args.Contains("--stdio");

var builder = WebApplication.CreateBuilder(args);

// Stdout must contain only JSON-RPC when running as a stdio server.
// Clear all log providers so nothing leaks into stdout.
if (isStdio)
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = Microsoft.Extensions.Logging.LogLevel.Trace);
    builder.WebHost.UseUrls(); // no HTTP listener in stdio mode
}

builder.Services.AddPersistence(
    builder.Configuration.GetConnectionString("DefaultConnection")!);
builder.Services.AddScoped<PositionService>();
builder.Services.AddScoped<HiringOrganizationService>();

var mcp = builder.Services
    .AddMcpServer()
    .WithTools<PositionTools>()
    .WithTools<HiringOrganizationTools>()
    .WithTools<JobDescriptionTools>();

if (isStdio)
    mcp.WithStdioServerTransport();
else
    mcp.WithHttpTransport();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
    db.Database.Migrate();

    // Looks for data/usajobs-seed.json in the working directory (solution root when using dotnet run)
    var seedPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "usajobs-seed.json");
    DbSeeder.Seed(db, seedPath);
}

if (!isStdio)
    app.MapMcp("/mcp");

await app.RunAsync();
```

**What changed from Part 1:**

- `--stdio` flag detection at startup
- Logging cleared and redirected to stderr in stdio mode (`UseUrls()` prevents Kestrel binding)
- `AddMcpServer()` with tool registrations
- `WithStdioServerTransport()` or `WithHttpTransport()` depending on the flag
- `app.MapMcp("/mcp")` wires the HTTP/SSE endpoint when running in HTTP mode

---

## Step 4 — Build

```bash
dotnet build DotnetAiAgentMcp.slnx   # 0 errors, 0 warnings
```

---

## Step 5 — Run in HTTP Mode

```bash
dotnet run --project src/HrMcp.McpServer
```

The server starts on `http://localhost:5100`. You will see the usual ASP.NET Core startup output, ending with:

```text
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5100
```

---

## Step 6 — Testing with MCP Inspector — Your MCP Postman + Swagger

If you have built REST APIs with .NET, you know this workflow:

- **Swagger UI** — start the server, open the browser, see all endpoints auto-discovered, click to call one
- **Postman** — send a request with custom inputs, inspect the raw JSON response

MCP Inspector does both in one UI for MCP servers. It connects to your running server, discovers all tools, and lets you call them with custom arguments and inspect the raw JSON-RPC exchange.

> **Prerequisite:** Node.js 22.7.5 or later. Check with `node --version`.

With the server running in HTTP mode, open a second terminal and run:

```bash
npx @modelcontextprotocol/inspector http://localhost:5100/mcp
```

The inspector starts on `http://localhost:6274`. Open it in a browser.

### What you will see

The **Tools** tab lists all five tools auto-discovered from the server. Click any tool to see its description and a **Run Tool** button. The right panel shows the live JSON-RPC response.

![MCP Inspector connected to HrMcp.McpServer — all 5 tools listed, GetHiringOrganizations result shown](diagrams/part-3-diagram-2-inspector-tools.png)

### Calling `GetHiringOrganizations`

Click `GetHiringOrganizations` → **Run Tool**. Expected response:

```json
[
  {
    "id": 1,
    "organizationName": "U.S. Citizenship and Immigration Services",
    "departmentName": "Department of Homeland Security",
    "agencyDescription": "",
    "openPositionCount": 4
  },
  ...
]
```

6 organizations returned (from the real DHS seed data).

### Calling `GetOpenPositions`

Click `GetOpenPositions` → **Run Tool**. Returns all positions where `IsOpen = true` with pay ranges and clearance levels.

### Calling `GetPositionsByOrganization`

Click `GetPositionsByOrganization`, enter `organizationId: 1` → **Run Tool**. Returns only positions belonging to organization 1.

### Calling `GetPositionById`

Click `GetPositionById`, enter `positionId: 1` → **Run Tool**. Returns full position detail including duties and qualifications.

### Calling `WriteJobDescription`

Click `WriteJobDescription`, enter `positionId: 1` → **Run Tool**. Returns the structured Markdown template:

```text
## IT Specialist (SYSADMIN)

**Department:** Department of Homeland Security
**Organization:** U.S. Citizenship and Immigration Services
...
*[Stub — LLM-generated narrative added in Part 4]*
```

All four tools respond correctly. The server is working.

> **Tip:** If tools are not appearing or calls are failing, check the inspector first before touching the stdio transport. Inspector runs in HTTP mode — if it works here, the server is correct and any Claude Desktop issues are stdio-specific.

---

## Step 7 — Verify stdio Mode

Stop the server. Run in stdio mode:

```bash
dotnet run --project src/HrMcp.McpServer -- --stdio
```

No ASP.NET Core startup output appears on stdout. The process blocks waiting for JSON-RPC input on stdin — which is exactly the behaviour Claude Desktop expects. Press `Ctrl+C` to exit.

---

## What We Built

- **`ModelContextProtocol.AspNetCore` 1.2.0** installed in `HrMcp.McpServer`
- **3 tool classes** — `PositionTools` (3 tools), `HiringOrganizationTools` (1 tool), `JobDescriptionTools` (1 tool)
- **`--stdio` flag** — single binary, two transports, no code duplication
- **Verified with MCP Inspector** — all 4 tools discovered and callable against real DHS data
- **`WriteJobDescription`** returns a structured stub, ready for LLM upgrade in Part 4

The AI still knows nothing about any of this. In Part 4, we wire in an LLM via `Microsoft.Extensions.AI` and Ollama, let it call these tools, and replace the `WriteJobDescription` stub with a real generated narrative.

---

## Next Up

**[Part 4: AI Agent with Microsoft.Extensions.AI + Ollama →](part-4-ai-agent-extensions-ai.md)**

We build the `HrMcp.Agent` console app: connect it to the MCP server, register Ollama as the chat client, and let the AI call `GetOpenPositions`, `GetHiringOrganizations`, and `WriteJobDescription` in a live conversation.

---

## Sources

- [ModelContextProtocol C# SDK — GitHub](https://github.com/modelcontextprotocol/csharp-sdk)
- [ModelContextProtocol NuGet Package](https://www.nuget.org/packages/ModelContextProtocol)
- [ModelContextProtocol.AspNetCore NuGet Package](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore)
- [MCP Inspector — GitHub](https://github.com/modelcontextprotocol/inspector)
- [MCP Specification — Transports](https://spec.modelcontextprotocol.io/specification/basic/transports/)
