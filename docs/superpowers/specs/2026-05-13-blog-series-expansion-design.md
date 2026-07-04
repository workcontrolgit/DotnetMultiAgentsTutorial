# Design: Blog Series Expansion — Parts 8–10 + Retrofit Navigation

**Date:** 2026-05-13
**Scope:** `blogs/multi-agents/` — all 11 files (Preface + Parts 1–10)

---

## Goal

1. Write three new blog posts: Part 8 (Pipe), Part 9 (Group Chat), Part 10 (Evaluator-Optimizer).
2. Retrofit all 11 posts (including existing Preface–Part 7) with:
   - Navigation header and footer (local file links + Medium URL placeholders)
   - Transition copy bridging to the next post
   - Grouped References section
3. Update `docs/blog-series-plan.md` with outlines for Parts 8–10.

---

## Approach

Sequential, file-by-file in dependency order:

```
blog-series-plan.md (add Parts 8–10 outlines)
  → Preface
    → Part 1 → Part 2 → Part 3 → Part 4 → Part 5 → Part 6 → Part 7
                                                              → Part 8 → Part 9 → Part 10
```

Each file is touched exactly once. No file requires re-editing.

---

## Navigation Structure

Every file gets an identical nav block immediately after the title/subtitle line, and a matching one at the bottom before References.

### Nav block format

```markdown
---
**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Prev Title](prev-file.md) &nbsp;|&nbsp; [Next Title](next-file.md) →

*Medium: [← Prev](MEDIUM_URL_PART_N-1) | [Next →](MEDIUM_URL_PART_N+1)*
---
```

### Edge cases
- **Preface**: no Prev link; breadcrumb shows **Preface** in bold
- **Part 10**: no Next link; Medium line shows only `← Part 9`
- **MEDIUM_URL tokens**: `MEDIUM_URL_PREFACE`, `MEDIUM_URL_PART_1` … `MEDIUM_URL_PART_10` — one find-replace pass after publishing

---

## Transition Copy

Replaces the existing "Next: Part N — Title" one-liners. Pattern:

> *One sentence summarizing what was just built.* *One sentence naming the limitation it introduces.* *One sentence previewing the next pattern as the answer.*

### Transitions per post

| Post | Key limitation introduced | Next post preview |
|------|--------------------------|-------------------|
| Preface → Part 1 | Single agent collapses under tool overload | IChatClient + MCP as the framework foundation |
| Part 1 → Part 2 | Framework primitives need a clean layer boundary | Clean architecture separates domain from AI infra |
| Part 2 → Part 3 | Architecture needs real tools behind it | HR MCP server with 9 tools over HTTP |
| Part 3 → Part 4 | LLM compliance is a liability for pass/fail law | Deterministic rule engine, zero LLM |
| Part 4 → Part 5 | Generated drafts vanish after the conversation | JobAnnouncement lifecycle with status tracking |
| Part 5 → Part 6 | 14 tools in one agent degrades quality | Selector routes each turn to one specialist |
| Part 6 → Part 7 | Orchestrator needs code; prototyping is slow | Claude Desktop drives the same servers, zero orchestrator code |
| Part 7 → Part 8 | Selector handles one turn; sequential workflows need each stage to gate the next | Pipe chains three stages: draft → compliance → status update |
| Part 8 → Part 9 | Single perspective misses what multiple experts catch | Group Chat runs three specialists in parallel, moderator synthesizes |
| Part 9 → Part 10 | First draft quality is unpredictable | Evaluator-Optimizer loops until score ≥ 80/100 |
| Part 10 | Series complete | Repository link + invitation to extend |

---

## References — Grouped Format

Each post ends with:

```markdown
---

## References

### NuGet Packages
- [Package](https://nuget.org/packages/...) — one-line description

### Microsoft Documentation
- [Title](https://learn.microsoft.com/...) — one-line description

### GitHub
- [Repo](https://github.com/...) — one-line description
```

Only categories with at least one reference are included. Empty category headings are omitted.

### Reference assignments per post

**Preface**
- NuGet: `Microsoft.Extensions.AI`, `OllamaSharp`, `ModelContextProtocol`
- MS Docs: Microsoft.Extensions.AI overview, .NET 10 What's New
- GitHub: DotnetMultiAgentsTutorial repo, modelcontextprotocol/csharp-sdk

