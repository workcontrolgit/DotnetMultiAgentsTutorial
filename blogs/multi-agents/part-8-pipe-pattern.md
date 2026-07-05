# Part 8 — The Pipe Pattern: Sequential Agent Stages

*[Building Multi-Agent Systems with .NET 10 Blog Series](preface-why-one-agent-is-not-enough.md)*

---

Part 7 showed Claude Desktop acting as the orchestrator — connecting the same two MCP servers with zero custom code. That works for ad hoc queries. But some workflows are not ad hoc: producing a compliant, persisted job announcement requires a draft, then a compliance check, then a status update — in that exact order, every time. This part introduces the Pipe pattern, where each agent stage feeds its output directly into the next.

The Selector pattern answers one question per turn. Each user message is classified, delegated to one specialist, and answered independently. That works well when queries are discrete.

But producing a compliant, persisted job announcement is not a discrete query — it is three dependent steps. A draft must exist before it can be compliance-checked. The compliance outcome must be known before the status can be updated. No step can be skipped, and none can run before the previous one finishes.

This is exactly the problem the Pipe pattern solves.

---

## The Pipe Pattern

In the Pipe pattern, agents are arranged in a fixed sequence. Each stage receives the previous stage's output as its input. No stage can run until the one before it completes. The pipeline itself enforces the order.

The HR pipeline in `Hr.PipeOrchestrator` has three stages:

- Stage 1 — `DraftAgent`: calls `WriteJobDescription` and `SaveJobAnnouncement`, returns the draft text and announcement ID
- Stage 2 — `ComplianceAgent`: calls `RunFullComplianceCheck`, returns the compliance report and a pass/fail boolean
- Stage 3 — Status update: calls `UpdateAnnouncementStatus` with the outcome from Stage 2

Each stage is a focused agent with a single responsibility. No stage needs to know what comes before or after it — the pipeline coordinator (`HrPipeline`) handles the handoff.

---

## Stage 1 — DraftAgent

[DraftAgent.cs](../../DotnetMultiAgents/src/Hr.PipeOrchestrator/Agents/DraftAgent.cs) performs two tool calls in sequence: generate the draft, then save it. The system prompt instructs the agent to embed the saved announcement ID in its reply using a predictable token:

```csharp
// src/Hr.PipeOrchestrator/Agents/DraftAgent.cs
new(ChatRole.System, """
    You are a federal HR writing specialist operating in an automated pipeline.
    When given a position ID:
    1. Call WriteJobDescription to generate the announcement draft.
    2. Call SaveJobAnnouncement with the position ID and the draft text.
    3. Include the saved announcement ID in your reply using this exact format on its own line:
       ANNOUNCEMENT_ID:<id>
    Do not ask questions. Complete both tool calls before responding.
    """),
```

The `ParseAnnouncementId` method extracts the ID from the reply using a simple string scan:

```csharp
private static int? ParseAnnouncementId(string text)
{
    const string prefix = "ANNOUNCEMENT_ID:";
    var idx = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
    if (idx < 0) return null;
    var token = text[(idx + prefix.Length)..].Trim().Split([' ', '\n', '\r'], 2)[0];
    return int.TryParse(token, out var id) ? id : null;
}
```

If the LLM does not include the token — an edge case that happens with weaker models — `ParseAnnouncementId` returns `null`. The pipeline logs a warning and skips Stage 3 rather than crashing.

The return type `(string Reply, int? AnnouncementId)` makes the null case explicit. The pipeline coordinator handles it without needing to parse text itself.

---

## Stage 2 — ComplianceAgent

[ComplianceAgent.cs](../../DotnetMultiAgents/src/Hr.PipeOrchestrator/Agents/ComplianceAgent.cs) calls `RunFullComplianceCheck` on the compliance server and extracts a pass/fail signal from the response. Its return type is `(string Report, bool Passed)`:

```csharp
// The agent's system prompt instructs it to produce a structured reply:
new(ChatRole.System, """
    You are a federal HR compliance specialist operating in an automated pipeline.
    Run RunFullComplianceCheck for the given position ID and report all results clearly.
    End your reply with exactly one of these lines (no extra text after it):
    COMPLIANCE_RESULT:PASSED
    COMPLIANCE_RESULT:FAILED
    """),
```

The boolean outcome flows directly into Stage 3, which uses it to set `CompliancePassed` or `ComplianceFailed` without re-parsing the report text.

---

## HrPipeline: The Three-Stage Coordinator

[HrPipeline.cs](../../DotnetMultiAgents/src/Hr.PipeOrchestrator/Pipeline/HrPipeline.cs) orchestrates the three stages with user confirmation gates between each:

