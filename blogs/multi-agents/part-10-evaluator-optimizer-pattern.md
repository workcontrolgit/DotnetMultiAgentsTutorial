# Part 10 — The Evaluator-Optimizer Pattern: Quality-Gated Generation

*Part 10 of: Building Multi-Agent Systems with .NET 10*

---

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · **Part 10**

← [Part 9 — The Group Chat Pattern](part-9-group-chat-pattern.md)

---

The Group Chat produces an improved draft through multiple perspectives. But "improved" is relative — the revised draft is better than the original, but is it good enough? Without a quality threshold, you cannot know when to stop revising and when to publish.

The Evaluator-Optimizer pattern introduces a measurable quality gate. A generator agent produces a draft. An evaluator agent scores it. If the score meets the threshold, the loop exits. If not, the evaluator's feedback becomes the generator's next prompt, and the loop runs again — up to a configurable maximum.

---

## The Evaluator-Optimizer Pattern

The loop has three components:

- `GeneratorAgent` — produces or improves a draft, optionally incorporating feedback from the previous iteration
- `EvaluatorAgent` — scores the draft against a fixed rubric and returns structured feedback
- `EvaluatorOptimizerLoop` — controls iteration: compare score to threshold, inject feedback, decide whether to continue

The loop exits on one of two conditions: the score meets or exceeds the threshold, or the maximum iteration count is reached. The highest-scoring draft is saved regardless of whether the threshold was met.

---

## GeneratorAgent: First Pass and Feedback Pass

[GeneratorAgent.cs](../../DotnetMultiAgents/src/Hr.EvaluatorOrchestrator/Agents/GeneratorAgent.cs) handles both cases with a single method. On the first iteration, `previousFeedback` is null and the system prompt contains no improvement guidance. On subsequent iterations, the feedback is injected directly into the system prompt:

```csharp
// src/Hr.EvaluatorOrchestrator/Agents/GeneratorAgent.cs
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
```

The feedback is injected into the system prompt, not as a separate user turn. This means the generator sees the quality expectations before it starts writing, not after. The model can weight the improvement guidance from the start of generation rather than trying to reconcile it with an already-formed draft.

---

## EvaluatorAgent: The 4-Criterion Rubric

[EvaluatorAgent.cs](../../DotnetMultiAgents/src/Hr.EvaluatorOrchestrator/Agents/EvaluatorAgent.cs) scores the draft against four criteria, 25 points each, for a maximum of 100. It returns a structured JSON object — no narrative, no preamble:

```csharp
// src/Hr.EvaluatorOrchestrator/Agents/EvaluatorAgent.cs
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
```

The response is parsed into a typed model:

```csharp
public async Task<EvaluationResult> EvaluateAsync(string draftText, CancellationToken ct = default)
{
    var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
    var json = (response.Text ?? "{}").Trim();

    try
    {
        var dto = JsonSerializer.Deserialize<EvaluationResultDto>(json, JsonOpts);
        return new EvaluationResult(
            dto?.Score ?? 0,
            dto?.Feedback ?? []);
    }
    catch (JsonException)
    {
        // LLM returned non-JSON — score 0 forces another iteration
        return new EvaluationResult(0, new Dictionary<string, string>
        {
            ["Parse Error"] = $"Non-JSON response: {json[..Math.Min(json.Length, 200)]}"
        });
    }
}
```

The `JsonException` catch is a deliberate design choice. If the LLM returns a narrative instead of JSON — which happens with weaker models under load — the score is set to 0, which forces another iteration. The loop does not crash; it retries with the same feedback structure. This makes the evaluator robust to model misbehavior without special-casing it in the loop.

---

## EvaluatorOptimizerLoop: The Control Flow

[EvaluatorOptimizerLoop.cs](../../DotnetMultiAgents/src/Hr.EvaluatorOrchestrator/Loop/EvaluatorOptimizerLoop.cs) manages the generate-evaluate-improve cycle:

```csharp
// src/Hr.EvaluatorOrchestrator/Loop/EvaluatorOptimizerLoop.cs
public async Task RunAsync(int positionId, CancellationToken ct = default)
{
    string bestDraft    = string.Empty;
    int    bestScore    = -1;
    string? lastFeedback = null;

    for (var i = 1; i <= maxIterations; i++)
    {
        // Generate (or improve) the draft
        var draft = await generator.GenerateAsync(positionId, lastFeedback, ct);

        // Evaluate
        var result = await evaluator.EvaluateAsync(draft, ct);

        // Track the best draft regardless of threshold
        if (result.Score > bestScore)
        {
            bestScore = result.Score;
            bestDraft = draft;
        }

        // Exit if threshold met
        if (result.MeetsThreshold)
        {
            Console.WriteLine($"\nQuality threshold met ({result.Score}/100). Exiting loop.\n");
            break;
        }

        // Prepare feedback for next iteration
        if (i < maxIterations)
        {
            lastFeedback = BuildFeedbackPrompt(result);
            if (!Confirm("Continue to next iteration with feedback?")) break;
        }
    }

    // Always save the best draft
    await SaveBestDraftAsync(positionId, bestDraft, ct);
}

private static string BuildFeedbackPrompt(EvaluationResult result)
{
    var lines = result.Feedback.Select(kv => $"- {kv.Key}: {kv.Value}");
    return $"Previous attempt scored {result.Score}/100. Specific weaknesses to address:\n{string.Join("\n", lines)}";
}
```

