# Series 2B — Multi-Agent Patterns Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add three new orchestrator projects to the solution — `Hr.PipeOrchestrator`, `Hr.GroupChatOrchestrator`, and `Hr.EvaluatorOrchestrator` — each demonstrating a distinct multi-agent pattern using the existing MCP servers and domain layers.

**Architecture:** Each project is a standalone .NET 10 console app sharing `Hr.Jobs.Mcp` (port 5100) and `Hr.Compliance.Mcp` (port 5200). No OIDC — MCP servers must have `Features:EnableOidc=false`. All three follow the same bootstrap pattern as `Hr.SelectorOrchestrator`: `OllamaApiClient` → `IChatClient`, `McpClient.CreateAsync(HttpClientTransport)`, tools cast as `AITool`.

**Tech Stack:** .NET 10, `OllamaSharp 5.*`, `Microsoft.Extensions.AI 10.*`, `ModelContextProtocol 1.*`, Ollama (`llama3.2`), SQL Server LocalDB.

**Working directory for all commands:** `c:/apps/DotnetMultiAgentsTutorial/DotnetMultiAgents`

---

## Part 8 — Pipe Pattern (`Hr.PipeOrchestrator`)

Three sequential stages: DraftAgent → ComplianceAgent → Status update. User confirms between each stage.

### Task 1: Create `Hr.PipeOrchestrator` project and add to solution

**Files:**
- Create: `src/Hr.PipeOrchestrator/Hr.PipeOrchestrator.csproj`
- Modify: `DotnetMultiAgents.slnx`

- [ ] **Step 1: Create the project file**

Create `src/Hr.PipeOrchestrator/Hr.PipeOrchestrator.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.AI" Version="10.*" />
    <PackageReference Include="OllamaSharp" Version="5.*" />
    <PackageReference Include="ModelContextProtocol" Version="1.*" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add project to solution**

In `DotnetMultiAgents.slnx`, add inside `<Folder Name="/src/">`:

```xml
<Project Path="src/Hr.PipeOrchestrator/Hr.PipeOrchestrator.csproj" />
```

- [ ] **Step 3: Restore packages**

```bash
dotnet restore
```

Expected: `Restore succeeded.`

---

### Task 2: Create `DraftAgent`

**Files:**
- Create: `src/Hr.PipeOrchestrator/Agents/DraftAgent.cs`

- [ ] **Step 1: Create `DraftAgent.cs`**

```csharp
// src/Hr.PipeOrchestrator/Agents/DraftAgent.cs
using Microsoft.Extensions.AI;

namespace Hr.PipeOrchestrator.Agents;

/// <summary>
/// Stage 1 of the HR pipeline. Calls WriteJobDescription then SaveJobAnnouncement.
/// Parses the saved announcement ID from the LLM reply.
/// </summary>
public sealed class DraftAgent(IChatClient chatClient, IReadOnlyList<AITool> tools)
{
    public async Task<(string Reply, int? AnnouncementId)> RunAsync(int positionId, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, """
                You are a federal HR writing specialist operating in an automated pipeline.
                When given a position ID:
                1. Call WriteJobDescription to generate the announcement draft.
                2. Call SaveJobAnnouncement with the position ID and the draft text.
                3. Include the saved announcement ID in your reply using this exact format on its own line:
                   ANNOUNCEMENT_ID:<id>
                Do not ask questions. Complete both tool calls before responding.
                """),
            new(ChatRole.User, $"Generate and save a job announcement draft for position ID {positionId}."),
        };

        var response = await chatClient.GetResponseAsync(
            messages, new ChatOptions { Tools = [.. tools] }, ct);

        var text = response.Text ?? string.Empty;
        return (text, ParseAnnouncementId(text));
    }

    private static int? ParseAnnouncementId(string text)
    {
        const string prefix = "ANNOUNCEMENT_ID:";
        var idx = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var token = text[(idx + prefix.Length)..].Trim().Split([' ', '\n', '\r'], 2)[0];
        return int.TryParse(token, out var id) ? id : null;
    }
}
```

---

### Task 3: Create `ComplianceAgent`

**Files:**
- Create: `src/Hr.PipeOrchestrator/Agents/ComplianceAgent.cs`

- [ ] **Step 1: Create `ComplianceAgent.cs`**

```csharp
// src/Hr.PipeOrchestrator/Agents/ComplianceAgent.cs
using Microsoft.Extensions.AI;

namespace Hr.PipeOrchestrator.Agents;

