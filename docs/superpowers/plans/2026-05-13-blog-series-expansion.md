# Blog Series Expansion — Parts 8–10 + Retrofit Navigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Retrofit all 11 blog posts with navigation, transitions, and grouped References; write three new posts for the Pipe, Group Chat, and Evaluator-Optimizer patterns.

**Architecture:** Sequential file-by-file edits in dependency order. Existing posts (Preface–Part 7) each get a nav header prepended and their closing replaced with a transition paragraph + nav footer + References section. Three new posts (Parts 8–10) are created in full.

**Tech Stack:** Markdown, `blogs/multi-agents/`, source in `DotnetMultiAgents/src/Hr.PipeOrchestrator/`, `Hr.GroupChatOrchestrator/`, `Hr.EvaluatorOrchestrator/`

---

## Shared Nav Block Reference

Every file uses this breadcrumb (bold the current post). Each post also gets a Prev/Next line and Medium placeholder line. Defined once here to avoid repetition.

**Full breadcrumb template** — replace the current post link with plain bold text:

```
**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)
```

**Nav block structure per post:**

```markdown
---
**Series:** [breadcrumb with current post bolded]

← [Prev Title](prev-file.md) &nbsp;|&nbsp; [Next Title →](next-file.md)

*Medium: [← Prev](MEDIUM_URL_PREV) | [Next →](MEDIUM_URL_NEXT)*

---
```

Edge cases:
- Preface: no Prev line; Medium line shows only `[Part 1 →](MEDIUM_URL_PART_1)`
- Part 10: no Next line; Medium line shows only `[← Part 9](MEDIUM_URL_PART_9)`

---

## Task 0: Update blog-series-plan.md

**Files:**
- Modify: `docs/blog-series-plan.md`

- [ ] **Step 1: Add Parts 8–10 to the series table**

Open `docs/blog-series-plan.md`. Find the series table (ends at Part 7). Append these three rows after the Part 7 row:

```markdown
| Part 8 | The Pipe Pattern: Sequential Agent Stages | Pipe | Hr.PipeOrchestrator | pending |
| Part 9 | The Group Chat Pattern: Parallel Expert Review | Group Chat | Hr.GroupChatOrchestrator | pending |
| Part 10 | The Evaluator-Optimizer Pattern: Quality-Gated Generation | Evaluator-Optimizer | Hr.EvaluatorOrchestrator | pending |
```

- [ ] **Step 2: Add Part 8 outline after the Part 7 outline**

Append after the Part 7 section:

