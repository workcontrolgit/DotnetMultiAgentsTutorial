# Part 6 — The Selector Pattern: Routing to Specialists

*Part 6 of: Building Multi-Agent Systems with .NET 10*

---

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · **Part 6** · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 5 — Persisting AI Artifacts](part-5-persisting-ai-artifacts.md) &nbsp;|&nbsp; [Part 7 — Claude Desktop as Multi-Agent Platform →](part-7-claude-desktop-multi-agent.md)

*Medium: [← Part 5](MEDIUM_URL_PART_5) | [Part 7 →](MEDIUM_URL_PART_7)*

---

Parts 3 through 5 built the tools — two MCP servers with 14 tools between them, covering HR data queries, job description generation, compliance checking, and announcement persistence. This post wires them together into a multi-agent system using the Selector pattern.

The Selector pattern is the simplest of the four multi-agent patterns and the right starting point for most real systems. One router classifies each user message. One specialist handles each turn. No parallelism, no feedback loops — just clean delegation.

---

## The Three Components

**AgentRouter** — classifies intent. Uses the LLM as a text classifier: no tools, low latency, single label output.

**SpecialistAgent** — handles one category of query. Focused system prompt, small tool subset, full function invocation.

**HrOrchestrator** — the selector loop. Reads user input, asks the router which agent should handle it, delegates, returns the response.

---

## AgentRouter: LLM as a Classifier

The router's system prompt defines exactly 5 categories and demands a single label in return:

```csharp
// src/Hr.SelectorOrchestrator/Orchestration/AgentRouter.cs
private static readonly string RouterSystemPrompt = """
    You are an intent classifier for an HR assistant application.
    Given a user's message, classify it into exactly one of these categories:

    position_search  — The user wants to list, find, filter, or read job positions.
    job_description  — The user wants to write, draft, or generate a job description.
    org_summary      — The user wants information about hiring organizations or departments.
    compliance       — The user wants to check OPM compliance, validate pay grades,
                       or verify a job posting meets federal standards.
    general          — Anything else: greetings, clarifications, off-topic messages.

    Reply with ONLY the category label — no explanation, no punctuation, no extra words.
    """;

public async Task<AgentIntent> ClassifyAsync(string userQuery, CancellationToken ct = default)
{
    var messages = new List<ChatMessage>
    {
        new(ChatRole.System, RouterSystemPrompt),
        new(ChatRole.User, userQuery),
    };

    var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
    var label = (response.Text ?? string.Empty).Trim().ToLowerInvariant();

    return label switch
    {
        "position_search" => AgentIntent.PositionSearch,
        "job_description" => AgentIntent.JobDescription,
        "org_summary"     => AgentIntent.OrgSummary,
        "compliance"      => AgentIntent.Compliance,
        _                 => AgentIntent.General,
    };
}
```

Three design choices to notice:

**No tools on the router.** The router `IChatClient` is built without `UseFunctionInvocation()`. This saves the overhead of scanning tool definitions on every classification call.

**Fallback to General.** Any label the LLM returns that is not one of the four known labels maps to `AgentIntent.General`. Unexpected model behavior degrades gracefully.

**Separate model budget.** The router and the specialist agents use the same `OllamaApiClient` configuration here, but they are built as separate `IChatClient` instances. In production you could route classification to a smaller, faster model (e.g., llama3.2:1b) while specialists use a larger, more capable one — one line change per client.

---

## SpecialistAgent: Focused Prompt, Scoped Tools

All five specialists share the same `SpecialistAgent` class. The difference between them is entirely in what you pass to the constructor.

The position search specialist:

```csharp
var positionTools = hrTools
    .Where(t => t.Name is "GetOpenPositions" or "GetPositionById"
                       or "GetPositionsByOrganization" or "GetHiringOrganizations")
    .ToList();  // 4 tools

var positionSearchAgent = new SpecialistAgent(
    name: "PositionSearch",
    systemPrompt: """
        You are a federal job search assistant. Help users find and understand open positions.
        - Use GetOpenPositions to list all open roles.
        - Use GetHiringOrganizations then GetPositionsByOrganization to scope by department.
        - Use GetPositionById for full detail on a specific role.
        - Present pay ranges in a readable format (e.g., "$68,000 – $107,000 per year").
        - Be concise; offer to go deeper when the user wants more detail.
        """,
    chatClient: agentClient,
    tools: positionTools);
```