/// <summary>
/// Stage 2 of the HR pipeline. Runs RunFullComplianceCheck and returns
/// the full report plus a pass/fail flag parsed from a sentinel line.
/// </summary>
public sealed class ComplianceAgent(IChatClient chatClient, IReadOnlyList<AITool> tools)
{
    public async Task<(string Report, bool Passed)> RunAsync(int positionId, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, """
                You are a federal HR compliance specialist operating in an automated pipeline.
                Run RunFullComplianceCheck for the given position ID and report all results clearly.
                End your reply with exactly one of these lines (no extra text after it):
                COMPLIANCE_RESULT:PASSED
                COMPLIANCE_RESULT:FAILED
                """),
            new(ChatRole.User, $"Run a full OPM compliance check for position ID {positionId}."),
        };

        var response = await chatClient.GetResponseAsync(
            messages, new ChatOptions { Tools = [.. tools] }, ct);

        var text = response.Text ?? string.Empty;
        var passed = text.Contains("COMPLIANCE_RESULT:PASSED", StringComparison.OrdinalIgnoreCase);
        return (text, passed);
    }
}
```

---

### Task 4: Create `HrPipeline`

**Files:**
- Create: `src/Hr.PipeOrchestrator/Pipeline/HrPipeline.cs`

- [ ] **Step 1: Create `HrPipeline.cs`**

```csharp
// src/Hr.PipeOrchestrator/Pipeline/HrPipeline.cs
using Hr.PipeOrchestrator.Agents;
using Microsoft.Extensions.AI;

namespace Hr.PipeOrchestrator.Pipeline;

/// <summary>
/// Coordinates the three-stage HR announcement pipeline.
/// Pauses for user confirmation between stages (semi-automated).
///
/// Pattern: Pipe — each stage's output is the next stage's input.
/// No stage can be skipped; the user controls pace via y/n prompts.
/// </summary>
public sealed class HrPipeline(
    DraftAgent draftAgent,
    ComplianceAgent complianceAgent,
    IChatClient statusClient,
    AITool updateStatusTool)
{
    public async Task RunAsync(int positionId, CancellationToken ct = default)
    {
        Console.WriteLine($"\n{"",60}".Replace(' ', '='));
        Console.WriteLine($"  HR Pipeline — Position {positionId}");
        Console.WriteLine($"{"",60}".Replace(' ', '=') + "\n");

        // ── Stage 1: Draft ───────────────────────────────────────────
        PrintStageHeader(1, "Generating job announcement draft");
        var (draftReply, announcementId) = await draftAgent.RunAsync(positionId, ct);
        Console.WriteLine($"\n{draftReply}\n");

        if (announcementId is null)
            Console.WriteLine("[Warning] Could not extract announcement ID — Stage 3 status update will be skipped.");

        if (!Confirm("Continue to Stage 2 — Compliance Check?")) return;

        // ── Stage 2: Compliance ──────────────────────────────────────
        PrintStageHeader(2, "Running OPM compliance check");
        var (report, passed) = await complianceAgent.RunAsync(positionId, ct);
        Console.WriteLine($"\n{report}\n");

        if (!Confirm("Continue to Stage 3 — Update Status?")) return;

        // ── Stage 3: Update status ───────────────────────────────────
        PrintStageHeader(3, "Recording compliance outcome");

        if (announcementId is null)
        {
            Console.WriteLine("[Skipped] No announcement ID available — cannot update status.");
            return;
        }

        var statusLabel = passed ? "CompliancePassed" : "ComplianceFailed";
        var summary = passed
            ? "All OPM compliance rules passed. Announcement is ready for publication."
            : "One or more OPM compliance rules failed. Review the compliance report above.";

        var statusMessages = new List<ChatMessage>
        {
            new(ChatRole.System,
                "You are an HR status recorder. Call UpdateAnnouncementStatus immediately with the details given. Do not ask questions."),
            new(ChatRole.User,
                $"Update announcement {announcementId} to status {statusLabel} with summary: {summary}"),
        };

        var statusResponse = await statusClient.GetResponseAsync(
            statusMessages, new ChatOptions { Tools = [updateStatusTool] }, ct);

        Console.WriteLine($"\n{statusResponse.Text}\n");

        Console.ForegroundColor = passed ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine($"Pipeline complete. Final status: {statusLabel}");
        Console.ResetColor();
    }

    private static void PrintStageHeader(int stage, string description)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[Stage {stage}/3] {description}...");
        Console.ResetColor();
    }

    private static bool Confirm(string prompt)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{prompt} (y/n): ");
        Console.ResetColor();
        return Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true;
    }
}
```

---

### Task 5: Create `Program.cs` for `Hr.PipeOrchestrator`

**Files:**
- Create: `src/Hr.PipeOrchestrator/Program.cs`

- [ ] **Step 1: Create `Program.cs`**

```csharp
// src/Hr.PipeOrchestrator/Program.cs
using Hr.PipeOrchestrator.Agents;
using Hr.PipeOrchestrator.Pipeline;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OllamaSharp;

// ── MCP connections ──────────────────────────────────────────────────────────
var hrMcpUrl = Environment.GetEnvironmentVariable("HR_MCP_SERVER_URL")
    ?? "http://localhost:5100/mcp";
