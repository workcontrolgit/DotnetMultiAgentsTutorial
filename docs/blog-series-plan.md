# Blog Series Plan — Building Multi-Agent Systems with .NET 10

**Publication target:** Medium.com
**Repository:** [DotnetMultiAgentsTutorial](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial)
**Last updated:** 2026-05-08

---

## Target Audience

.NET developers who have:
- Built at least one AI agent using `Microsoft.Extensions.AI` or similar
- Understand MCP — can build or connect to an MCP server
- Want to scale from single-agent to production-grade multi-agent systems

What they do **not** yet know:
- Why a single agent breaks down at scale
- How to route between specialists
- How to design clean architecture for AI systems
- How to persist AI-generated artifacts with a lifecycle
- How Claude Desktop can drive the whole system without orchestrator code

---

## Series Structure

| Post | Title | Pattern | Code project | Status |
|------|-------|---------|--------------|--------|
| Preface | Why One Agent Is Not Enough | — | — | pending |
| Part 1 | The .NET Agent Framework | IChatClient + MCP | Hr.SelectorOrchestrator | pending |
| Part 2 | Clean Architecture for AI Apps | Layered design | All projects | pending |
| Part 3 | Building the HR Data MCP Server | MCP server + tools | Hr.Jobs.Mcp | pending |
| Part 4 | Compliance MCP: Deterministic Rules, Zero LLM | Rule engine | Hr.Compliance.Mcp | pending |
| Part 5 | Persisting AI Artifacts | JobAnnouncement lifecycle | Hr.Infrastructure | pending |
| Part 6 | The Selector Pattern | Multi-agent routing | Hr.SelectorOrchestrator | pending |
| Part 7 | Claude Desktop as Multi-Agent Platform | Claude Desktop + MCP | Hr.Jobs.Mcp + Hr.Compliance.Mcp | pending |

---

## Post Outlines

### Preface — Why One Agent Is Not Enough

**Hook:** You built an agent. It can search positions, write job descriptions, and check compliance.
But when you ask it to do all three in one conversation, quality collapses. Why?

**Sections:**
- The tool overload problem: 12 tools in context — LLM picks wrong tools
- The prompt dilution problem: general prompt = general output
- Show the failure: single-agent log where it invokes the wrong tool
- The 4 multi-agent patterns this series covers: Selector, Pipe, Group Chat, Evaluator-Optimizer
- Architecture preview (Mermaid diagram)
- Prerequisites: .NET 10, Ollama, SQL Server LocalDB, USAJobs API key

---

### Part 1 — The .NET Agent Framework: IChatClient and MCP Clients

**Audience bridge:** "You have used IChatClient. Here is what is underneath and why it is the right abstraction for multi-agent systems."

**Sections:**
- `IChatClient` as the universal primitive — swap Ollama for Claude or GPT in one line
- `OllamaSharp` 5.x: why `Microsoft.Extensions.AI.Ollama` is deprecated
- `AsBuilder().UseFunctionInvocation().Build()` pipeline — what each middleware does
- `McpClient.CreateAsync(new HttpClientTransport(...))` — connecting to MCP, casting tools as `AITool`
- Key insight: an `AITool` from MCP is identical to a local tool — the agent cannot tell the difference
- Side-by-side: `HrAgent` (single agent, all tools) vs. `SpecialistAgent` (focused prompt, tool subset)

**Code references:**
- `src/Hr.Agent/HrAgent.cs`
- `src/Hr.SelectorOrchestrator/Agents/SpecialistAgent.cs`
- `src/Hr.SelectorOrchestrator/Program.cs` (BuildClient helper)

---

### Part 2 — Clean Architecture for AI Applications

**Audience bridge:** "You know clean architecture from CRUD APIs. The same principles apply — with one twist: the LLM is an infrastructure concern."

**Sections:**
- Layer diagram: Hr.Core to Hr.Application to Hr.Infrastructure to MCP servers to Orchestrator
- Rule: domain and application layers must never import `Microsoft.Extensions.AI`
- Domain entities: Position, HiringOrganization, JobAnnouncement
- EF Core + SQL Server LocalDB setup — shared HrMcpDb
- The seed pipeline: USAJobs API to usajobs-seed.json to DbSeeder to SQL
- MCP as a service boundary: two servers, two ports, independent deployment
- `JobAnnouncement` lifecycle as a clean architecture case study

**Code references:**
- `src/Hr.Core/` (entities, interfaces, enums)
- `src/Hr.Infrastructure/HrDbContext.cs`
- `src/Hr.Infrastructure/DependencyInjection.cs`
- `tools/UsaJobsFetcher/Program.cs`

---

### Part 3 — Building the HR Data MCP Server

**Sections:**
- Project setup: `ModelContextProtocol` 1.x, `[McpServerToolType]`, `[McpServerTool]`, DI registration
- Walking through: GetOpenPositions, GetPositionById, GetPositionsByOrganization, GetHiringOrganizations
- `WriteJobDescription`: an LLM-powered tool inside the MCP server
- Grade-ladder context: how sibling positions improve draft quality — max 3, richness-ranked
- `SaveJobAnnouncement`, `GetJobAnnouncement`, `ListJobAnnouncements`, `UpdateAnnouncementStatus`
- OIDC feature flag pattern: `Features:EnableOidc` for local dev vs. production
- Testing with MCP Inspector — screenshot walkthrough