```csharp
// src/Hr.PipeOrchestrator/Pipeline/HrPipeline.cs
public async Task RunAsync(int positionId, CancellationToken ct = default)
{
    // Stage 1: Draft
    PrintStageHeader(1, "Generating job announcement draft");
    var (draftReply, announcementId) = await draftAgent.RunAsync(positionId, ct);
    Console.WriteLine($"\n{draftReply}\n");

    if (announcementId is null)
        Console.WriteLine("[Warning] Could not extract announcement ID — Stage 3 status update will be skipped.");

    if (!Confirm("Continue to Stage 2 — Compliance Check?")) return;

    // Stage 2: Compliance
    PrintStageHeader(2, "Running OPM compliance check");
    var (report, passed) = await complianceAgent.RunAsync(positionId, ct);
    Console.WriteLine($"\n{report}\n");

    if (!Confirm("Continue to Stage 3 — Update Status?")) return;

    // Stage 3: Status update
    PrintStageHeader(3, "Recording compliance outcome");
    var statusLabel = passed ? "CompliancePassed" : "ComplianceFailed";
    var summary = passed
        ? "All OPM compliance rules passed. Announcement is ready for publication."
        : "One or more OPM compliance rules failed. Review the compliance report above.";

    var statusMessages = new List<ChatMessage>
    {
        new(ChatRole.System,
            "You are an HR status recorder. Call UpdateAnnouncementStatus immediately with the details given."),
        new(ChatRole.User,
            $"Update announcement {announcementId} to status {statusLabel} with summary: {summary}"),
    };

    var statusResponse = await statusClient.GetResponseAsync(
        statusMessages, ChatOptionsFactory.Create([updateStatusTool], numCtx), ct);

    Console.WriteLine($"\n{statusResponse.Text}\n");
    Console.ForegroundColor = passed ? ConsoleColor.Green : ConsoleColor.Red;
    Console.WriteLine($"Pipeline complete. Final status: {statusLabel}");
    Console.ResetColor();
}
```

Three design choices to notice:

The confirmation gates are intentional. This is a semi-automated pipeline — the user sees each stage's output before approving the next step. For a fully automated pipeline (no human in the loop), remove the `Confirm` calls.

Stage 3 uses a minimal `IChatClient` call with a single tool, not a full `SpecialistAgent`. The task is simple — call one tool with known arguments — and does not benefit from a conversational history.

The `statusClient` and `updateStatusTool` are injected separately from the Stage 1 and Stage 2 agents. This means Stage 3 can use a different, cheaper model without affecting the draft quality.

---

## Running the Pipeline

```bash
dotnet run --project src/Hr.PipeOrchestrator
```

Expected output:

```
============================================================
  HR Pipeline — Position 7
============================================================

[Stage 1/3] Generating job announcement draft...

IT Specialist (APPSW), GS-12, Department of Homeland Security

## Summary
Join the Department of Homeland Security as an IT Specialist...

## Duties
- You will design and implement enterprise software applications...

ANNOUNCEMENT_ID:5

Continue to Stage 2 — Compliance Check? (y/n): y

[Stage 2/3] Running OPM compliance check...

Overall: PASS
RequiredFields:        PASS
PayGradeRange:         PASS
PayGradeAlignment:     PASS — GS-12 is within allowed range for series 2210
ApplicationPeriod:     PASS — 22 business days
QualificationsGrade:   PASS
SecurityClearance:     PASS
WhoMayApply:           PASS

COMPLIANCE_RESULT:PASSED

Continue to Stage 3 — Update Status? (y/n): y

[Stage 3/3] Recording compliance outcome...

Announcement 5 updated to CompliancePassed.

Pipeline complete. Final status: CompliancePassed
```

Three stages, two confirmation prompts, one fully-compliant persisted announcement.

---

## Pipe vs. Selector

Use the Selector pattern when:
- Each user turn is independent — position search, org summary, compliance check are unrelated requests
- The agent that answers is determined by what the user asks, not by what happened previously

Use the Pipe pattern when:
- Steps are dependent — Stage 2 requires Stage 1's output; Stage 3 requires Stage 2's outcome
- The order cannot change — compliance checking a non-existent draft makes no sense
- You need clear stage accountability — each stage has one owner, one responsibility, one output contract

The two patterns compose. A Selector orchestrator can route "generate a compliant announcement" requests to a `HrPipeline` instance while routing "search for positions" requests to a `SpecialistAgent`.

---

A single pipeline produces one draft per run. When you need multiple expert perspectives on that draft before saving it, one reviewer is not enough. Part 9 introduces the Group Chat pattern, where three specialists critique the same draft in parallel and a moderator synthesizes their feedback into a revised version.

---

← [Part 7 — Claude Desktop as Multi-Agent Platform](part-7-claude-desktop-multi-agent.md) &nbsp;|&nbsp; [Part 9 — The Group Chat Pattern →](part-9-group-chat-pattern.md)

---

## References

### NuGet Packages

- [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI) — `IChatClient`, `ChatMessage`, `ChatOptions`, `AITool`
- [OllamaSharp](https://www.nuget.org/packages/OllamaSharp) — `OllamaApiClient` backing all three pipeline agents
- [ModelContextProtocol.Core](https://www.nuget.org/packages/ModelContextProtocol.Core) — `McpClient.CreateAsync`, `HttpClientTransport` for connecting to both MCP servers

### Microsoft Documentation

- [Microsoft.Extensions.AI overview](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai) — IChatClient and function invocation middleware
- [Task-based Asynchronous Pattern](https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap) — `async/await` patterns used throughout the pipeline

### GitHub

- [DotnetMultiAgentsTutorial](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial) — Full source including `Hr.PipeOrchestrator`
- [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) — Official C# MCP SDK