var complianceMcpUrl = Environment.GetEnvironmentVariable("COMPLIANCE_MCP_SERVER_URL")
    ?? "http://localhost:5200/compliance";

await using var hrMcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(hrMcpUrl) }));

await using var complianceMcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(complianceMcpUrl) }));

var hrTools         = (await hrMcpClient.ListToolsAsync()).Cast<AITool>().ToList();
var complianceTools = (await complianceMcpClient.ListToolsAsync()).Cast<AITool>().ToList();

Console.WriteLine($"HR tools:         {string.Join(", ", hrTools.Select(t => t.Name))}");
Console.WriteLine($"Compliance tools: {string.Join(", ", complianceTools.Select(t => t.Name))}\n");

// ── Chat client ──────────────────────────────────────────────────────────────
IChatClient chatClient = ((IChatClient)new OllamaApiClient(
        new Uri("http://localhost:11434"), "llama3.2"))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

// ── Tool subsets ─────────────────────────────────────────────────────────────
var draftTools = hrTools
    .Where(t => t.Name is "WriteJobDescription" or "GetPositionById" or "SaveJobAnnouncement")
    .ToList();

var complianceAgentTools = complianceTools
    .Where(t => t.Name == "RunFullComplianceCheck")
    .ToList();

var updateStatusTool = hrTools.First(t => t.Name == "UpdateAnnouncementStatus");

// ── User input ───────────────────────────────────────────────────────────────
Console.Write("Enter position ID to process: ");
if (!int.TryParse(Console.ReadLine(), out var positionId))
{
    Console.WriteLine("Invalid position ID. Exiting.");
    return;
}

// ── Run pipeline ─────────────────────────────────────────────────────────────
var pipeline = new HrPipeline(
    new DraftAgent(chatClient, draftTools),
    new ComplianceAgent(chatClient, complianceAgentTools),
    chatClient,
    updateStatusTool);

await pipeline.RunAsync(positionId);
```

---

### Task 6: Build and commit Part 8

**Files:** All files created in Tasks 1–5

- [ ] **Step 1: Build the solution**

```bash
dotnet build --no-restore -v q
```

Expected: `Build succeeded.` with `0 Error(s)`.

- [ ] **Step 2: Commit**

```bash
cd c:/apps/DotnetMultiAgentsTutorial
git add DotnetMultiAgents/DotnetMultiAgents.slnx DotnetMultiAgents/src/Hr.PipeOrchestrator/
git commit -m "feat: add Hr.PipeOrchestrator — Part 8 Pipe pattern"
```

---

## Part 9 — Group Chat Pattern (`Hr.GroupChatOrchestrator`)

Three specialist reviewers critique a draft in parallel, then a Moderator synthesizes a revised version.

### Task 7: Create `Hr.GroupChatOrchestrator` project and add to solution

**Files:**
- Create: `src/Hr.GroupChatOrchestrator/Hr.GroupChatOrchestrator.csproj`
- Modify: `DotnetMultiAgents.slnx`

- [ ] **Step 1: Create the project file**

Create `src/Hr.GroupChatOrchestrator/Hr.GroupChatOrchestrator.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.AI" Version="10.*" />
    <PackageReference Include="OllamaSharp" Version="5.*" />
    <PackageReference Include="ModelContextProtocol" Version="1.*" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add project to solution**

In `DotnetMultiAgents.slnx`, add inside `<Folder Name="/src/">`:

```xml
<Project Path="src/Hr.GroupChatOrchestrator/Hr.GroupChatOrchestrator.csproj" />
```

- [ ] **Step 3: Restore packages**

```bash
dotnet restore
```

Expected: `Restore succeeded.`

---

### Task 8: Create `ReviewerAgent`

**Files:**
- Create: `src/Hr.GroupChatOrchestrator/Agents/ReviewerAgent.cs`

All four chat roles (HR Specialist, Legal Reviewer, Budget Analyst, Moderator) share the same structure — one class, different system prompts.

- [ ] **Step 1: Create `ReviewerAgent.cs`**

