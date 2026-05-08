# Dotnet Multi-Agents Tutorial

A hands-on tutorial repository for building **Multi-Agent Systems** with .NET 10, [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai), the [Model Context Protocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk), and the [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) for multi-agent orchestration.

Each part of the series implements a different multi-agent pattern — all using the same federal HR domain (positions, hiring organizations, job descriptions) as the running example.

📖 **Blog Series:** [Building Multi-Agent Systems with .NET 10 on Medium](https://medium.com/)

---

## What You'll Learn

- How to design multi-agent systems where specialist agents outperform a single general-purpose agent
- Four production-ready orchestration patterns implemented in C#:
  - **Selector** — route each user query to the right specialist
  - **Pipe** — chain agents sequentially, each transforming the output of the last
  - **Group Chat** — run a panel of agents in parallel, then synthesize with a moderator
  - **Evaluator-Optimizer** — loop a critic agent against a drafter until quality is met
- How to connect agents to an MCP server via `ModelContextProtocol` client
- How to use `IChatClient` (Microsoft.Extensions.AI) to stay model-agnostic — swap Ollama for any LLM without touching agent logic
- How the [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) provides the orchestration primitives (`AgentRouter`, `SpecialistAgent`) that wire intent classification to tool-scoped specialist agents

---

## Architecture

```mermaid
flowchart TD
    User(["👤 User"])

    User -->|query| Orchestrator

    subgraph ORC["Hr.Orchestrator"]
        direction TB
        Orchestrator["HrOrchestrator"]
        Router["AgentRouter\n─────────────\nLLM intent classifier\nno tools · low latency"]
        Orchestrator --> Router
    end

    Router -->|position_search| PS
    Router -->|job_description| JD
    Router -->|org_summary|    OS
    Router -->|compliance|     CA
    Router -->|general|        GA

    subgraph AGENTS["Specialist Agents"]
        direction LR
        PS["PositionSearch"]
        JD["JobDescription"]
        OS["OrgSummary"]
        CA["OPMCompliance"]
        GA["General"]
    end

    PS -->|"GetOpenPositions\nGetPositionById\nGetPositionsByOrg\nGetHiringOrgs"| MCP1
    JD -->|"WriteJobDescription\nGetPositionById"| MCP1
    OS -->|"GetHiringOrgs\nGetPositionsByOrg"| MCP1
    CA -->|"GetPositionById"| MCP1
    CA -->|"RunFullComplianceCheck\nValidatePayGrade\nCheckApplicationPeriod\nGetOPMStandard\nListOPMSeries"| MCP2

    subgraph MCP1["Hr.Jobs.Mcp  :5100"]
        direction TB
        HrTools["HR Data Tools"]
        Ollama["🦙 Ollama llama3.2\n(WriteJobDescription)"]
        DB1[("SQL Server\nLocalDB · HrMcpDb")]
        HrTools --> Ollama
        HrTools --> DB1
    end

    subgraph MCP2["Hr.Compliance.Mcp  :5200"]
        direction TB
        RuleEngine["OpmRuleEngine\n─────────────\n7 deterministic rules\nzero LLM calls"]
        Repo["OpmStandardsRepository\n8 occupational series"]
        DB2[("SQL Server\nLocalDB · HrMcpDb")]
        RuleEngine --> Repo
        RuleEngine --> DB2
    end

    style ORC  fill:#1e3a5f,color:#fff,stroke:#4a90d9
    style AGENTS fill:#1a3a2a,color:#fff,stroke:#4caf50
    style MCP1 fill:#3a1a00,color:#fff,stroke:#ff9800
    style MCP2 fill:#3a001a,color:#fff,stroke:#e91e8c
```

---

## Project Structure

```
DotnetMultiAgentsTutorial/
├── DotnetMultiAgents/                       # .NET solution
│   ├── DotnetMultiAgents.slnx
│   └── src/
│       ├── Hr.Core/                         # Domain entities (Position, HiringOrganization)
│       ├── Hr.Application/                  # Application services
│       ├── Hr.Infrastructure/               # EF Core + SQL Server LocalDB
│       ├── Hr.Jobs.Mcp/                     # MCP server — HR data tools          :5100
│       ├── Hr.Compliance.Mcp/               # MCP server — OPM rule engine        :5200
│       │   ├── Rules/
│       │   │   ├── OpmRuleEngine.cs         # 7 deterministic compliance rules
│       │   │   ├── OpmStandardsRepository.cs# 8 OPM occupational series
│       │   │   └── ComplianceResult.cs      # Pass / Warning / Fail per rule
│       │   └── Tools/
│       │       └── ComplianceTools.cs       # MCP tool definitions
│       ├── Hr.Agent/                        # Single-agent baseline (for comparison)
│       └── Hr.Orchestrator/                 # ✅ Part 1 — Selector pattern
│           ├── Agents/
│           │   └── SpecialistAgent.cs       # Configurable specialist agent
│           └── Orchestration/
│               ├── AgentIntent.cs           # PositionSearch|JobDescription|OrgSummary|Compliance|General
│               ├── AgentRouter.cs           # LLM-based intent classifier
│               └── HrOrchestrator.cs        # Main selector loop
├── blogs/
│   └── series-2-multi-agents/               # Blog posts (in progress)
└── docs/
    └── blog-series-plan.md
```

---

## Blog Series

| Part | Pattern | Status | Code Project |
|------|---------|--------|-------------|
| Preface | Why Multi-Agent? | ⬜ | — |
| 1 | Selector — Router to Specialists | ⬜ | `Hr.Orchestrator` ✅ |
| 2 | Pipe — Sequential Agent Chain | ⬜ | `Hr.Pipeline` |
| 3 | Group Chat — Panel + Moderator | ⬜ | `Hr.GroupChat` |
| 4 | Shared Memory — Stateful Context | ⬜ | `Hr.SharedMemory` |
| 5 | Evaluator-Optimizer — Critic Loop | ⬜ | `Hr.EvalOptimizer` |

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [SQL Server LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) (ships with Visual Studio)
- [Ollama](https://ollama.com) with `llama3.2` pulled locally
- [Duende IdentityServer](https://duendesoftware.com/products/identityserver) container (for OIDC auth on the MCP server — optional for local dev)

---

## Quick Start

```bash
# Clone
git clone https://github.com/workcontrolgit/DotnetMultiAgentsTutorial.git
cd DotnetMultiAgentsTutorial/DotnetMultiAgents

# Build the solution
dotnet build DotnetMultiAgents.slnx

# Run EF Core migrations and seed HR data
dotnet ef database update \
  --project src/Hr.Infrastructure \
  --startup-project src/Hr.Jobs.Mcp

# Terminal 1 — HR data MCP server (port 5100)
dotnet run --project src/Hr.Jobs.Mcp

# Terminal 2 — OPM compliance MCP server (port 5200)
dotnet run --project src/Hr.Compliance.Mcp

# Terminal 3 — multi-agent orchestrator (Selector pattern)
dotnet run --project src/Hr.Orchestrator
```

> **Local dev without OIDC:** comment out the token acquisition block in `Hr.Orchestrator/Program.cs`
> and remove `.RequireAuthorization()` from `Hr.Jobs.Mcp/Program.cs` to skip auth locally.

---

## MCP Tools

### Hr.Jobs.Mcp — HR data tools (port 5100)

| Tool | Description |
|------|-------------|
| `GetOpenPositions` | All currently open federal job positions |
| `GetPositionById` | Full detail for a specific position |
| `GetPositionsByOrganization` | Positions filtered by hiring organization |
| `GetHiringOrganizations` | All hiring organizations with position counts |
| `WriteJobDescription` | AI-generated job description via Ollama |

```bash
npx @modelcontextprotocol/inspector http://localhost:5100/mcp
```

### Hr.Compliance.Mcp — OPM rule engine (port 5200)

| Tool | Description |
|------|-------------|
| `RunFullComplianceCheck` | Runs all 7 OPM rules against a position; returns Pass / Warning / Fail per rule |
| `ValidatePayGrade` | Checks grade format, min ≤ max, and alignment with OPM series standard |
| `CheckApplicationPeriod` | Enforces the 5-business-day minimum announcement window |
| `GetOPMStandard` | Returns allowed grade range and qualification standard URL for a series |
| `ListOPMSeries` | All occupational series known to the compliance server |

```bash
npx @modelcontextprotocol/inspector http://localhost:5200/compliance
```

> **Zero LLM calls in the rule engine.** All compliance decisions are made in deterministic C# code. The `OPMCompliance` specialist agent uses the LLM only to explain results to the user in plain language.

### Sample Rule: Occupational Series Check

The occupational series drives two rules that work together.

**Rule 1 — `CheckPayGradeAlignment`** (series → allowed grade range)

`OpmRuleEngine` calls `OpmStandardsRepository.GetBySeries(position.OccupationalSeries)`.
The repository normalises the code (strips/pads leading zeros) so `"201"` and `"0201"` resolve identically.

```
Position.OccupationalSeries
        │
        ▼
OpmStandardsRepository.GetBySeries()
        │
   ┌────┴─────────────────┐
   │ Not found             │ Found → AllowedGradeNumbers
   ▼                       ▼
Warning                 PayGradeMin + PayGradeMax
"unknown series"        each must be in the allowed list
                              │
                        ┌─────┴─────┐
                        │ Outside    │ Inside
                        ▼            ▼
                       Fail          Pass
                  (+ OPM standard URL)
```

Example — an IT Specialist posted at GS-16 fails because series `2210` only allows GS-05 through GS-15:

```
Rule:    PayGradeAlignment  →  FAIL
Message: Grade GS-16 is outside the allowed range for series 2210
         (Information Technology Management). Allowed: GS-05 to GS-15.
         Standard: https://www.opm.gov/...
```

**Rule 2 — `CheckQualificationsText`** (implicit series enforcement)

This rule does not call the repository, but enforces a consequence of OPM series standards: the qualifications text must explicitly mention the advertised grade level (e.g. `"GS-12"`), because OPM qualification standards are written grade-by-grade.

```
Qualifications text contains "GS-12" (or the advertised grade)?
    Yes → Pass
    No  → Warning: "Qualifications text does not reference the advertised grade"
```

**What is NOT checked** — the `RequiredQualificationKeyword` stored on each `OpmStandard`
(e.g. `"information technology"` for series 2210) is available via the `GetOPMStandard` tool
but no rule currently validates it. This is a natural extension point for a stricter Rule 8.

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Multi-agent orchestration | [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) |
| Agent abstraction | `Microsoft.Extensions.AI` 10.* (`IChatClient`) |
| Local LLM | `OllamaSharp` 5.* (`OllamaApiClient`) |
| MCP server SDK | [`ModelContextProtocol` 1.*](https://github.com/modelcontextprotocol/csharp-sdk) |
| MCP client SDK | [`ModelContextProtocol.Core`](https://github.com/modelcontextprotocol/csharp-sdk) (via `ModelContextProtocol`) |
| Auth | Duende IdentityServer — client credentials flow |
| Persistence | EF Core 9 + SQL Server LocalDB |
| Target framework | .NET 10 |

---

## Related Repositories

- [AngularNetTutorial](https://github.com/workcontrolgit/AngularNetTutorial) — Full-stack Angular 20 / .NET 10 / Duende IdentityServer

---

## License

MIT