```markdown
---

### Part 8 — The Pipe Pattern: Sequential Agent Stages

**Hook:** The Selector handles one question per turn. But generating a job announcement, compliance-checking it, and recording the outcome are three dependent steps — each requires the previous to complete. The Pipe pattern enforces this order.

**Sections:**
- The Pipe pattern defined: linear chain, each stage transforms and passes forward
- Three-stage HR pipeline: DraftAgent → ComplianceAgent → status update
- `DraftAgent`: calls `WriteJobDescription` + `SaveJobAnnouncement`, extracts announcement ID via `ANNOUNCEMENT_ID:<id>` token
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

**Hook:** A single reviewer misses things. Three domain experts reviewing independently — without seeing each other's feedback — catch more. A moderator synthesizes their critiques into a revised draft. This is the Group Chat (Debate) pattern.

**Sections:**
- The Group Chat pattern defined: N agents in parallel, one moderator synthesizes
- Why parallel and blind: `Task.WhenAll` with no shared state eliminates anchoring bias
- The three reviewers: HrSpecialist (terminology, structure), LegalReviewer (compliance language), BudgetAnalyst (pay grade justification)
- `ReviewerAgent`: shared class, differentiated by system prompt; `ReviewAsync` and `SynthesizeAsync` methods
- `HrGroupChat`: load draft → parallel review (Round 1) → moderator synthesis (Round 2) → save
- When to use Group Chat vs. Pipe: multi-perspective evaluation vs. sequential transformation

**Code references:**
- `src/Hr.GroupChatOrchestrator/Chat/HrGroupChat.cs`
- `src/Hr.GroupChatOrchestrator/Agents/ReviewerAgent.cs`

---

### Part 10 — The Evaluator-Optimizer Pattern: Quality-Gated Generation

**Hook:** First drafts are rarely good enough. The Evaluator-Optimizer pattern runs a generate-evaluate-improve loop until a quality threshold is met — or until a maximum iteration count is reached, always saving the best draft.

**Sections:**
- The Evaluator-Optimizer pattern defined: generator + evaluator in a feedback loop
- `GeneratorAgent`: first pass uses position data only; subsequent passes inject structured evaluator feedback
- `EvaluatorAgent`: 4-criterion rubric (Clarity, OPM Language, Completeness, Tone), 25 pts each; returns JSON score
- `EvaluationResult`: typed model parsed from LLM JSON; `JsonException` catch forces score 0 and retry
- `EvaluatorOptimizerLoop`: threshold 80/100, max 3 iterations, tracks best draft across iterations
- Save step: highest-scoring draft persisted via `SaveJobAnnouncement` regardless of threshold
- When to use Evaluator-Optimizer vs. Group Chat: iterative quality improvement vs. multi-perspective synthesis
- Series closing: all four patterns compose — mix and chain using the same MCP servers

**Code references:**
- `src/Hr.EvaluatorOrchestrator/Loop/EvaluatorOptimizerLoop.cs`
- `src/Hr.EvaluatorOrchestrator/Agents/GeneratorAgent.cs`
- `src/Hr.EvaluatorOrchestrator/Agents/EvaluatorAgent.cs`
- `src/Hr.EvaluatorOrchestrator/Models/EvaluationResult.cs`
```

- [ ] **Step 3: Update the Dependency Order section**

Find the dependency order diagram at the bottom and replace it with:

```markdown
## Dependency Order

Posts must be written in this order (Parts 3 and 4 can be written in parallel):

```
Preface
  └── Part 1 (IChatClient + MCP)
        └── Part 2 (Clean Architecture)
              ├── Part 3 (HR MCP Server)    ──┐
              └── Part 4 (Compliance MCP)   ──┤
                                              └── Part 5 (Persistence)
                                                    └── Part 6 (Selector Pattern)
                                                          └── Part 7 (Claude Desktop)
                                                                └── Part 8 (Pipe)
                                                                      └── Part 9 (Group Chat)
                                                                            └── Part 10 (Evaluator-Optimizer)
```
```

- [ ] **Step 4: Commit**

```bash
git add docs/blog-series-plan.md
git commit -m "docs: add Parts 8-10 outlines to blog series plan"
```

---

## Task 1: Retrofit Preface

**Files:**
- Modify: `blogs/multi-agents/preface-why-one-agent-is-not-enough.md`

- [ ] **Step 1: Insert nav header after the opening separator**

After the line `*Preface to: Building Multi-Agent Systems with .NET 10*`, find the first `---` separator. Insert the following nav block immediately after that `---`:

```markdown
**Series:** **Preface** · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

[Part 1 — The .NET Agent Framework →](part-1-dotnet-agent-framework.md)

*Medium: [Part 1 →](MEDIUM_URL_PART_1)*

---
```

- [ ] **Step 2: Replace the closing section**

Find and replace this exact closing block:

```
**Next: Part 1 — The .NET Agent Framework: IChatClient and MCP Clients**

[View the repository](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial)
```

Replace with:

