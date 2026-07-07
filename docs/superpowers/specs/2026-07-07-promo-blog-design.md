# Promotional Blog Post Design

## Meta

- **Title:** When Your .NET AI Agent Starts Failing, Build This Instead
- **Publication:** Medium — Scrum and Coke
- **Type:** Standalone promotional article (teaser)
- **Target length:** 800–1,000 words
- **Goal:** Drive readers who have built a single .NET AI agent and hit its limits to click through to the 10-part multi-agent series starting at Part 1

## Target Reader

A .NET developer who has already built an AI agent. It works in demos. Under realistic conditions it picks the wrong tool, produces inconsistent output, or loses work between sessions. They know something is wrong but have not yet identified the architectural cause. They are actively looking for a solution.

## Tone and Style

- Direct, empathetic — validate the reader's experience before explaining anything
- Technical but not tutorial-level — one code snippet maximum
- No padding, no introductory pleasantries
- Ends with a single warm CTA, not a bullet list of links

## Structure

### Section 1 — The Failure Moment (~150 words)

Opens with a concrete conversation log showing a single agent failing on a realistic three-step request:

1. Find an open IT Specialist position
2. Write a job description for it
3. Check if it passes OPM compliance

Show three specific failures in the log:
- The qualifications text the agent generated is the exact text that fails the compliance rule it then runs
- The agent does not route the compliance failure back to the writer
- The draft is never saved — it disappears when the session ends

No explanation in this section. Just the scene. The reader recognizes it.

Source material: the conversation log in `blogs/multi-agents/preface-why-one-agent-is-not-enough.md` (the `Hr.Agent` baseline log).

### Section 2 — Two Root Causes, Named (~200 words)

Name and define the two structural problems:

**Tool overload:** An agent with 12 tools makes 12 choices per turn. LLM accuracy degrades measurably above 8–10 tools — Anthropic research supports this. With overlapping tools (GetPositionById vs GetOpenPositions), the model hedges, calls the wrong one, or calls both.

**Prompt dilution:** A system prompt doing four jobs — search specialist, writer, compliance checker, org analyst — does none of them well. Each role needs precise, role-specific instructions that crowd each other out in a single prompt.

Key message: this is not a bug in the reader's code. It is a structural limit of the single-agent architecture.

### Section 3 — The Multi-Agent Answer (~150 words)

The solution: give each concern its own agent.

- One specialist per concern — focused prompt, small tool subset (max 7 tools)
- One router to classify intent and delegate — no tool calls, low latency
- No agent is distracted by another agent's concerns

Four patterns for four failure modes — names and one-line descriptions only:
- **Selector** — routes each query to one specialist
- **Pipe** — chains agents sequentially, each transforming the previous output
- **Group Chat** — runs agents in parallel, moderator synthesizes
- **Evaluator-Optimizer** — critic scores output and loops until quality threshold is met

Do NOT explain any pattern in depth. The series does that.

### Section 4 — The Same System, Rebuilt (~200 words)

The identical HR scenario from Section 1, now with the multi-agent version:

- Router classifies the three-part request
- JobDescription specialist calls WriteJobDescription with the correct position data
- Compliance specialist runs RunFullComplianceCheck — finds the issue, reports it
- Draft is persisted by SaveJobAnnouncement — survives the session

Contrast is the payoff: same request, different architecture, clean result.

Include one code snippet — the router `ClassifyAsync` call — to make it concrete without becoming a tutorial. Use the snippet from `src/Hr.SelectorOrchestrator/Orchestration/AgentRouter.cs`.

End this section with: "The same two MCP servers. The same Ollama model running locally. Different architecture."

### Section 5 — CTA (~100 words)

Warm close, not a hard sell:

> "If any of that failure log looked familiar, this series is for you."

- Link to Part 1 with anchor text: "Part 1 — The .NET Agent Framework: IChatClient and MCP Clients"
- One sentence on what Part 1 covers: the IChatClient abstraction and MCP tool setup that makes running five specialist agents practical without duplicating infrastructure
- Link to the GitHub repo: `https://github.com/workcontrolgit/DotnetMultiAgentsTutorial`

No bullet list of all 10 parts. One door, one CTA.

## Output File

Save the finished post to:
`blogs/multi-agents/promo-standalone-blog.md`

Convert to Medium HTML at:
`blogs/multi-agents/promo-standalone-blog.medium.html`

## Source References

| Content | Source |
|---------|--------|
| Failure log (Section 1) | `blogs/multi-agents/preface-why-one-agent-is-not-enough.md` — "A Concrete Failure" section |
| Tool overload data (Section 2) | Same preface — "The Two Problems" section |
| Router code snippet (Section 4) | `DotnetMultiAgents/src/Hr.SelectorOrchestrator/Orchestration/AgentRouter.cs` |
| Pattern descriptions (Section 3) | `blogs/multi-agents/preface-why-one-agent-is-not-enough.md` — "Four Patterns" section |
| Series Part 1 URL | Published Medium URL from `medium/medium-public-url.json` |