```csharp
// src/Hr.GroupChatOrchestrator/Agents/ReviewerAgent.cs
using Microsoft.Extensions.AI;

namespace Hr.GroupChatOrchestrator.Agents;

/// <summary>
/// Single-turn agent used for both reviewers and the Moderator in the group chat.
/// Reviewers call ReviewAsync; the Moderator calls SynthesizeAsync.
/// No MCP tools — all agents reason over draft text passed in the prompt.
/// </summary>
public sealed class ReviewerAgent(string name, string systemPrompt, IChatClient chatClient)
{
    public string Name { get; } = name;

    /// <summary>Critiques the draft from this agent's specialist perspective.</summary>
    public async Task<string> ReviewAsync(string draftText, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, $"Review the following job announcement draft:\n\n{draftText}"),
        };

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        return response.Text ?? string.Empty;
    }

    /// <summary>
    /// Synthesizes multiple critiques into a revised draft.
    /// Used by the Moderator only.
    /// </summary>
    public async Task<string> SynthesizeAsync(
        string draftText,
        IReadOnlyList<(string ReviewerName, string Critique)> critiques,
        CancellationToken ct = default)
    {
        var critiqueBlock = string.Join("\n\n", critiques
            .Select(c => $"--- {c.ReviewerName} ---\n{c.Critique}"));

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, $"""
                Original draft:
                {draftText}

                Expert critiques:
                {critiqueBlock}

                Produce a revised draft that addresses all valid critique points.
                Return only the revised announcement text — no commentary, no preamble.
                """),
        };

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        return response.Text ?? string.Empty;
    }
}
```

---

### Task 9: Create `HrGroupChat`

**Files:**
- Create: `src/Hr.GroupChatOrchestrator/Chat/HrGroupChat.cs`

- [ ] **Step 1: Create `HrGroupChat.cs`**

```csharp
// src/Hr.GroupChatOrchestrator/Chat/HrGroupChat.cs
using Hr.GroupChatOrchestrator.Agents;
using Microsoft.Extensions.AI;

namespace Hr.GroupChatOrchestrator.Chat;

/// <summary>
/// Implements the Group Chat (Debate) pattern.
///
/// Round 1: Three specialists critique the draft in parallel via Task.WhenAll.
///          No reviewer sees another's feedback — eliminates anchoring bias.
/// Round 2: Moderator synthesizes all critiques into a revised draft.
/// The revised draft is saved via SaveJobAnnouncement after user confirmation.
/// </summary>
public sealed class HrGroupChat(
    ReviewerAgent hrSpecialist,
    ReviewerAgent legalReviewer,
    ReviewerAgent budgetAnalyst,
    ReviewerAgent moderator,
    IChatClient mcpClient,
    AITool getAnnouncementTool,
    AITool saveAnnouncementTool)
{
    public async Task RunAsync(int announcementId, int positionId, CancellationToken ct = default)
    {
        Console.WriteLine($"\n{"",60}".Replace(' ', '='));
        Console.WriteLine($"  HR Group Chat — Announcement {announcementId}");
        Console.WriteLine($"{"",60}".Replace(' ', '=') + "\n");

        // ── Load draft ───────────────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Loading draft from database...");
        Console.ResetColor();

        var loadMessages = new List<ChatMessage>
        {
            new(ChatRole.System,
                "Retrieve the job announcement with the given ID and return its full text verbatim. Do not add commentary."),
            new(ChatRole.User, $"Get job announcement ID {announcementId}."),
        };
        var loadResponse = await mcpClient.GetResponseAsync(
            loadMessages, new ChatOptions { Tools = [getAnnouncementTool] }, ct);
        var draftText = loadResponse.Text ?? string.Empty;

        Console.WriteLine($"\n--- Current Draft ---\n{draftText}\n");

        if (!Confirm("Start group review (3 specialists will critique in parallel)?")) return;

        // ── Round 1: Parallel debate ─────────────────────────────────
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n[Round 1] Running parallel expert review...");
        Console.ResetColor();

        var hrTask     = hrSpecialist.ReviewAsync(draftText, ct);
        var legalTask  = legalReviewer.ReviewAsync(draftText, ct);
        var budgetTask = budgetAnalyst.ReviewAsync(draftText, ct);
        await Task.WhenAll(hrTask, legalTask, budgetTask);

        PrintCritique(hrSpecialist.Name,  hrTask.Result);
        PrintCritique(legalReviewer.Name, legalTask.Result);
        PrintCritique(budgetAnalyst.Name, budgetTask.Result);

        if (!Confirm("Continue to Round 2 — Moderator synthesis?")) return;

        // ── Round 2: Synthesis ───────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n[Round 2] Moderator synthesizing critiques into revised draft...");
        Console.ResetColor();

        var critiques = new List<(string, string)>
        {
            (hrSpecialist.Name,  hrTask.Result),
            (legalReviewer.Name, legalTask.Result),
            (budgetAnalyst.Name, budgetTask.Result),
        };
        var revisedDraft = await moderator.SynthesizeAsync(draftText, critiques, ct);

        Console.WriteLine($"\n--- Revised Draft ---\n{revisedDraft}\n");

        if (!Confirm("Save revised draft to database?")) return;

        // ── Save revised draft ───────────────────────────────────────
        var saveMessages = new List<ChatMessage>
        {
            new(ChatRole.System,
                "Save the provided job announcement draft for the given position. Return only the save confirmation."),
            new(ChatRole.User,
                $"Save this announcement for position ID {positionId}:\n\n{revisedDraft}"),
        };
        var saveResponse = await mcpClient.GetResponseAsync(
            saveMessages, new ChatOptions { Tools = [saveAnnouncementTool] }, ct);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\nGroup chat complete. {saveResponse.Text}");
        Console.ResetColor();
    }

    private static void PrintCritique(string name, string critique)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"\n[{name}]");
        Console.ResetColor();
        Console.WriteLine(critique);
    }

    private static bool Confirm(string prompt)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{prompt} (y/n): ");
        Console.ResetColor();
        return Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true;
    }
}
```