```markdown
The four patterns this series covers — Selector, Pipe, Group Chat, and Evaluator-Optimizer — all share the same foundation: `IChatClient` as the universal agent primitive and MCP as the tool delivery layer. Before building the first multi-agent system, Part 1 unpacks exactly what those abstractions do and why they make it practical to run five specialist agents without duplicating infrastructure.

---

**Series:** **Preface** · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

[Part 1 — The .NET Agent Framework →](part-1-dotnet-agent-framework.md)

*Medium: [Part 1 →](MEDIUM_URL_PART_1)*

---

## References

### NuGet Packages

- [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI) — Unified AI client abstractions (`IChatClient`, `ChatMessage`, `AITool`) for .NET
- [OllamaSharp](https://www.nuget.org/packages/OllamaSharp) — .NET client for Ollama; `OllamaApiClient` implements `IChatClient` natively in v5+
- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) — Official .NET MCP server SDK (`[McpServerTool]`, `[McpServerToolType]`)

### Microsoft Documentation

- [Microsoft.Extensions.AI overview](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai) — IChatClient, middleware pipeline, and AI abstractions for .NET
- [What's new in .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview) — Runtime and SDK improvements used in this series

### GitHub

- [DotnetMultiAgentsTutorial](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial) — Full source for all patterns in this series
- [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) — Official C# MCP SDK
```

- [ ] **Step 3: Commit**

```bash
git add blogs/multi-agents/preface-why-one-agent-is-not-enough.md
git commit -m "docs(blog): add nav, transition, and references to Preface"
```

---

## Task 2: Retrofit Part 1

**Files:**
- Modify: `blogs/multi-agents/part-1-dotnet-agent-framework.md`

- [ ] **Step 1: Insert nav header after the opening separator**

After the first `---` separator (after `*Part 1 of: Building Multi-Agent Systems with .NET 10*`), insert:

```markdown
**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · **Part 1** · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Preface](preface-why-one-agent-is-not-enough.md) &nbsp;|&nbsp; [Part 2 — Clean Architecture for AI Applications →](part-2-clean-architecture-for-ai.md)

*Medium: [← Preface](MEDIUM_URL_PREFACE) | [Part 2 →](MEDIUM_URL_PART_2)*

---
```

- [ ] **Step 2: Replace the closing section**

Find and replace:

```
**Next: Part 2 — Clean Architecture for AI Applications**

[View the repository](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial)
```

Replace with:

```markdown
You now have the framework primitives: `IChatClient`, the `UseFunctionInvocation` middleware, and the MCP client that turns remote tool servers into local tool lists. The next step is to arrange these primitives into a codebase that is testable, maintainable, and swap-friendly at every layer. Part 2 applies clean architecture to the HR system and explains why the LLM belongs in the infrastructure layer, not the domain.

---

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · **Part 1** · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Preface](preface-why-one-agent-is-not-enough.md) &nbsp;|&nbsp; [Part 2 — Clean Architecture for AI Applications →](part-2-clean-architecture-for-ai.md)

*Medium: [← Preface](MEDIUM_URL_PREFACE) | [Part 2 →](MEDIUM_URL_PART_2)*

---

## References

### NuGet Packages

- [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI) — `IChatClient`, `ChatMessage`, `AITool`, and the `UseFunctionInvocation` middleware
- [OllamaSharp](https://www.nuget.org/packages/OllamaSharp) — `OllamaApiClient` implements `IChatClient`; cast to `IChatClient` before calling `.AsBuilder()`
- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) — MCP server SDK
- [ModelContextProtocol.Core](https://www.nuget.org/packages/ModelContextProtocol.Core) — MCP client API (`McpClient`, `HttpClientTransport`, `McpClientTool`)

### Microsoft Documentation

- [Microsoft.Extensions.AI overview](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai) — IChatClient, middleware pipeline, function invocation
- [IChatClient interface](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.ichatclient) — Full API reference

### GitHub

- [DotnetMultiAgentsTutorial](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial) — Full source for all patterns in this series
- [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) — Official C# MCP SDK
```

- [ ] **Step 3: Commit**

```bash
git add blogs/multi-agents/part-1-dotnet-agent-framework.md
git commit -m "docs(blog): add nav, transition, and references to Part 1"
```

---

## Task 3: Retrofit Part 2

**Files:**
- Modify: `blogs/multi-agents/part-2-clean-architecture-for-ai.md`