Four design choices to notice:

`bestDraft` and `bestScore` track the highest-scoring output across all iterations. If the loop reaches max iterations without meeting the threshold, the best draft seen is still saved — the loop always produces output.

`lastFeedback` is null on iteration 1, populated from the evaluator on every subsequent iteration. The generator sees exactly what to improve, framed as specific weaknesses rather than a generic "try again".

The confirmation prompt between iterations is optional for production. In this implementation it is present to let you observe each iteration's score and decide whether to continue. Remove the `Confirm` call for a fully automated loop.

The threshold (80) and max iterations (3) are constructor parameters. Tighten the threshold for quality-critical workflows; increase max iterations if the model needs more passes.

---

## A Full Demo Run

```bash
# Terminal 1
dotnet run --project src/Hr.Jobs.Mcp

# Terminal 2
dotnet run --project src/Hr.EvaluatorOrchestrator
```

```
============================================================
  Evaluator-Optimizer — Position 7
  Threshold: 80/100 | Max iterations: 3
============================================================

[Iteration 1/3] Generating draft...
[Iteration 1/3] Evaluating draft...

--- Draft (Iteration 1) ---
IT Specialist (APPSW), GS-12, Department of Homeland Security

Summary: The Department of Homeland Security seeks an IT Specialist...
[truncated at 500 chars]

--- Evaluation Score: 64/100 ---
  Clarity         : Writing is clear but uses passive voice in duties section
  OPM Language    : Missing "Supervisory Status" declaration; grade language informal
  Completeness    : All four required sections present
  Tone            : Formal and inclusive; minor improvement possible

Score 64/100 — below threshold.
Continue to next iteration with feedback? (y/n): y

[Iteration 2/3] Generating draft...
[Iteration 2/3] Evaluating draft...

--- Draft (Iteration 2) ---
IT Specialist (APPSW), GS-12 | Supervisory Status: No
Department of Homeland Security

Summary: Join DHS as an IT Specialist (APPSW) at the GS-12 level...
[truncated at 500 chars]

--- Evaluation Score: 83/100 ---
  Clarity         : Active voice throughout; well-structured
  OPM Language    : Supervisory status added; grade language correct
  Completeness    : All required sections present and complete
  Tone            : Formal, inclusive, appropriate for federal posting

Quality threshold met (83/100). Exiting loop.

Saving best draft to database...

Done. Job announcement saved. ID: 7
```

Two iterations to reach 83/100. The generator received the Clarity and OPM Language notes from iteration 1 and addressed both specifically in iteration 2.

---

## Evaluator-Optimizer vs. Group Chat

Use the Group Chat pattern when:
- Multiple domain perspectives improve quality (HR, legal, budget each catch different issues)
- Parallel execution matters — three reviewers in the time of one
- You want synthesis of conflicting feedback from different expert angles

Use the Evaluator-Optimizer pattern when:
- Quality is measurable on a consistent rubric
- You want a defined exit condition — "good enough" is a number, not a judgment call
- Iterative improvement is more valuable than breadth of perspective

The patterns compose: run the Evaluator-Optimizer first to reach a quality threshold, then run the Group Chat to refine the passing draft from multiple expert perspectives before publication.

---

## The Four Patterns Together

This series covered all four multi-agent patterns using the same HR domain, the same two MCP servers, and the same `IChatClient` abstraction throughout:

- **Selector** — routes each user turn to one specialist; best for discrete, categorised queries
- **Pipe** — chains agents sequentially; best for ordered workflows where each step depends on the previous
- **Group Chat** — runs agents in parallel, synthesizes results; best for multi-perspective quality improvement
- **Evaluator-Optimizer** — loops until a quality threshold is met; best for generation tasks where consistency matters

None of these patterns requires a different infrastructure. The same `OllamaApiClient`, the same two MCP servers, the same `McpClient.CreateAsync` setup from Part 1. The pattern is the orchestration logic — how agents are connected and when they run. The tools, the models, and the domain knowledge stay the same.

The repository is open. Clone it, run it locally with Ollama, and extend it. Add a fourth reviewer to the Group Chat. Raise the Evaluator-Optimizer threshold to 90. Chain the Pipe into the Evaluator-Optimizer. The infrastructure is already there.

---

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · **Part 10**

← [Part 9 — The Group Chat Pattern](part-9-group-chat-pattern.md)

---

## References

### NuGet Packages

- [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI) — `IChatClient`, `ChatMessage`, `ChatOptions`, `AITool`
- [OllamaSharp](https://www.nuget.org/packages/OllamaSharp) — `OllamaApiClient` for generator and evaluator agents
- [ModelContextProtocol.Core](https://www.nuget.org/packages/ModelContextProtocol.Core) — `McpClient.CreateAsync`, `HttpClientTransport` for `WriteJobDescription` and `SaveJobAnnouncement`
- [System.Text.Json](https://www.nuget.org/packages/System.Text.Json) — `JsonSerializer.Deserialize` for parsing structured evaluator output

### Microsoft Documentation

- [Microsoft.Extensions.AI overview](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai) — IChatClient, function invocation, structured output patterns
- [System.Text.Json serialization overview](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview) — `JsonSerializer`, `JsonSerializerOptions`, handling deserialization errors

### GitHub

- [DotnetMultiAgentsTutorial](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial) — Full source including `Hr.EvaluatorOrchestrator`
- [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) — Official C# MCP SDK