---

### Task 10: Create `Program.cs` for `Hr.GroupChatOrchestrator`

**Files:**
- Create: `src/Hr.GroupChatOrchestrator/Program.cs`

- [ ] **Step 1: Create `Program.cs`**

```csharp
// src/Hr.GroupChatOrchestrator/Program.cs
using Hr.GroupChatOrchestrator.Agents;
using Hr.GroupChatOrchestrator.Chat;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OllamaSharp;

// ── MCP connection ────────────────────────────────────────────────────────────
var hrMcpUrl = Environment.GetEnvironmentVariable("HR_MCP_SERVER_URL")
    ?? "http://localhost:5100/mcp";

await using var hrMcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(hrMcpUrl) }));

var hrTools = (await hrMcpClient.ListToolsAsync()).Cast<AITool>().ToList();
Console.WriteLine($"HR tools: {string.Join(", ", hrTools.Select(t => t.Name))}\n");

// ── Reviewer client (no function invocation — reviewers don't call MCP tools) ─
IChatClient reviewerClient = ((IChatClient)new OllamaApiClient(
        new Uri("http://localhost:11434"), "llama3.2"))
    .AsBuilder()
    .Build();

// ── MCP client (needs function invocation for GetJobAnnouncement + Save) ──────
IChatClient mcpClient = ((IChatClient)new OllamaApiClient(
        new Uri("http://localhost:11434"), "llama3.2"))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var getAnnouncementTool  = hrTools.First(t => t.Name == "GetJobAnnouncement");
var saveAnnouncementTool = hrTools.First(t => t.Name == "SaveJobAnnouncement");

// ── Reviewer agents ───────────────────────────────────────────────────────────
var hrSpecialist = new ReviewerAgent(
    name: "HR Specialist",
    systemPrompt: """
        You are a senior federal HR specialist reviewing a job announcement draft.
        Focus on: accuracy of position title and series, clarity of duties section,
        qualification requirements alignment with OPM standards, and overall compliance
        with federal hiring language. Be specific — cite exact lines that need improvement.
        """,
    chatClient: reviewerClient);

var legalReviewer = new ReviewerAgent(
    name: "Legal Reviewer",
    systemPrompt: """
        You are a federal employment law specialist reviewing a job announcement draft.
        Focus on: EEO statement completeness, non-discriminatory language throughout,
        reasonable accommodation language, and any phrases that could create legal risk.
        Flag any missing required legal statements. Be specific and cite exact text.
        """,
    chatClient: reviewerClient);

var budgetAnalyst = new ReviewerAgent(
    name: "Budget Analyst",
    systemPrompt: """
        You are a federal budget and compensation analyst reviewing a job announcement draft.
        Focus on: pay grade accuracy, salary range correctness for the grade and location,
        benefits summary completeness, and whether the compensation package is competitive.
        Note any discrepancies between stated grade and salary figures.
        """,
    chatClient: reviewerClient);

var moderator = new ReviewerAgent(
    name: "Moderator",
    systemPrompt: """
        You are a senior HR editor moderating a panel review of a federal job announcement.
        You will receive the original draft and critiques from three experts: HR Specialist,
        Legal Reviewer, and Budget Analyst. Your job is to produce a revised announcement
        that incorporates all valid improvements from each expert. Do not favor any single
        reviewer — synthesize all perspectives. The output must be a complete, polished
        job announcement ready for publication.
        """,
    chatClient: reviewerClient);

// ── User input ────────────────────────────────────────────────────────────────
Console.Write("Enter announcement ID to review: ");
if (!int.TryParse(Console.ReadLine(), out var announcementId))
{
    Console.WriteLine("Invalid announcement ID. Exiting.");
    return;
}

Console.Write("Enter position ID (needed to save revised draft): ");
if (!int.TryParse(Console.ReadLine(), out var positionId))
{
    Console.WriteLine("Invalid position ID. Exiting.");
    return;
}

// ── Run ───────────────────────────────────────────────────────────────────────
var groupChat = new HrGroupChat(
    hrSpecialist, legalReviewer, budgetAnalyst, moderator,
    mcpClient, getAnnouncementTool, saveAnnouncementTool);

await groupChat.RunAsync(announcementId, positionId);
```

---

### Task 11: Build and commit Part 9

**Files:** All files created in Tasks 7–10