- [ ] **Step 1: Insert nav header after the opening separator**

After the first `---` separator, insert:

```markdown
**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · **Part 2** · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 1 — The .NET Agent Framework](part-1-dotnet-agent-framework.md) &nbsp;|&nbsp; [Part 3 — Building the HR Data MCP Server →](part-3-hr-data-mcp-server.md)

*Medium: [← Part 1](MEDIUM_URL_PART_1) | [Part 3 →](MEDIUM_URL_PART_3)*

---
```

- [ ] **Step 2: Replace the closing section**

Find and replace the last `**Next: Part 3 ...` block and `[View the repository]` line with:

```markdown
The layer boundaries are in place and the `JobAnnouncement` lifecycle is designed on paper. Part 3 puts real tools behind the architecture: the HR Jobs MCP server with nine endpoints covering position search, job description generation, and announcement persistence.

---

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · **Part 2** · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 1 — The .NET Agent Framework](part-1-dotnet-agent-framework.md) &nbsp;|&nbsp; [Part 3 — Building the HR Data MCP Server →](part-3-hr-data-mcp-server.md)

*Medium: [← Part 1](MEDIUM_URL_PART_1) | [Part 3 →](MEDIUM_URL_PART_3)*

---

## References

### NuGet Packages

- [Microsoft.EntityFrameworkCore](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore) — ORM for `HrDbContext`, entities, and migrations
- [Microsoft.EntityFrameworkCore.SqlServer](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer) — SQL Server LocalDB provider
- [Microsoft.EntityFrameworkCore.Tools](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Tools) — `dotnet ef` CLI for migrations

### Microsoft Documentation

- [EF Core — Getting Started](https://learn.microsoft.com/en-us/ef/core/get-started/overview/first-app) — DbContext, entities, and DbSet configuration
- [Common web application architectures](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures) — Clean Architecture layers and dependency rule

### GitHub

- [DotnetMultiAgentsTutorial](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial) — Full source for all patterns in this series
```

- [ ] **Step 3: Commit**

```bash
git add blogs/multi-agents/part-2-clean-architecture-for-ai.md
git commit -m "docs(blog): add nav, transition, and references to Part 2"
```

---

## Task 4: Retrofit Part 3

**Files:**
- Modify: `blogs/multi-agents/part-3-hr-data-mcp-server.md`

- [ ] **Step 1: Insert nav header after the opening separator**

After the first `---` separator, insert:

```markdown
**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · **Part 3** · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 2 — Clean Architecture for AI Applications](part-2-clean-architecture-for-ai.md) &nbsp;|&nbsp; [Part 4 — Compliance MCP: Deterministic Rules, Zero LLM →](part-4-compliance-mcp-deterministic-rules.md)

*Medium: [← Part 2](MEDIUM_URL_PART_2) | [Part 4 →](MEDIUM_URL_PART_4)*

---
```

- [ ] **Step 2: Replace the closing section**

Find and replace the last `**Next: Part 4 ...` block and `[View the repository]` line with:

```markdown
The HR Jobs server handles data queries and LLM-powered draft generation. But compliance checking is different — the rules are statutory, the outcomes are pass/fail by law, and a hallucinating LLM is a liability. Part 4 builds a second MCP server whose 7 rules run entirely in deterministic C#, with zero `IChatClient` dependency.

---

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · **Part 3** · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 2 — Clean Architecture for AI Applications](part-2-clean-architecture-for-ai.md) &nbsp;|&nbsp; [Part 4 — Compliance MCP: Deterministic Rules, Zero LLM →](part-4-compliance-mcp-deterministic-rules.md)

*Medium: [← Part 2](MEDIUM_URL_PART_2) | [Part 4 →](MEDIUM_URL_PART_4)*

---

## References

### NuGet Packages

- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) — `[McpServerTool]`, `[McpServerToolType]`, MCP server host integration
- [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI) — `IChatClient` used by `WriteJobDescription` for LLM-powered tool
- [OllamaSharp](https://www.nuget.org/packages/OllamaSharp) — `OllamaApiClient` backing the in-server `WriteJobDescription` call

### Microsoft Documentation

- [.NET MCP SDK — Getting Started](https://learn.microsoft.com/en-us/dotnet/ai/model-context-protocol) — `[McpServerTool]` attribute, DI registration, server hosting
- [Microsoft.Extensions.AI overview](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai) — IChatClient usage inside MCP tools

### GitHub

- [DotnetMultiAgentsTutorial](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial) — Full source for all patterns in this series
- [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) — Official C# MCP SDK
- [USAJobs Developer Portal](https://developer.usajobs.gov/) — API used to seed position data
```