The compliance specialist:

```csharp
var complianceAgentTools = complianceTools  // 5 compliance tools
    .Concat(hrTools.Where(t => t.Name is "GetPositionById" or "UpdateAnnouncementStatus"))
    .ToList();  // 7 tools total

var complianceAgent = new SpecialistAgent(
    name: "OPMCompliance",
    systemPrompt: """
        You are a federal HR compliance specialist. Check whether positions meet OPM standards.
        - Use RunFullComplianceCheck for a complete 7-rule report.
        - If the user provides an announcement ID, call UpdateAnnouncementStatus after
          the check completes — set status to CompliancePassed or ComplianceFailed
          and include a brief compliance summary.
        - Clearly state PASS, WARNING, or FAIL for each rule.
        - For failures, suggest specific corrections.
        """,
    chatClient: agentClient,
    tools: complianceAgentTools);
```

The full tool assignment across all five agents:

- **PositionSearch**: GetOpenPositions, GetPositionById, GetPositionsByOrganization, GetHiringOrganizations — 4 tools
- **JobDescription**: WriteJobDescription, GetPositionById, SaveJobAnnouncement, GetJobAnnouncement, ListJobAnnouncements — 5 tools
- **OrgSummary**: GetHiringOrganizations, GetPositionsByOrganization — 2 tools
- **OPMCompliance**: RunFullComplianceCheck, ValidatePayGrade, CheckApplicationPeriod, GetOPMStandard, ListOPMSeries, GetPositionById, UpdateAnnouncementStatus — 7 tools
- **General**: no tools — answers from LLM knowledge only

Maximum tools any single agent sees: 7. Total tools across both servers: 14. The agents collectively cover all 14, but no individual agent is burdened with the full set.

---

## HrOrchestrator: The Selector Loop

```csharp
// src/Hr.SelectorOrchestrator/Orchestration/HrOrchestrator.cs
public async Task RunAsync(CancellationToken ct = default)
{
    Console.WriteLine("HR Orchestrator ready. Type 'exit' to quit.\n");

    while (!ct.IsCancellationRequested)
    {
        Console.Write("You: ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input)) continue;
        if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

        // Step 1 — classify intent
        var intent = await router.ClassifyAsync(input, ct);
        var agent  = SelectAgent(intent);

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[Router → {agent.Name}]");
        Console.ResetColor();

        // Step 2 — delegate to specialist
        var reply = await agent.HandleAsync(input, ct);
        Console.WriteLine($"\nAssistant ({agent.Name}): {reply}\n");
    }
}

private SpecialistAgent SelectAgent(AgentIntent intent) => intent switch
{
    AgentIntent.PositionSearch => positionSearchAgent,
    AgentIntent.JobDescription => jobDescriptionAgent,
    AgentIntent.OrgSummary     => orgSummaryAgent,
    AgentIntent.Compliance     => complianceAgent,
    _                          => generalAgent,
};
```

The loop is 30 lines. The complexity is in the configuration — the prompts and tool subsets — not in the orchestration logic.

---

## A Full Demo Conversation

Start all three components:

```bash
# Terminal 1
dotnet run --project src/Hr.Jobs.Mcp

# Terminal 2
dotnet run --project src/Hr.Compliance.Mcp

# Terminal 3
dotnet run --project src/Hr.SelectorOrchestrator
```

