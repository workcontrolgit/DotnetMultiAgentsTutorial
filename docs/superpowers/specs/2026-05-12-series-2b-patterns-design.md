# Series 2B — Multi-Agent Patterns Extension: Design Spec

**Date:** 2026-05-12  
**Status:** Approved  
**Scope:** Parts 8–10 of the DotnetMultiAgentsTutorial blog series  
**Repo:** DotnetMultiAgentsTutorial  

---

## Context

The current blog series (Parts 1–7) covers Clean Architecture, two MCP servers, and the Selector pattern. This spec extends the series with three additional orchestration patterns, each implemented as an isolated console app project sharing the existing MCP servers and domain layers.

---

## Decisions Made

| Question | Decision |
|---|---|
| Project structure | Separate project per pattern (Approach 1) |
| Pipe mode | Semi-automated — pause after each stage for user confirmation |
| Group Chat model | Debate model — 3 reviewers in parallel, Moderator synthesizes |
| Evaluator-Optimizer evaluation | Quality rubric scored by LLM EvaluatorAgent (not compliance rules) |

---

## Solution Structure

Three new projects added to `DotnetMultiAgents.slnx`:

```
DotnetMultiAgents/src/
├── Hr.Orchestrator/              ← existing (Selector, Part 6)
├── Hr.PipeOrchestrator/          ← NEW (Part 8)
│   ├── Hr.PipeOrchestrator.csproj
│   ├── Program.cs
│   ├── Agents/
│   │   ├── DraftAgent.cs
│   │   └── ComplianceAgent.cs
│   └── Pipeline/
│       └── HrPipeline.cs
├── Hr.GroupChatOrchestrator/     ← NEW (Part 9)
│   ├── Hr.GroupChatOrchestrator.csproj
│   ├── Program.cs
│   ├── Agents/
│   │   ├── HrSpecialistAgent.cs
│   │   ├── LegalReviewerAgent.cs
│   │   ├── BudgetAnalystAgent.cs
│   │   └── ModeratorAgent.cs
│   └── Chat/
│       └── HrGroupChat.cs
└── Hr.EvaluatorOrchestrator/     ← NEW (Part 10)
    ├── Hr.EvaluatorOrchestrator.csproj
    ├── Program.cs
    ├── Agents/
    │   ├── GeneratorAgent.cs
    │   └── EvaluatorAgent.cs
    └── Loop/
        └── EvaluatorOptimizerLoop.cs
```

**Shared infrastructure (no changes needed):**
- `Hr.Jobs.Mcp` — port 5100
- `Hr.Compliance.Mcp` — port 5200
- `Hr.Core`, `Hr.Application`, `Hr.Infrastructure` — domain and persistence layers

Each new project uses `IChatClient` via `OllamaSharp` and connects to MCP servers via `McpClient.CreateAsync(new HttpClientTransport(...))` — identical stack to `Hr.Orchestrator`.

---

## Part 8 — Pipe Pattern (`Hr.PipeOrchestrator`)

### Teaching Angle
"The pipe pattern is not about routing — it's about *guaranteeing sequence*. Each stage's output is the next stage's input, and no stage can be skipped."

### Pipeline Flow

```
User provides PositionId
       ↓
Stage 1 — DraftAgent
  Tools: WriteJobDescription, GetPositionById, SaveJobAnnouncement
  → Generates draft, saves to DB, returns JobAnnouncementId
  → [PAUSE] Displays draft to user, awaits confirmation
       ↓
Stage 2 — ComplianceAgent
  Tools: RunFullComplianceCheck, GetPositionById
  → Runs all 7 OPM rules against the saved announcement
  → [PAUSE] Displays compliance report, awaits confirmation
       ↓
Stage 3 — StatusAgent (deterministic, no LLM)
  If all rules passed → UpdateAnnouncementStatus(Published)
  If any failed      → UpdateAnnouncementStatus(ComplianceFailed)
  → Displays final status
```

### Key Components

- **`HrPipeline.cs`** — orchestrates three stages sequentially; after each stage prints result and prompts `"Continue to next stage? (y/n)"`
- **`DraftAgent`** — standalone class with focused system prompt and 3 MCP tools; no dependency on `Hr.Orchestrator`
- **`ComplianceAgent`** — standalone class with 2 MCP tools; no dependency on `Hr.Orchestrator`
- **Stage 3** — direct MCP tool call from `HrPipeline.cs`, no LLM (deterministic status write)

### Semi-Automated UX

After each stage the console prints the stage output and prompts the user to continue. This makes intermediate state visible — the key learning moment of the Pipe pattern.