- [ ] **Step 3: Commit**

```bash
git add blogs/multi-agents/part-3-hr-data-mcp-server.md
git commit -m "docs(blog): add nav, transition, and references to Part 3"
```

---

## Task 5: Retrofit Part 4

**Files:**
- Modify: `blogs/multi-agents/part-4-compliance-mcp-deterministic-rules.md`

- [ ] **Step 1: Insert nav header after the opening separator**

After the first `---` separator, insert:

```markdown
**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · **Part 4** · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 3 — Building the HR Data MCP Server](part-3-hr-data-mcp-server.md) &nbsp;|&nbsp; [Part 5 — Persisting AI Artifacts →](part-5-persisting-ai-artifacts.md)

*Medium: [← Part 3](MEDIUM_URL_PART_3) | [Part 5 →](MEDIUM_URL_PART_5)*

---
```

- [ ] **Step 2: Replace the closing section**

Find and replace the last `**Next: Part 5 ...` block and `[View the repository]` line with:

```markdown
Both MCP servers are running and tested. But `WriteJobDescription` returns a string that disappears when the conversation ends. Part 5 introduces the `JobAnnouncement` entity and lifecycle — Draft, CompliancePassed, ComplianceFailed, Published — so every generated draft persists across sessions with a full audit trail.

---

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · **Part 4** · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 3 — Building the HR Data MCP Server](part-3-hr-data-mcp-server.md) &nbsp;|&nbsp; [Part 5 — Persisting AI Artifacts →](part-5-persisting-ai-artifacts.md)

*Medium: [← Part 3](MEDIUM_URL_PART_3) | [Part 5 →](MEDIUM_URL_PART_5)*

---

## References

### NuGet Packages

- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) — MCP server SDK; this server has zero `IChatClient` dependency by design

### Microsoft Documentation

- [.NET MCP SDK — Getting Started](https://learn.microsoft.com/en-us/dotnet/ai/model-context-protocol) — `[McpServerTool]` attribute and server hosting
- [OPM Classification Standards](https://www.opm.gov/policy-data-oversight/classification-qualifications/classifying-general-schedule-positions/) — Authoritative source for the 7 compliance rules

### GitHub

- [DotnetMultiAgentsTutorial](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial) — Full source for all patterns in this series
- [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) — Official C# MCP SDK
```

- [ ] **Step 3: Commit**

```bash
git add blogs/multi-agents/part-4-compliance-mcp-deterministic-rules.md
git commit -m "docs(blog): add nav, transition, and references to Part 4"
```

---

## Task 6: Retrofit Part 5

**Files:**
- Modify: `blogs/multi-agents/part-5-persisting-ai-artifacts.md`

- [ ] **Step 1: Insert nav header after the opening separator**

After the first `---` separator, insert:

```markdown
**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · **Part 5** · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 4 — Compliance MCP: Deterministic Rules, Zero LLM](part-4-compliance-mcp-deterministic-rules.md) &nbsp;|&nbsp; [Part 6 — The Selector Pattern →](part-6-selector-pattern.md)

*Medium: [← Part 4](MEDIUM_URL_PART_4) | [Part 6 →](MEDIUM_URL_PART_6)*

---
```