```
HR Orchestrator ready. Type 'exit' to quit.

You: What IT Specialist positions are currently open?
[Router → PositionSearch]
Assistant (PositionSearch): I found 12 open IT Specialist positions. Here are the highlights:
  - IT Specialist (APPSW), GS-11–12, $73,000–$115,000, Washington DC, DHS
  - IT Specialist (INFOSEC), GS-12–13, $89,000–$136,000, Arlington VA, DoD
  ...

You: Write a job description for position 7 and save it
[Router → JobDescription]
Assistant (JobDescription): I've generated and saved the job announcement for position 7.

  Announcement ID: 4 | Status: Draft | Generated: 2026-05-08 06:14 UTC

  ## Summary
  Join the Department of Homeland Security as an IT Specialist (APPSW) at the GS-12 level...

  ## Duties
  - You will design and implement enterprise software applications...
  - You will lead code reviews and mentor junior developers...
  ...

You: Check if position 7 passes OPM compliance — announcement ID is 4
[Router → OPMCompliance]
Assistant (OPMCompliance): I ran the full OPM compliance check for position 7.

  Overall: PASS

  RequiredFields:        PASS
  PayGradeRange:         PASS
  PayGradeAlignment:     PASS — GS-12 is within allowed range for series 2210 (GS-05 to GS-15)
  ApplicationPeriod:     PASS — 22 business days (well above the 5-day minimum)
  QualificationsGrade:   PASS — qualifications text references GS-12
  SecurityClearance:     PASS — Secret clearance disclosed in qualifications
  WhoMayApply:           PASS — "U.S. Citizens" is a recognized category

  I've updated announcement 4 to CompliancePassed.

You: How many organizations are hiring for IT roles?
[Router → OrgSummary]
Assistant (OrgSummary): 8 organizations currently have open IT Specialist positions:
  - Department of Homeland Security: 12 open IT roles
  - Department of Defense: 9 open IT roles
  ...

You: Thanks, that's all for now
[Router → General]
Assistant (General): You're welcome! I can help with position searches, job description
writing, OPM compliance checks, and organization summaries whenever you need.
```

Five queries. Five different agents. The router got every one right. The draft was generated, saved, and compliance-checked — and announcement 4 is in the database with status `CompliancePassed`, ready for the next session.

---

## Cost Optimisation Notes

In this implementation, router and specialists use the same Ollama model. In production you would differentiate:

The router's classification task is simple. A 1B parameter model handles it reliably. The specialist agents handle complex tool selection and multi-step reasoning — they benefit from a larger, more capable model. Routing 100% of classification calls to a smaller model while only specialist calls use the larger model can cut token spend significantly without visible quality loss on routing.

The `BuildClient` helper in `Program.cs` already separates the two client instances. Swapping the router to a different model is a one-line change.

---

The Selector orchestrator works — but it required writing a router, five specialist agents, and an orchestration loop. Part 7 shows an alternative: connect the same two MCP servers unchanged to Claude Desktop and let Claude act as the orchestrator. Same results, zero orchestrator code — and a clear picture of when each approach is the right choice.

---

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · **Part 6** · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 5 — Persisting AI Artifacts](part-5-persisting-ai-artifacts.md) &nbsp;|&nbsp; [Part 7 — Claude Desktop as Multi-Agent Platform →](part-7-claude-desktop-multi-agent.md)

*Medium: [← Part 5](MEDIUM_URL_PART_5) | [Part 7 →](MEDIUM_URL_PART_7)*

---

## References

### NuGet Packages

- [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI) — `IChatClient`, `UseFunctionInvocation` middleware, `AITool`
- [OllamaSharp](https://www.nuget.org/packages/OllamaSharp) — `OllamaApiClient` for router and specialist agents
- [ModelContextProtocol.Core](https://www.nuget.org/packages/ModelContextProtocol.Core) — `McpClient.CreateAsync`, `HttpClientTransport`, `McpClientTool`

### Microsoft Documentation

- [Microsoft.Extensions.AI overview](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai) — IChatClient, middleware pipeline
- [IChatClient interface](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.ichatclient) — Full API reference including `GetResponseAsync`

### GitHub

- [DotnetMultiAgentsTutorial](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial) — Full source for all patterns in this series
- [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) — Official C# MCP SDK
