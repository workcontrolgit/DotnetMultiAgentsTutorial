# Part 9 — The Group Chat Pattern: Parallel Expert Review

*[Building Multi-Agent Systems with .NET 10 Blog Series](preface-why-one-agent-is-not-enough.md)*

---

Part 8 built the Pipe — three stages chained sequentially: a drafting agent writes the job announcement, a compliance agent checks it against OPM rules, and a persistence agent saves the result. Each stage depends on the previous, and the output flows forward automatically. But the pipeline has one perspective per stage, one reviewer per draft. This part adds breadth: the Group Chat pattern, where multiple specialist agents review the same draft in parallel before a moderator synthesizes their feedback.

The Pipe pattern produces a draft, compliance-checks it, and records the outcome. One agent per stage, one perspective per draft. For many workflows that is enough.

But a federal job announcement is reviewed by multiple stakeholders before it is published — an HR specialist checks terminology, a legal reviewer checks statutory language, a budget analyst checks grade justification. Running one reviewer misses what the others would catch. Running them one after another lets each reviewer anchor on the previous critique.

The Group Chat pattern eliminates both problems.

---

## The Group Chat Pattern

In the Group Chat (Debate) pattern, multiple agents review the same input simultaneously. Each agent works independently — no reviewer sees another's feedback until the moderator collects them all. A moderator agent then synthesizes the critiques into a revised output.

Two properties make this effective:

Parallel execution via `Task.WhenAll` means all three reviewers finish in roughly the time of the slowest one — not the sum of all three.

Blind review means each agent reasons from the draft alone, not from a position someone else has already taken. This eliminates anchoring bias — the tendency to agree with or react to the first opinion rather than forming an independent one.

The HR Group Chat has four agents:

- `HrSpecialist` — checks federal HR terminology, required sections, and OPM style
- `LegalReviewer` — checks statutory language, equal opportunity statements, and classification accuracy
- `BudgetAnalyst` — checks pay grade justification, salary range accuracy, and grade-level alignment
- `Moderator` — synthesizes all three critiques into a revised draft

---

## ReviewerAgent: One Class, Three Experts

All four agents — three reviewers and the moderator — use the same [ReviewerAgent.cs](../../DotnetMultiAgents/src/Hr.GroupChatOrchestrator/Agents/ReviewerAgent.cs) class. The difference between them is entirely in the system prompt:

```csharp
// src/Hr.GroupChatOrchestrator/Agents/ReviewerAgent.cs
public sealed class ReviewerAgent(string name, string systemPrompt, IChatClient chatClient)
{
    public string Name { get; } = name;

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

`ReviewAsync` is used by the three reviewers. `SynthesizeAsync` is used only by the moderator — it receives the original draft plus all three critiques and produces the revised version. All agents are stateless single-turn calls with no MCP tools. They reason entirely over the draft text passed in the prompt.

The three reviewer system prompts:

```csharp
var hrSpecialist = new ReviewerAgent(
    name: "HR Specialist",
    systemPrompt: """
        You are a federal HR specialist reviewing a job announcement draft.
        Focus on: correct federal HR terminology, required OPM sections (duties, qualifications,
        pay, how to apply), proper GS grade language, and clarity for applicants.
        Be specific. List each issue with a suggested fix. 3-5 bullet points maximum.
        """,
    chatClient: reviewerClient);

var legalReviewer = new ReviewerAgent(
    name: "Legal Reviewer",
    systemPrompt: """
        You are a federal employment law reviewer checking a job announcement draft.
        Focus on: equal opportunity language, veterans preference statement, reasonable
        accommodation notice, prohibited questions, and classification accuracy.
        Be specific. List each issue with a suggested fix. 3-5 bullet points maximum.
        """,
    chatClient: reviewerClient);

var budgetAnalyst = new ReviewerAgent(
    name: "Budget Analyst",
    systemPrompt: """
        You are a federal budget analyst reviewing a job announcement for fiscal accuracy.
        Focus on: pay range accuracy for the GS grade and locality, grade justification
        matching stated duties, and whether the announced salary matches OPM pay tables.
        Be specific. List each issue with a suggested fix. 3-5 bullet points maximum.
        """,
    chatClient: reviewerClient);