- [ ] **Step 2: Replace the closing section**

Find and replace the last `**Next: Part 6 ...` block and `[View the repository]` line with:

```markdown
The persistence layer is in place and all 14 tools work end to end. The next problem is quality: a single agent holding all 14 tools writes worse job descriptions than one focused on writing alone. Part 6 introduces the Selector pattern — a router that classifies each user turn and delegates it to one specialist with a scoped tool set and a focused system prompt.

---

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · **Part 5** · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 4 — Compliance MCP: Deterministic Rules, Zero LLM](part-4-compliance-mcp-deterministic-rules.md) &nbsp;|&nbsp; [Part 6 — The Selector Pattern →](part-6-selector-pattern.md)

*Medium: [← Part 4](MEDIUM_URL_PART_4) | [Part 6 →](MEDIUM_URL_PART_6)*

---

## References

### NuGet Packages

- [Microsoft.EntityFrameworkCore](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore) — `JobAnnouncement` entity, `HrDbContext`, migrations
- [Microsoft.EntityFrameworkCore.SqlServer](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer) — SQL Server LocalDB provider
- [Microsoft.EntityFrameworkCore.Tools](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Tools) — `dotnet ef migrations add` and `dotnet ef database update`

### Microsoft Documentation

- [EF Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/) — Adding, applying, and rolling back schema migrations
- [EF Core Relationships](https://learn.microsoft.com/en-us/ef/core/modeling/relationships) — FK from `JobAnnouncement` to `Position`, cascade delete

### GitHub

- [DotnetMultiAgentsTutorial](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial) — Full source for all patterns in this series
```

- [ ] **Step 3: Commit**

```bash
git add blogs/multi-agents/part-5-persisting-ai-artifacts.md
git commit -m "docs(blog): add nav, transition, and references to Part 5"
```

---

## Task 7: Retrofit Part 6

**Files:**
- Modify: `blogs/multi-agents/part-6-selector-pattern.md`

- [ ] **Step 1: Insert nav header after the opening separator**

After the first `---` separator, insert:

```markdown
**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · **Part 6** · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 5 — Persisting AI Artifacts](part-5-persisting-ai-artifacts.md) &nbsp;|&nbsp; [Part 7 — Claude Desktop as Multi-Agent Platform →](part-7-claude-desktop-multi-agent.md)

*Medium: [← Part 5](MEDIUM_URL_PART_5) | [Part 7 →](MEDIUM_URL_PART_7)*

---
```

- [ ] **Step 2: Replace the closing section**

Find and replace:

```
**Next: Part 7 — Claude Desktop as Multi-Agent Platform**

[View the repository](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial)
```

Replace with:

```markdown
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
```

- [ ] **Step 3: Commit**

```bash
git add blogs/multi-agents/part-6-selector-pattern.md
git commit -m "docs(blog): add nav, transition, and references to Part 6"
```

---

## Task 8: Retrofit Part 7

**Files:**
- Modify: `blogs/multi-agents/part-7-claude-desktop-multi-agent.md`

- [ ] **Step 1: Insert nav header after the opening separator**

After the first `---` separator, insert:

```markdown
**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · **Part 7** · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 6 — The Selector Pattern](part-6-selector-pattern.md) &nbsp;|&nbsp; [Part 8 — The Pipe Pattern →](part-8-pipe-pattern.md)

*Medium: [← Part 6](MEDIUM_URL_PART_6) | [Part 8 →](MEDIUM_URL_PART_8)*

---
```

- [ ] **Step 2: Replace the closing section**

Find and replace the entire closing block starting from `## What Comes Next` through the end of the file (`[View the repository](...)`):