**Part 1 (IChatClient + MCP)**
- NuGet: `Microsoft.Extensions.AI`, `OllamaSharp`, `ModelContextProtocol`, `ModelContextProtocol.Core`
- MS Docs: IChatClient middleware pipeline, Function invocation middleware
- GitHub: DotnetMultiAgentsTutorial repo

**Part 2 (Clean Architecture)**
- NuGet: `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer`
- MS Docs: EF Core getting started, Clean Architecture with ASP.NET Core
- GitHub: DotnetMultiAgentsTutorial repo

**Part 3 (HR MCP Server)**
- NuGet: `ModelContextProtocol`, `Microsoft.Extensions.AI`, `OllamaSharp`
- MS Docs: MCP .NET SDK docs, IChatClient overview
- GitHub: DotnetMultiAgentsTutorial repo, modelcontextprotocol/csharp-sdk, USAJobs API

**Part 4 (Compliance MCP)**
- NuGet: `ModelContextProtocol`
- MS Docs: MCP .NET SDK docs, OPM classification standards (opm.gov)
- GitHub: DotnetMultiAgentsTutorial repo

**Part 5 (Persistence)**
- NuGet: `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Tools`
- MS Docs: EF Core migrations, EF Core relationships
- GitHub: DotnetMultiAgentsTutorial repo

**Part 6 (Selector Pattern)**
- NuGet: `Microsoft.Extensions.AI`, `OllamaSharp`, `ModelContextProtocol.Core`
- MS Docs: IChatClient overview, Function invocation middleware
- GitHub: DotnetMultiAgentsTutorial repo

**Part 7 (Claude Desktop)**
- NuGet: `ModelContextProtocol`
- MS Docs: MCP .NET SDK docs
- GitHub: DotnetMultiAgentsTutorial repo, Claude Desktop MCP docs

**Part 8 (Pipe Pattern)**
- NuGet: `Microsoft.Extensions.AI`, `OllamaSharp`, `ModelContextProtocol.Core`
- MS Docs: IChatClient overview, Task-based async pattern
- GitHub: DotnetMultiAgentsTutorial repo, `Hr.PipeOrchestrator` source

**Part 9 (Group Chat Pattern)**
- NuGet: `Microsoft.Extensions.AI`, `OllamaSharp`, `ModelContextProtocol.Core`
- MS Docs: Task.WhenAll (parallel async), IChatClient overview
- GitHub: DotnetMultiAgentsTutorial repo, `Hr.GroupChatOrchestrator` source

**Part 10 (Evaluator-Optimizer)**
- NuGet: `Microsoft.Extensions.AI`, `OllamaSharp`, `ModelContextProtocol.Core`, `System.Text.Json`
- MS Docs: IChatClient overview, System.Text.Json serialization, Structured output patterns
- GitHub: DotnetMultiAgentsTutorial repo, `Hr.EvaluatorOrchestrator` source

---

## New Blog Post Outlines

### Part 8 — The Pipe Pattern: Sequential Agent Stages

**Hook:** The Selector handles one question per turn. But generating a job announcement, compliance-checking it, and recording the outcome are three dependent steps — each requires the previous to complete. The Pipe pattern enforces this order.

**Sections:**
- The Pipe pattern defined: linear chain, each stage transforms and passes forward
- Three-stage HR pipeline: DraftAgent → ComplianceAgent → StatusRecorder
- `DraftAgent`: calls `WriteJobDescription` + `SaveJobAnnouncement`, extracts announcement ID from response
- `ComplianceAgent`: calls `RunFullComplianceCheck`, returns `(report, passed)` tuple
- `HrPipeline`: user confirmation gates between stages — semi-automated by design
- Stage 3: `UpdateAnnouncementStatus` with `CompliancePassed` or `ComplianceFailed`
- When to use Pipe vs. Selector: ordered transformation vs. categorical routing

**Code references:**
- `src/Hr.PipeOrchestrator/Pipeline/HrPipeline.cs`
- `src/Hr.PipeOrchestrator/Agents/DraftAgent.cs`
- `src/Hr.PipeOrchestrator/Agents/ComplianceAgent.cs`

---