```

The moderator system prompt:

```csharp
var moderator = new ReviewerAgent(
    name: "Moderator",
    systemPrompt: """
        You are a senior federal HR editor synthesizing expert review feedback.
        You will receive the original draft and critiques from three specialists.
        Produce a revised announcement that addresses all valid critique points.
        Maintain the original structure. Do not add new sections unless a critique explicitly requires it.
        Return only the revised announcement — no commentary, no preamble.
        """,
    chatClient: reviewerClient);
```

---

## HrGroupChat: Load → Review → Synthesize → Save

[HrGroupChat.cs](../../DotnetMultiAgents/src/Hr.GroupChatOrchestrator/Chat/HrGroupChat.cs) coordinates the two-round flow:

```csharp
// src/Hr.GroupChatOrchestrator/Chat/HrGroupChat.cs
public async Task RunAsync(int announcementId, int positionId, CancellationToken ct = default)
{
    // Load draft from database
    var loadMessages = new List<ChatMessage>
    {
        new(ChatRole.System,
            "Retrieve the job announcement with the given ID and return its full text verbatim."),
        new(ChatRole.User, $"Get job announcement ID {announcementId}."),
    };
    var loadResponse = await mcpClient.GetResponseAsync(
        loadMessages, new ChatOptions { Tools = [getAnnouncementTool] }, ct);
    var draftText = loadResponse.Text ?? string.Empty;

    Console.WriteLine($"\n--- Current Draft ---\n{draftText}\n");
    if (!Confirm("Start group review (3 specialists will critique in parallel)?")) return;

    // Round 1: Parallel debate
    Console.WriteLine("\n[Round 1] Running parallel expert review...");
    var hrTask     = hrSpecialist.ReviewAsync(draftText, ct);
    var legalTask  = legalReviewer.ReviewAsync(draftText, ct);
    var budgetTask = budgetAnalyst.ReviewAsync(draftText, ct);
    await Task.WhenAll(hrTask, legalTask, budgetTask);

    PrintCritique(hrSpecialist.Name,  hrTask.Result);
    PrintCritique(legalReviewer.Name, legalTask.Result);
    PrintCritique(budgetAnalyst.Name, budgetTask.Result);

    if (!Confirm("Continue to Round 2 — Moderator synthesis?")) return;

    // Round 2: Synthesis
    Console.WriteLine("\n[Round 2] Moderator synthesizing critiques into revised draft...");
    var critiques = new List<(string, string)>
    {
        (hrSpecialist.Name,  hrTask.Result),
        (legalReviewer.Name, legalTask.Result),
        (budgetAnalyst.Name, budgetTask.Result),
    };
    var revisedDraft = await moderator.SynthesizeAsync(draftText, critiques, ct);
    Console.WriteLine($"\n--- Revised Draft ---\n{revisedDraft}\n");

    if (!Confirm("Save revised draft to database?")) return;

    // Save
    var saveMessages = new List<ChatMessage>
    {
        new(ChatRole.System, "Save the provided job announcement draft for the given position."),
        new(ChatRole.User, $"Save this announcement for position ID {positionId}:\n\n{revisedDraft}"),
    };
    var saveResponse = await mcpClient.GetResponseAsync(
        saveMessages, new ChatOptions { Tools = [saveAnnouncementTool] }, ct);
    Console.WriteLine($"\nGroup chat complete. {saveResponse.Text}");
}
```

Three things to note:

The draft is loaded from the database using `GetJobAnnouncement`. This means the Group Chat picks up where the Pipe left off — run the Pipe first to create the announcement, then run the Group Chat to improve it.

`Task.WhenAll` starts all three reviewer calls simultaneously. The three `IChatClient` calls are independent (no shared state) so running them in parallel is safe. The combined review time is roughly the time of the slowest reviewer, not the sum of all three.

The moderator receives the complete critique text from all three reviewers in a single call. It does not see intermediate synthesis — it gets the original draft and all three critiques together, which gives it full context for a coherent revision.

---

## A Full Demo Run

```bash
# Terminal 1
dotnet run --project src/Hr.Jobs.Mcp