```markdown
## What Comes Next

The Selector pattern routes each turn to one specialist. But some workflows are inherently sequential — a job announcement must be drafted before it can be compliance-checked, and the compliance outcome must be recorded before the announcement moves forward. Part 8 introduces the Pipe pattern, where each agent's output becomes the next stage's input and no stage runs until the previous one completes.

---

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · **Part 7** · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 6 — The Selector Pattern](part-6-selector-pattern.md) &nbsp;|&nbsp; [Part 8 — The Pipe Pattern →](part-8-pipe-pattern.md)

*Medium: [← Part 6](MEDIUM_URL_PART_6) | [Part 8 →](MEDIUM_URL_PART_8)*

---

## References

### NuGet Packages

- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) — MCP server SDK; `--stdio` flag enables Claude Desktop transport

### Microsoft Documentation

- [.NET MCP SDK — Getting Started](https://learn.microsoft.com/en-us/dotnet/ai/model-context-protocol) — stdio transport configuration
- [Claude Desktop MCP documentation](https://modelcontextprotocol.io/quickstart/user) — `claude_desktop_config.json` format and server registration

### GitHub

- [DotnetMultiAgentsTutorial](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial) — Full source for all patterns in this series
- [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) — Official C# MCP SDK
```

- [ ] **Step 3: Commit**

```bash
git add blogs/multi-agents/part-7-claude-desktop-multi-agent.md
git commit -m "docs(blog): add nav, transition, and references to Part 7"
```

---

## Task 9: Write Part 8 — The Pipe Pattern

**Files:**
- Create: `blogs/multi-agents/part-8-pipe-pattern.md`

**Source files to reference (local links from `blogs/multi-agents/`):**
- [HrPipeline.cs](../../DotnetMultiAgents/src/Hr.PipeOrchestrator/Pipeline/HrPipeline.cs)
- [DraftAgent.cs](../../DotnetMultiAgents/src/Hr.PipeOrchestrator/Agents/DraftAgent.cs)
- [ComplianceAgent.cs](../../DotnetMultiAgents/src/Hr.PipeOrchestrator/Agents/ComplianceAgent.cs)

- [ ] **Step 1: Create the file with the following complete content**

```markdown
# Part 8 — The Pipe Pattern: Sequential Agent Stages

*Part 8 of: Building Multi-Agent Systems with .NET 10*

---

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · **Part 8** · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 7 — Claude Desktop as Multi-Agent Platform](part-7-claude-desktop-multi-agent.md) &nbsp;|&nbsp; [Part 9 — The Group Chat Pattern →](part-9-group-chat-pattern.md)

*Medium: [← Part 7](MEDIUM_URL_PART_7) | [Part 9 →](MEDIUM_URL_PART_9)*

---

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
    You are a federal HR compliance checker operating in an automated pipeline.
    When given a position ID:
    1. Call RunFullComplianceCheck to run all 7 OPM compliance rules.
    2. Return the full compliance report.
    3. On its own line at the end, write either: COMPLIANCE_RESULT:PASSED or COMPLIANCE_RESULT:FAILED
    Do not ask questions. Run the check and report immediately.
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
        statusMessages, new ChatOptions { Tools = [updateStatusTool] }, ct);

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
# Terminal 1
dotnet run --project src/Hr.Jobs.Mcp

# Terminal 2
dotnet run --project src/Hr.Compliance.Mcp

# Terminal 3
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

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · **Part 8** · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 7 — Claude Desktop as Multi-Agent Platform](part-7-claude-desktop-multi-agent.md) &nbsp;|&nbsp; [Part 9 — The Group Chat Pattern →](part-9-group-chat-pattern.md)

*Medium: [← Part 7](MEDIUM_URL_PART_7) | [Part 9 →](MEDIUM_URL_PART_9)*

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
```

- [ ] **Step 2: Commit**

```bash
git add blogs/multi-agents/part-8-pipe-pattern.md
git commit -m "docs(blog): add Part 8 — Pipe Pattern"
```

---

## Task 10: Write Part 9 — The Group Chat Pattern

**Files:**
- Create: `blogs/multi-agents/part-9-group-chat-pattern.md`

