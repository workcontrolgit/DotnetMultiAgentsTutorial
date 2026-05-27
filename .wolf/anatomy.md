# anatomy.md

> Auto-maintained by OpenWolf. Last scanned: 2026-05-14T12:22:48.042Z
> Files: 66 tracked | Anatomy hits: 0 | Misses: 0

## ../../Users/Fuji Nguyen/.claude/plugins/

- `installed_plugins.json` (~1105 tok)

## ../../Users/Fuji Nguyen/.claude/plugins/cache/local/my-skills/1.0.0/

- `package.json` — Node.js package manifest (~20 tok)

## ../../Users/Fuji Nguyen/.claude/projects/c--apps-DotnetMultiAgentsTutorial/memory/

- `feedback_commit_messages.md` (~98 tok)
- `MEMORY.md` — Memory Index (~33 tok)

## ../DotnetMcpTutorial/tests/

- `seed.spec.ts` (~35 tok)

## ./

- `.mcp.json` (~104 tok)
- `playwright.config.ts` (~25 tok)
- `README.md` — Project documentation (~3061 tok)

## .wolf/


## DotnetMultiAgents/

- `DotnetMultiAgents.slnx` (~200 tok)

## DotnetMultiAgents/src/Hr.Agent/

- `appsettings.json` (~94 tok)
- `Hr.Agent.csproj` (~225 tok)
- `Program.cs` — src/Hr.Agent/Program.cs (~674 tok)

## DotnetMultiAgents/src/Hr.Application/

- `Hr.Application.csproj` — References Hr.Core (~30 tok)

## DotnetMultiAgents/src/Hr.Application/Services/

- `JobAnnouncementService.cs` — src/Hr.Application/Services/JobAnnouncementService.cs (~297 tok)
- `PositionService.cs` — src/Hr.Application/Services/PositionService.cs (~260 tok)

## DotnetMultiAgents/src/Hr.Compliance.Mcp/

- `Hr.Compliance.Mcp.csproj` — MCP server, port 5200; references Hr.Core + Hr.Infrastructure (~50 tok)

## DotnetMultiAgents/src/Hr.Core/

- `Hr.Core.csproj` — Domain layer, no dependencies (~30 tok)

## DotnetMultiAgents/src/Hr.Core/Entities/

- `JobAnnouncement.cs` — A generated job announcement draft for a Position. A position can have multiple drafts over time; on (~304 tok)

## DotnetMultiAgents/src/Hr.Core/Enums/

- `AnnouncementStatus.cs` — Generated but not yet compliance-checked. (~120 tok)

## DotnetMultiAgents/src/Hr.Core/Interfaces/

- `IJobAnnouncementRepository.cs` — src/Hr.Core/Interfaces/IJobAnnouncementRepository.cs (~163 tok)
- `IPositionRepository.cs` — src/Hr.Core/Interfaces/IPositionRepository.cs (~164 tok)

## DotnetMultiAgents/src/Hr.EvaluatorOrchestrator/

- `Hr.EvaluatorOrchestrator.csproj` (~126 tok)
- `Program.cs` — src/Hr.EvaluatorOrchestrator/Program.cs (~29 tok)

## DotnetMultiAgents/src/Hr.EvaluatorOrchestrator/Agents/

- `EvaluatorAgent.cs` — Scores a job announcement draft against a 4-criterion rubric (25 pts each, 100 max). Returns a struc (~683 tok)

## DotnetMultiAgents/src/Hr.EvaluatorOrchestrator/Loop/

- `EvaluatorOptimizerLoop.cs` — Implements the Evaluator-Optimizer pattern. Each iteration: 1. GeneratorAgent produces (or improves) (~1294 tok)

## DotnetMultiAgents/src/Hr.GroupChatOrchestrator/

- `Hr.GroupChatOrchestrator.csproj` (~126 tok)

## DotnetMultiAgents/src/Hr.GroupChatOrchestrator/Chat/

- `HrGroupChat.cs` — Group Chat (Debate) pattern. Runs 3 specialists in parallel to critique a draft, then moderator synthesizes (~370 tok)

## DotnetMultiAgents/src/Hr.Infrastructure/

- `DependencyInjection.cs` — src/Hr.Infrastructure/DependencyInjection.cs (~218 tok)
- `Hr.Infrastructure.csproj` — References Hr.Core, Hr.Application; EF Core + SQL Server (~50 tok)
- `HrDbContext.cs` — src/Hr.Infrastructure/HrDbContext.cs (~314 tok)

## DotnetMultiAgents/src/Hr.Infrastructure/Repositories/

- `JobAnnouncementRepository.cs` — src/Hr.Infrastructure/Repositories/JobAnnouncementRepository.cs (~478 tok)
- `PositionRepository.cs` — src/Hr.Infrastructure/Repositories/PositionRepository.cs (~357 tok)