# Terminal 2
dotnet run --project src/Hr.GroupChatOrchestrator
# Enter announcement ID: 5
# Enter position ID: 7
```

```
============================================================
  HR Group Chat — Announcement 5
============================================================

Loading draft from database...

--- Current Draft ---
IT Specialist (APPSW), GS-12, Department of Homeland Security
...

Start group review (3 specialists will critique in parallel)? (y/n): y

[Round 1] Running parallel expert review...

[HR Specialist]
- The "Duties" section uses passive voice throughout. Federal postings should use active voice ("You will design..." not "Responsibilities include designing...").
- Missing: "Supervisory Status: No" declaration required by OPM guidance.
- "Competitive Service" vs "Excepted Service" status not stated.

[Legal Reviewer]
- Equal Employment Opportunity statement present but missing the disability accommodation notice.
- Veterans preference language is correct.
- "U.S. Citizen" requirement properly stated for competitive service.

[Budget Analyst]
- GS-12 Step 1–10 range for Washington DC locality ($89,834–$116,788) matches the announced range.
- Grade justification in duties section adequately supports GS-12 level complexity.
- No issues found with pay table accuracy.

Continue to Round 2 — Moderator synthesis? (y/n): y

[Round 2] Moderator synthesizing critiques into revised draft...

--- Revised Draft ---
IT Specialist (APPSW), GS-12, Department of Homeland Security

Supervisory Status: No | Service: Competitive

## Summary
Join the Department of Homeland Security as an IT Specialist (APPSW) at the GS-12 level...

## Duties
- You will design and implement enterprise software applications supporting DHS mission systems.
- You will lead code reviews and mentor junior developers on secure coding standards.
...

Equal Opportunity: This agency is an equal opportunity employer. Reasonable accommodations
are available for persons with disabilities. Contact HR@dhs.gov to request accommodation.
...

Save revised draft to database? (y/n): y

Group chat complete. Announcement saved. New ID: 6
```

Two rounds, three reviewers, one synthesized revision. The revised draft addressed all five critique points.

---

## Group Chat vs. Pipe

Use the Pipe pattern when:
- Steps are ordered and each depends on the previous output
- You need a linear transformation: draft → check → record

Use the Group Chat pattern when:
- Multiple independent perspectives improve the output
- Parallel review is faster than sequential review
- You want a moderator to reconcile conflicting feedback

The patterns compose naturally. Run the Pipe first to produce a compliant, persisted draft. Then run the Group Chat on that draft to improve its quality before publication.

---

Group Chat produces a better draft. But "better" is qualitative — how much better? And is it consistently above the quality bar required for publication? Part 10 introduces the Evaluator-Optimizer pattern, which quantifies draft quality on a 100-point rubric and keeps iterating until the score meets a defined threshold.

---

← [Part 8 — The Pipe Pattern](part-8-pipe-pattern.md) &nbsp;|&nbsp; [Part 10 — The Evaluator-Optimizer Pattern →](part-10-evaluator-optimizer-pattern.md)

---

## References

### NuGet Packages

- [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI) — `IChatClient`, `ChatMessage`, `ChatOptions`, `AITool`
- [OllamaSharp](https://www.nuget.org/packages/OllamaSharp) — `OllamaApiClient` for all four agents (reviewers + moderator)
- [ModelContextProtocol.Core](https://www.nuget.org/packages/ModelContextProtocol.Core) — `McpClient.CreateAsync`, `HttpClientTransport` for `GetJobAnnouncement` and `SaveJobAnnouncement`

### Microsoft Documentation

- [Task.WhenAll](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.whenall) — Running multiple async operations in parallel and awaiting all results
- [Microsoft.Extensions.AI overview](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai) — IChatClient and stateless single-turn call patterns

### GitHub

- [DotnetMultiAgentsTutorial](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial) — Full source including `Hr.GroupChatOrchestrator`
- [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) — Official C# MCP SDK