- [ ] **Step 1: Build the solution**

```bash
dotnet build --no-restore -v q
```

Expected: `Build succeeded.` with `0 Error(s)`.

- [ ] **Step 2: Commit**

```bash
cd c:/apps/DotnetMultiAgentsTutorial
git add DotnetMultiAgents/DotnetMultiAgents.slnx DotnetMultiAgents/src/Hr.GroupChatOrchestrator/
git commit -m "feat: add Hr.GroupChatOrchestrator — Part 9 Group Chat pattern"
```

---

## Part 10 — Evaluator-Optimizer Pattern (`Hr.EvaluatorOrchestrator`)

GeneratorAgent writes a draft → EvaluatorAgent scores it (0–100) → loop with feedback until score ≥ 80 or 3 iterations.

### Task 12: Create `Hr.EvaluatorOrchestrator` project and add to solution

**Files:**
- Create: `src/Hr.EvaluatorOrchestrator/Hr.EvaluatorOrchestrator.csproj`
- Modify: `DotnetMultiAgents.slnx`

- [ ] **Step 1: Create the project file**

Create `src/Hr.EvaluatorOrchestrator/Hr.EvaluatorOrchestrator.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.AI" Version="10.*" />
    <PackageReference Include="OllamaSharp" Version="5.*" />
    <PackageReference Include="ModelContextProtocol" Version="1.*" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add project to solution**

In `DotnetMultiAgents.slnx`, add inside `<Folder Name="/src/">`:

```xml
<Project Path="src/Hr.EvaluatorOrchestrator/Hr.EvaluatorOrchestrator.csproj" />
```

- [ ] **Step 3: Restore packages**

```bash
dotnet restore
```

Expected: `Restore succeeded.`

---

### Task 13: Create `EvaluationResult`

**Files:**
- Create: `src/Hr.EvaluatorOrchestrator/Models/EvaluationResult.cs`

- [ ] **Step 1: Create `EvaluationResult.cs`**

```csharp
// src/Hr.EvaluatorOrchestrator/Models/EvaluationResult.cs
namespace Hr.EvaluatorOrchestrator.Models;

/// <summary>
/// Structured output returned by the EvaluatorAgent after scoring a draft.
/// </summary>
public sealed record EvaluationResult(int Score, Dictionary<string, string> Feedback)
{
    /// <summary>Draft meets the quality threshold when score ≥ 80.</summary>
    public bool MeetsThreshold => Score >= 80;
}
```

---

### Task 14: Create `GeneratorAgent`

**Files:**
- Create: `src/Hr.EvaluatorOrchestrator/Agents/GeneratorAgent.cs`

- [ ] **Step 1: Create `GeneratorAgent.cs`**

```csharp
// src/Hr.EvaluatorOrchestrator/Agents/GeneratorAgent.cs
using Microsoft.Extensions.AI;

namespace Hr.EvaluatorOrchestrator.Agents;

/// <summary>
/// Generates a job announcement draft for a given position.
/// On subsequent iterations, receives evaluator feedback and improves the draft.
/// </summary>
public sealed class GeneratorAgent(IChatClient chatClient, IReadOnlyList<AITool> tools)
{
    public async Task<string> GenerateAsync(
        int positionId,
        string? previousFeedback = null,
        CancellationToken ct = default)
    {
        var improvementGuidance = previousFeedback is null
            ? string.Empty
            : $"\n\nPrevious attempt feedback — address all points:\n{previousFeedback}";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, $"""
                You are a federal HR writing specialist.
                Call WriteJobDescription with the given position ID to generate a job announcement draft.
                Return the full draft text and nothing else — no commentary, no preamble.{improvementGuidance}
                """),
            new(ChatRole.User, $"Generate a job announcement for position ID {positionId}."),
        };

        var response = await chatClient.GetResponseAsync(
            messages, new ChatOptions { Tools = [.. tools] }, ct);

        return response.Text ?? string.Empty;
    }
}
```

---

### Task 15: Create `EvaluatorAgent`

**Files:**
- Create: `src/Hr.EvaluatorOrchestrator/Agents/EvaluatorAgent.cs`

- [ ] **Step 1: Create `EvaluatorAgent.cs`**

```csharp
// src/Hr.EvaluatorOrchestrator/Agents/EvaluatorAgent.cs
using System.Text.Json;
using Hr.EvaluatorOrchestrator.Models;
using Microsoft.Extensions.AI;

namespace Hr.EvaluatorOrchestrator.Agents;