**Code references:**
- `src/Hr.Jobs.Mcp/Tools/JobDescriptionTools.cs`
- `src/Hr.Jobs.Mcp/Tools/JobAnnouncementTools.cs`
- `src/Hr.Jobs.Mcp/Program.cs`

---

### Part 4 — The Compliance MCP Server: Deterministic Rules, Zero LLM

**Sections:**
- Business case: OPM compliance rules are pass/fail by law — LLM hallucination is a liability
- Architecture decision: why this server has zero IChatClient dependency
- The 7 rules as pure C# (`OpmRuleEngine.cs`)
- `OpmStandardsRepository`: 8 series, static reference data
- `ComplianceResult` / `ComplianceReport`: value object pipeline
- Sample rule deep-dive: occupational series check (series lookup to grade range to fail + OPM URL)
- Testing with MCP Inspector — `RunFullComplianceCheck(1)` walkthrough
- Rule of thumb: if a lawyer could audit it, make it deterministic

**Code references:**
- `src/Hr.Compliance.Mcp/Rules/OpmRuleEngine.cs`
- `src/Hr.Compliance.Mcp/Rules/OpmStandardsRepository.cs`
- `src/Hr.Compliance.Mcp/Tools/ComplianceTools.cs`

---

### Part 5 — Persisting AI Artifacts: The JobAnnouncement Lifecycle

**Sections:**
- The problem: `WriteJobDescription` returns a string — no history, no status, no audit trail
- Design options: field on Position vs. separate entity vs. cross-server records
- `JobAnnouncement` entity: `AnnouncementStatus` enum (Draft to CompliancePassed to ComplianceFailed to Published)
- EF Core migration walkthrough: AddJobAnnouncement migration, FK to Positions, cascade delete
- Repository + service + MCP tools — the clean architecture chain in action
- End-to-end demo: generate, save, compliance check, UpdateAnnouncementStatus, retrieve
- Design principle: the agent writes the artifact; the database owns the truth

**Code references:**
- `src/Hr.Core/Entities/JobAnnouncement.cs`
- `src/Hr.Core/Enums/AnnouncementStatus.cs`
- `src/Hr.Infrastructure/Repositories/JobAnnouncementRepository.cs`
- `src/Hr.Application/Services/JobAnnouncementService.cs`
- `src/Hr.Jobs.Mcp/Tools/JobAnnouncementTools.cs`

---

### Part 6 — The Selector Pattern: Routing to Specialists

**Sections:**
- The Selector pattern defined: one router, N specialists, one handles each turn
- `AgentRouter`: LLM text classifier, no tools, low latency
- `SpecialistAgent`: reusable wrapper — name + system prompt + IChatClient + tool subset
- Tool subset design: why each agent gets only its tools
- `HrOrchestrator`: the selector loop — classify, pick, delegate, stream reply
- Console output demo: `[Router to JobDescription]` labeling each turn
- Cost analysis: router uses small model; specialists use capable model

**Tool assignment:**
- PositionSearch: GetOpenPositions, GetPositionById, GetPositionsByOrganization, GetHiringOrganizations
- JobDescription: WriteJobDescription, GetPositionById, SaveJobAnnouncement, GetJobAnnouncement, ListJobAnnouncements
- OrgSummary: GetHiringOrganizations, GetPositionsByOrganization
- OPMCompliance: RunFullComplianceCheck, ValidatePayGrade, CheckApplicationPeriod, GetOPMStandard, ListOPMSeries, GetPositionById, UpdateAnnouncementStatus
- General: no tools

**Code references:**
- `src/Hr.SelectorOrchestrator/Orchestration/AgentRouter.cs`
- `src/Hr.SelectorOrchestrator/Orchestration/AgentIntent.cs`
- `src/Hr.SelectorOrchestrator/Orchestration/HrOrchestrator.cs`
- `src/Hr.SelectorOrchestrator/Agents/SpecialistAgent.cs`
- `src/Hr.SelectorOrchestrator/Program.cs`

---

### Part 7 — Claude Desktop as Your Multi-Agent Platform

**Sections:**
- Recap: what the coded orchestrator does vs. what Claude Desktop can do natively
- Comparison: coded orchestrator vs. Claude Desktop (model, routing style, cost, best use)
- `claude_desktop_config.json` — stdio transport config for both MCP servers
- Demo: same 5 queries answered through Claude Desktop, zero orchestrator code
- When to use each: Claude Desktop for prototyping; coded orchestrator for production
- Teaser: next series — Pipe, Group Chat, Evaluator-Optimizer patterns

**claude_desktop_config.json:**
```json
{
  "mcpServers": {
    "hr-jobs": {
      "command": "dotnet",
      "args": ["run", "--project", "src/Hr.Jobs.Mcp", "--", "--stdio"],
      "cwd": "C:/apps/DotnetMultiAgentsTutorial/DotnetMultiAgents"
    },
    "hr-compliance": {
      "command": "dotnet",
      "args": ["run", "--project", "src/Hr.Compliance.Mcp", "--", "--stdio"],
      "cwd": "C:/apps/DotnetMultiAgentsTutorial/DotnetMultiAgents"
    }
  }
}
```

---

## Writing Guidelines for Medium.com

- No markdown tables — use bullet lists or prose sections instead
- Use numbered steps for tutorials
- Code blocks with language tags (csharp, bash, json)
- No relative image paths — use absolute hosted URLs or embed screenshots
- Keep each post under 2,500 words for Medium readability
- End each post with: "Next: Part N — Title" and a link to the repo

---

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
```
