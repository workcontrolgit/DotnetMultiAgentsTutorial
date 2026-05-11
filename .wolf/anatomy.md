# anatomy.md

> Auto-maintained by OpenWolf. Last scanned: 2026-05-08T11:29:17.164Z
> Files: 37 tracked | Anatomy hits: 0 | Misses: 0

## ../../Users/Fuji Nguyen/.claude/projects/c--apps-DotnetMultiAgentsTutorial/memory/

- `feedback_commit_messages.md` (~98 tok)
- `MEMORY.md` — Memory Index (~33 tok)

## ./

- `README.md` — Project documentation (~3003 tok)

## .wolf/


## DotnetMultiAgents/

- `DotnetMultiAgents.slnx` — Solution file referencing all 7 Hr.* projects (~50 tok)

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

## DotnetMultiAgents/src/Hr.Orchestrator/

- `Hr.Orchestrator.csproj` — Multi-agent selector orchestrator; Part 1 of blog series (~50 tok)
- `Program.cs` — src/Hr.Orchestrator/Program.cs (~2431 tok)

## DotnetMultiAgents/tools/UsaJobsFetcher/

- `Program.cs` — tools/UsaJobsFetcher/Program.cs (~3417 tok)

## blogs/


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

- `blog-series-plan.md` — Blog Series Plan — Building Multi-Agent Systems with .NET 10 (~2536 tok)