/// <summary>
/// Scores a job announcement draft against a 4-criterion rubric (25 pts each, 100 max).
/// Returns a structured <see cref="EvaluationResult"/> parsed from the LLM's JSON response.
/// No MCP tools — reasons purely over the draft text passed in the prompt.
/// </summary>
public sealed class EvaluatorAgent(IChatClient chatClient)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<EvaluationResult> EvaluateAsync(string draftText, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, """
                You are an expert evaluator of federal job announcement drafts.
                Score the draft on these four criteria (0–25 points each, 100 total):
                - Clarity:       Is the writing clear, concise, and professionally structured?
                - OPM Language:  Does it use correct federal HR terminology and OPM style?
                - Completeness:  Does it include all standard sections: duties, qualifications, pay, how to apply?
                - Tone:          Is the tone formal, inclusive, and appropriate for a federal posting?

                Reply with ONLY a valid JSON object — no markdown fences, no extra text:
                {"score":<0-100>,"feedback":{"Clarity":"<note>","OPM Language":"<note>","Completeness":"<note>","Tone":"<note>"}}
                """),
            new(ChatRole.User, $"Evaluate this job announcement:\n\n{draftText}"),
        };

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        var json = (response.Text ?? "{}").Trim();

        try
        {
            var dto = JsonSerializer.Deserialize<EvaluationResultDto>(json, JsonOpts);
            return new EvaluationResult(
                dto?.Score ?? 0,
                dto?.Feedback ?? []);
        }
        catch
        {
            // LLM returned non-JSON — score 0 forces another iteration
            return new EvaluationResult(0, new Dictionary<string, string>
            {
                ["Parse Error"] = $"Non-JSON response: {json[..Math.Min(json.Length, 200)]}"
            });
        }
    }

    private sealed record EvaluationResultDto(int Score, Dictionary<string, string> Feedback);
}
```

---

### Task 16: Create `EvaluatorOptimizerLoop`

**Files:**
- Create: `src/Hr.EvaluatorOrchestrator/Loop/EvaluatorOptimizerLoop.cs`

- [ ] **Step 1: Create `EvaluatorOptimizerLoop.cs`**

```csharp
// src/Hr.EvaluatorOrchestrator/Loop/EvaluatorOptimizerLoop.cs
using Hr.EvaluatorOrchestrator.Agents;
using Hr.EvaluatorOrchestrator.Models;
using Microsoft.Extensions.AI;

namespace Hr.EvaluatorOrchestrator.Loop;

/// <summary>
/// Implements the Evaluator-Optimizer pattern.
///
/// Each iteration:
///   1. GeneratorAgent produces (or improves) a draft using evaluator feedback.
///   2. EvaluatorAgent scores the draft on four criteria (0–100).
///   3. Score ≥ 80 → exit; else inject feedback and retry.
/// Maximum 3 iterations. The highest-scoring draft is saved.
/// </summary>
public sealed class EvaluatorOptimizerLoop(
    GeneratorAgent generator,
    EvaluatorAgent evaluator,
    IChatClient saverClient,
    AITool saveAnnouncementTool,
    int maxIterations = 3,
    int threshold = 80)
{
    public async Task RunAsync(int positionId, CancellationToken ct = default)
    {
        Console.WriteLine($"\n{"",60}".Replace(' ', '='));
        Console.WriteLine($"  Evaluator-Optimizer — Position {positionId}");
        Console.WriteLine($"  Threshold: {threshold}/100 | Max iterations: {maxIterations}");
        Console.WriteLine($"{"",60}".Replace(' ', '=') + "\n");

        string bestDraft    = string.Empty;
        int    bestScore    = -1;
        string? lastFeedback = null;

        for (var i = 1; i <= maxIterations; i++)
        {
            PrintHeader(i, maxIterations, "Generating draft");
            var draft = await generator.GenerateAsync(positionId, lastFeedback, ct);

            PrintHeader(i, maxIterations, "Evaluating draft");
            var result = await evaluator.EvaluateAsync(draft, ct);

            PrintEvaluation(i, draft, result);

            if (result.Score > bestScore)
            {
                bestScore = result.Score;
                bestDraft = draft;
            }

            if (result.MeetsThreshold)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\nQuality threshold met ({result.Score}/100). Exiting loop.\n");
                Console.ResetColor();
                break;
            }

            if (i < maxIterations)
            {
                lastFeedback = BuildFeedbackPrompt(result);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Score {result.Score}/100 — below threshold. Retrying with feedback...\n");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\nMax iterations reached. Best score: {bestScore}/100.\n");
                Console.ResetColor();
            }
        }

        // Save the highest-scoring draft
        Console.WriteLine("Saving best draft to database...");
        var saveMessages = new List<ChatMessage>
        {
            new(ChatRole.System,
                "Save the provided job announcement draft for the given position. Return only the save confirmation."),
            new(ChatRole.User,
                $"Save this announcement for position ID {positionId}:\n\n{bestDraft}"),
        };
        var saveResponse = await saverClient.GetResponseAsync(
            saveMessages, new ChatOptions { Tools = [saveAnnouncementTool] }, ct);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\nDone. {saveResponse.Text}");
        Console.ResetColor();
    }

    private static void PrintHeader(int iteration, int max, string action)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[Iteration {iteration}/{max}] {action}...");
        Console.ResetColor();
    }

    private static void PrintEvaluation(int iteration, string draft, EvaluationResult result)
    {
        Console.WriteLine($"\n--- Draft (Iteration {iteration}) ---");
        Console.WriteLine(draft.Length > 500 ? draft[..500] + "..." : draft);
        Console.WriteLine($"\n--- Evaluation Score: {result.Score}/100 ---");
        foreach (var (criterion, note) in result.Feedback)
            Console.WriteLine($"  {criterion,-15}: {note}");
        Console.WriteLine();
    }

    private static string BuildFeedbackPrompt(EvaluationResult result)
    {
        var lines = result.Feedback.Select(kv => $"- {kv.Key}: {kv.Value}");
        return $"Previous attempt scored {result.Score}/100. Specific weaknesses to address:\n{string.Join("\n", lines)}";
    }
}
```

---

### Task 17: Create `Program.cs` for `Hr.EvaluatorOrchestrator`

**Files:**
- Create: `src/Hr.EvaluatorOrchestrator/Program.cs`

- [ ] **Step 1: Create `Program.cs`**

```csharp
// src/Hr.EvaluatorOrchestrator/Program.cs
using Hr.EvaluatorOrchestrator.Agents;
using Hr.EvaluatorOrchestrator.Loop;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OllamaSharp;