---

## Part 9 — Group Chat Pattern (`Hr.GroupChatOrchestrator`)

### Teaching Angle
"Group Chat shines when you need multiple expert perspectives on the same artifact without any one perspective dominating. The Moderator's job is synthesis, not decision-making."

### Chat Flow

```
User provides JobAnnouncementId (existing draft)
       ↓
HrGroupChat loads draft text via GetJobAnnouncement
       ↓
Round 1 — Parallel debate (Task.WhenAll)
  ├── HrSpecialistAgent   → HR policy perspective critique
  ├── LegalReviewerAgent  → Legal/EEO language critique
  └── BudgetAnalystAgent  → Pay grade/budget perspective critique
  → [PAUSE] Displays all three critiques to user
       ↓
Round 2 — Synthesis
  ModeratorAgent
  Input: original draft + all three critiques (single combined prompt)
  → Produces revised final draft
  → [PAUSE] Displays revised draft to user
       ↓
User confirms → SaveJobAnnouncement (overwrites draft)
```

### Key Components

- **`HrGroupChat.cs`** — runs Round 1 with `Task.WhenAll` (parallel, no anchoring bias), invokes Moderator with combined context, then calls `SaveJobAnnouncement` directly after user confirmation
- **Reviewer agents** — no MCP tools; reason over draft text passed in prompt only
- **ModeratorAgent** — no MCP tools; produces revised draft text only; `HrGroupChat.cs` owns the DB write

### Design Note
Reviewers run in parallel intentionally — no reviewer sees another's feedback before forming their own critique. This prevents the first critic from anchoring the others.

---

## Part 10 — Evaluator-Optimizer Pattern (`Hr.EvaluatorOrchestrator`)

### Teaching Angle
"The Evaluator-Optimizer is not a retry loop — it's a *learning* loop. Each iteration the Generator gets smarter because the Evaluator tells it exactly what failed and why."

### Loop Flow

```
User provides PositionId
       ↓
EvaluatorOptimizerLoop begins (max 3 iterations)
       ↓
┌──────────────────────────────────────────────────────┐
│ Iteration N                                          │
│                                                      │
│  GeneratorAgent                                      │
│  Tools: WriteJobDescription, GetPositionById         │
│  Prompt includes evaluator feedback if iter > 1      │
│        ↓                                             │
│  EvaluatorAgent (no tools, reasoning only)           │
│  Rubric:                                             │
│    Clarity         25 pts                            │
│    OPM Language    25 pts                            │
│    Completeness    25 pts                            │
│    Tone            25 pts                            │
│  Returns: EvaluationResult { Score, Feedback }       │
│        ↓                                             │
│  [PAUSE] Displays draft, score, per-criterion notes  │
│        ↓                                             │
│  Score ≥ 80? ──YES──→ EXIT LOOP                      │
│      │                                               │
│     NO                                               │
│      └──→ Next iteration (feedback injected)         │
└──────────────────────────────────────────────────────┘
       ↓
SaveJobAnnouncement (highest-scoring draft)
UpdateAnnouncementStatus(Draft)
```

### Key Components

- **`EvaluatorOptimizerLoop.cs`** — manages iteration state, tracks best draft, injects feedback into next Generator prompt as: `"Previous attempt scored {score}/100. Weaknesses: {feedback}. Improve these areas."`
- **`GeneratorAgent`** — 2 MCP tools; system prompt updated each iteration with prior feedback
- **`EvaluatorAgent`** — no MCP tools; returns `EvaluationResult` as JSON (parsed from LLM response via JSON mode)
- **`EvaluationResult`** — `record` with `int Score` and `Dictionary<string, string> Feedback`

### Loop Exit Conditions
- Score ≥ 80 → exit with current draft
- 3 iterations reached → exit with highest-scoring draft across all iterations

---

## Blog Post Mapping

| Part | Title | New Project | Pattern |
|---|---|---|---|
| Part 8 | The Pipe Pattern: Guaranteed Sequence | `Hr.PipeOrchestrator` | Pipe |
| Part 9 | The Group Chat Pattern: Multiple Expert Perspectives | `Hr.GroupChatOrchestrator` | Group Chat (Debate) |
| Part 10 | The Evaluator-Optimizer: Learning Loops | `Hr.EvaluatorOrchestrator` | Evaluator-Optimizer |

---

## Non-Goals

- No new domain entities or DB schema changes
- No changes to existing MCP servers
- No new API endpoints
- No OIDC/security changes (out of scope for tutorial patterns)
- No shared `SpecialistAgent` base class extraction (YAGNI — each project is self-contained)