### Part 9 — The Group Chat Pattern: Parallel Expert Review

**Hook:** A single reviewer misses things. Three domain experts reviewing independently — without seeing each other's feedback — catch more. A moderator then synthesizes their critiques into a revised draft. This is the Group Chat (Debate) pattern.

**Sections:**
- The Group Chat pattern defined: N agents in parallel, one moderator synthesizes
- Why parallel and blind: `Task.WhenAll` with no shared state eliminates anchoring bias
- The three reviewers: HrSpecialist (terminology, structure), LegalReviewer (compliance language), BudgetAnalyst (pay grade justification)
- `ReviewerAgent`: shared class, differentiated by system prompt
- `ModeratorAgent.SynthesizeAsync`: receives all three critiques, produces revised draft
- `HrGroupChat`: load draft → parallel review → synthesize → save
- When to use Group Chat vs. Pipe: multi-perspective evaluation vs. sequential transformation

**Code references:**
- `src/Hr.GroupChatOrchestrator/Chat/HrGroupChat.cs`
- `src/Hr.GroupChatOrchestrator/Agents/ReviewerAgent.cs`

---

### Part 10 — The Evaluator-Optimizer Pattern: Quality-Gated Generation

**Hook:** First drafts are rarely good enough. The Evaluator-Optimizer pattern runs a generate-evaluate-improve loop until a quality threshold is met — or until a maximum iteration count is reached, saving the best draft regardless.

**Sections:**
- The Evaluator-Optimizer pattern defined: generator + evaluator in a feedback loop
- `GeneratorAgent`: first pass generates from position data; subsequent passes receive structured feedback
- `EvaluatorAgent`: 4-criterion rubric (Clarity, OPM Language, Completeness, Tone), 25 pts each, returns JSON score
- `EvaluationResult`: typed model parsed from LLM JSON; `JsonException` catch forces score 0 → retry
- `EvaluatorOptimizerLoop`: threshold 80/100, max 3 iterations, tracks best draft, user confirmation between iterations
- Save step: highest-scoring draft saved via `SaveJobAnnouncement` regardless of whether threshold was met
- When to use Evaluator-Optimizer vs. Group Chat: iterative quality improvement vs. multi-perspective synthesis
- Series closing: all four patterns, same domain, same MCP servers — mix and chain as needed

**Code references:**
- `src/Hr.EvaluatorOrchestrator/Loop/EvaluatorOptimizerLoop.cs`
- `src/Hr.EvaluatorOrchestrator/Agents/GeneratorAgent.cs`
- `src/Hr.EvaluatorOrchestrator/Agents/EvaluatorAgent.cs`
- `src/Hr.EvaluatorOrchestrator/Models/EvaluationResult.cs`

---

## Files Changed

| File | Change type |
|------|-------------|
| `docs/blog-series-plan.md` | Add Parts 8–10 outlines |
| `blogs/multi-agents/preface-why-one-agent-is-not-enough.md` | Add nav + transition + references |
| `blogs/multi-agents/part-1-dotnet-agent-framework.md` | Add nav + transition + references |
| `blogs/multi-agents/part-2-clean-architecture-for-ai.md` | Add nav + transition + references |
| `blogs/multi-agents/part-3-hr-data-mcp-server.md` | Add nav + transition + references |
| `blogs/multi-agents/part-4-compliance-mcp-deterministic-rules.md` | Add nav + transition + references |
| `blogs/multi-agents/part-5-persisting-ai-artifacts.md` | Add nav + transition + references |
| `blogs/multi-agents/part-6-selector-pattern.md` | Add nav + transition + references |
| `blogs/multi-agents/part-7-claude-desktop-multi-agent.md` | Add nav + transition + references |
| `blogs/multi-agents/part-8-pipe-pattern.md` | **New file** — full post |
| `blogs/multi-agents/part-9-group-chat-pattern.md` | **New file** — full post |
| `blogs/multi-agents/part-10-evaluator-optimizer-pattern.md` | **New file** — full post |

---

## Constraints (from cerebrum.md)

- No markdown tables in blog post body — use bullet lists or prose
- Code blocks with language tags (`csharp`, `bash`, `json`)
- No relative image paths in post content — absolute hosted URLs only
- Each post under 2,500 words
- End each post with nav footer + References