## DotnetMultiAgents/src/Hr.Jobs.Mcp/

- `Hr.Jobs.Mcp.csproj` — MCP server, port 5100; references Hr.Application + Hr.Infrastructure (~50 tok)
- `Program.cs` — src/Hr.Jobs.Mcp/Program.cs (~1956 tok)

## DotnetMultiAgents/src/Hr.Jobs.Mcp/Tools/

- `JobAnnouncementTools.cs` — src/Hr.Jobs.Mcp/Tools/JobAnnouncementTools.cs (~1335 tok)
- `JobDescriptionTools.cs` — Fetches all positions in the same occupational series, then selects at most 3 representatives (next- (~2315 tok)

## DotnetMultiAgents/src/Hr.PipeOrchestrator/Agents/

- `DraftAgent.cs` — Stage 1 of the HR pipeline. Calls WriteJobDescription then SaveJobAnnouncement. Parses the saved ann (~500 tok)

## DotnetMultiAgents/src/Hr.SelectorOrchestrator/

- `Hr.SelectorOrchestrator.csproj` — Multi-agent selector orchestrator; Part 1 of blog series (~50 tok)
- `Program.cs` — src/Hr.SelectorOrchestrator/Program.cs (~2431 tok)

## DotnetMultiAgents/tools/UsaJobsFetcher/

- `Program.cs` — tools/UsaJobsFetcher/Program.cs (~3417 tok)

## blogs/


## blogs/multi-agents/

- `part-1-dotnet-agent-framework.md` — IChatClient: The Universal Agent Primitive (~2607 tok)
- `part-10-evaluator-optimizer-pattern.md` — The Evaluator-Optimizer Pattern (~3393 tok)
- `part-2-clean-architecture-for-ai.md` — The Layer Diagram (~2907 tok)
- `part-3-hr-data-mcp-server.md` — Project Setup (~3021 tok)
- `part-4-compliance-mcp-deterministic-rules.md` — Why Deterministic Rules, Not LLM Judgment (~3273 tok)
- `part-5-persisting-ai-artifacts.md` — The Design Decision (~2867 tok)
- `part-6-selector-pattern.md` — The Three Components (~3251 tok)
- `part-7-claude-desktop-multi-agent.md` — How Claude Desktop Works with MCP (~2035 tok)
- `part-8-pipe-pattern.md` — The Pipe Pattern (~2767 tok)
- `part-9-group-chat-pattern.md` — The Group Chat Pattern (~3618 tok)
- `preface-why-one-agent-is-not-enough.md` — Series Overview (~2648 tok)

## blogs/series-2-multi-agents/

- `part-1-dotnet-agent-framework.md` — Part 1 — The .NET Agent Framework: IChatClient and MCP Clients (~2343 tok)
- `part-2-clean-architecture-for-ai.md` — Part 2 — Clean Architecture for AI Applications (~2664 tok)
- `part-3-hr-data-mcp-server.md` — Part 3 — Building the HR Data MCP Server (~2506 tok)
- `part-4-compliance-mcp-deterministic-rules.md` — Part 4 — The Compliance MCP Server: Deterministic Rules, Zero LLM (~2877 tok)
- `part-5-persisting-ai-artifacts.md` — Part 5 — Persisting AI Artifacts: The JobAnnouncement Lifecycle (~2431 tok)
- `part-6-selector-pattern.md` — Part 6 — The Selector Pattern: Routing to Specialists (~2766 tok)
- `part-7-claude-desktop-multi-agent.md` — Part 7 — Claude Desktop as Your Multi-Agent Platform (~1907 tok)
- `preface-why-one-agent-is-not-enough.md` — Why One Agent Is Not Enough (~1845 tok)

## docs/

- `blog-series-plan.md` — Blog Series Plan — Building Multi-Agent Systems with .NET 10 (~3602 tok)

## docs/superpowers/plans/

- `2026-05-12-series-2b-patterns.md` — Series 2B — Multi-Agent Patterns Implementation Plan (~10215 tok)
- `2026-05-13-blog-series-expansion.md` — Blog Series Expansion — Parts 8–10 + Retrofit Navigation Implementation Plan (~19839 tok)

## docs/superpowers/specs/

- `2026-05-12-series-2b-patterns-design.md` — Series 2B — Multi-Agent Patterns Extension: Design Spec (~2099 tok)
- `2026-05-13-blog-series-expansion-design.md` — Design: Blog Series Expansion — Parts 8–10 + Retrofit Navigation (~2962 tok)

## medium/

- `medium-public-url.json` (~2825 tok)

## tests/

- `seed.spec.ts` (~35 tok)