// ── MCP connection ─────────────────────────────────────────────────────────────
var hrMcpUrl = Environment.GetEnvironmentVariable("HR_MCP_SERVER_URL")
    ?? "http://localhost:5100/mcp";

await using var hrMcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(hrMcpUrl) }));

var hrTools = (await hrMcpClient.ListToolsAsync()).Cast<AITool>().ToList();
Console.WriteLine($"HR tools: {string.Join(", ", hrTools.Select(t => t.Name))}\n");

// ── Chat clients ───────────────────────────────────────────────────────────────
// Generator and Saver need function invocation; Evaluator reasons over text only.
IChatClient generatorClient = ((IChatClient)new OllamaApiClient(
        new Uri("http://localhost:11434"), "llama3.2"))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

IChatClient evaluatorClient = ((IChatClient)new OllamaApiClient(
        new Uri("http://localhost:11434"), "llama3.2"))
    .AsBuilder()
    .Build();

IChatClient saverClient = ((IChatClient)new OllamaApiClient(
        new Uri("http://localhost:11434"), "llama3.2"))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

// ── Tool subsets ───────────────────────────────────────────────────────────────
var generatorTools = hrTools
    .Where(t => t.Name is "WriteJobDescription" or "GetPositionById")
    .ToList();

var saveAnnouncementTool = hrTools.First(t => t.Name == "SaveJobAnnouncement");

// ── User input ─────────────────────────────────────────────────────────────────
Console.Write("Enter position ID to optimize: ");
if (!int.TryParse(Console.ReadLine(), out var positionId))
{
    Console.WriteLine("Invalid position ID. Exiting.");
    return;
}

// ── Run loop ───────────────────────────────────────────────────────────────────
var loop = new EvaluatorOptimizerLoop(
    new GeneratorAgent(generatorClient, generatorTools),
    new EvaluatorAgent(evaluatorClient),
    saverClient,
    saveAnnouncementTool);

await loop.RunAsync(positionId);
```

---

### Task 18: Build and commit Part 10

**Files:** All files created in Tasks 12–17

- [ ] **Step 1: Build the solution**

```bash
dotnet build --no-restore -v q
```

Expected: `Build succeeded.` with `0 Error(s)`.

- [ ] **Step 2: Commit**

```bash
cd c:/apps/DotnetMultiAgentsTutorial
git add DotnetMultiAgents/DotnetMultiAgents.slnx DotnetMultiAgents/src/Hr.EvaluatorOrchestrator/
git commit -m "feat: add Hr.EvaluatorOrchestrator — Part 10 Evaluator-Optimizer pattern"
```

---

## Running the Projects

Prerequisites: Ollama running locally with `llama3.2` pulled, `Hr.Jobs.Mcp` running on port 5100 with `Features:EnableOidc=false`, `Hr.Compliance.Mcp` running on port 5200.

**Part 8 — Pipe:**
```bash
cd DotnetMultiAgents
dotnet run --project src/Hr.PipeOrchestrator
# Enter a valid position ID when prompted (e.g. 1)
```

**Part 9 — Group Chat:**
```bash
dotnet run --project src/Hr.GroupChatOrchestrator
# Enter an announcement ID (from Part 8 or a prior run), then a position ID
```

**Part 10 — Evaluator-Optimizer:**
```bash
dotnet run --project src/Hr.EvaluatorOrchestrator
# Enter a valid position ID when prompted
```