**Source files to reference (local links from `blogs/multi-agents/`):**
- [HrGroupChat.cs](../../DotnetMultiAgents/src/Hr.GroupChatOrchestrator/Chat/HrGroupChat.cs)
- [ReviewerAgent.cs](../../DotnetMultiAgents/src/Hr.GroupChatOrchestrator/Agents/ReviewerAgent.cs)

- [ ] **Step 1: Create the file with the following complete content**

```markdown
# Part 9 — The Group Chat Pattern: Parallel Expert Review

*Part 9 of: Building Multi-Agent Systems with .NET 10*

---

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · **Part 9** · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 8 — The Pipe Pattern](part-8-pipe-pattern.md) &nbsp;|&nbsp; [Part 10 — The Evaluator-Optimizer Pattern →](part-10-evaluator-optimizer-pattern.md)

*Medium: [← Part 8](MEDIUM_URL_PART_8) | [Part 10 →](MEDIUM_URL_PART_10)*

---

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

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · **Part 9** · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 8 — The Pipe Pattern](part-8-pipe-pattern.md) &nbsp;|&nbsp; [Part 10 — The Evaluator-Optimizer Pattern →](part-10-evaluator-optimizer-pattern.md)

*Medium: [← Part 8](MEDIUM_URL_PART_8) | [Part 10 →](MEDIUM_URL_PART_10)*

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
```

- [ ] **Step 2: Commit**

```bash
git add blogs/multi-agents/part-9-group-chat-pattern.md
git commit -m "docs(blog): add Part 9 — Group Chat Pattern"
```

---

## Task 11: Write Part 10 — The Evaluator-Optimizer Pattern

**Files:**
- Create: `blogs/multi-agents/part-10-evaluator-optimizer-pattern.md`

**Source files to reference (local links from `blogs/multi-agents/`):**
- [EvaluatorOptimizerLoop.cs](../../DotnetMultiAgents/src/Hr.EvaluatorOrchestrator/Loop/EvaluatorOptimizerLoop.cs)
- [GeneratorAgent.cs](../../DotnetMultiAgents/src/Hr.EvaluatorOrchestrator/Agents/GeneratorAgent.cs)
- [EvaluatorAgent.cs](../../DotnetMultiAgents/src/Hr.EvaluatorOrchestrator/Agents/EvaluatorAgent.cs)
- [EvaluationResult.cs](../../DotnetMultiAgents/src/Hr.EvaluatorOrchestrator/Models/EvaluationResult.cs)

- [ ] **Step 1: Create the file with the following complete content**

```markdown
# Part 10 — The Evaluator-Optimizer Pattern: Quality-Gated Generation

*Part 10 of: Building Multi-Agent Systems with .NET 10*

---

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · **Part 10**

← [Part 9 — The Group Chat Pattern](part-9-group-chat-pattern.md)

*Medium: [← Part 9](MEDIUM_URL_PART_9)*

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

*Medium: [← Part 9](MEDIUM_URL_PART_9)*

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
```

- [ ] **Step 2: Commit**

```bash
git add blogs/multi-agents/part-10-evaluator-optimizer-pattern.md
git commit -m "docs(blog): add Part 10 — Evaluator-Optimizer Pattern"
```

---

## Self-Review Checklist

Run through these before starting execution:

- [ ] All 11 blog files accounted for (Preface + Parts 1–10)
- [ ] `blog-series-plan.md` updated with Parts 8–10 outlines and dependency order
- [ ] Every nav header uses the correct bold entry for the current post
- [ ] Every nav footer matches the nav header in the same file
- [ ] `MEDIUM_URL_PART_N` tokens consistent: PREFACE, PART_1 … PART_10
- [ ] No empty Reference categories (headings only appear when they have content)
- [ ] No markdown tables in blog post bodies (References use bullet lists)
- [ ] Each new post body stays under 2,500 words
- [ ] Source file local links use correct relative paths from `blogs/multi-agents/` to `DotnetMultiAgents/src/`
- [ ] Part 10 has no "Next" link (last in series)
- [ ] Preface has no "Prev" link (first in series)
