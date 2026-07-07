# Promotional Blog Post Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Write a standalone 800–1,000-word Medium teaser post that drives developers who have hit the single-agent wall to read the 10-part multi-agent series starting at Part 1.

**Architecture:** Two files — a `.md` source and a `.medium.html` for pasting into Medium. The `.md` contains the canonical prose. The `.medium.html` is generated from it with no URL differences (all links in this post are absolute Medium/GitHub URLs, so no in-memory substitution is needed).

**Tech Stack:** Markdown, HTML (Medium paste format), no code compilation

## Global Constraints

- Title: "When Your .NET AI Agent Starts Failing, Build This Instead"
- Target length: 800–1,000 words
- One code snippet maximum (the 3-line orchestrator loop)
- No bullet list of all 10 parts in the CTA — one link to Part 1 only
- Series Part 1 URL: `https://medium.com/scrum-and-coke/part-1-the-net-agent-framework-ichatclient-and-mcp-clients-4b52cc179e26`
- GitHub repo URL: `https://github.com/workcontrolgit/DotnetMultiAgentsTutorial`
- Output `.md`: `blogs/multi-agents/promo-standalone-blog.md`
- Output `.medium.html`: `blogs/multi-agents/promo-standalone-blog.medium.html`

---

### Task 1: Write the Markdown Blog Post

**Files:**
- Create: `blogs/multi-agents/promo-standalone-blog.md`

**Interfaces:**
- Consumes: nothing (all prose content is in this plan)
- Produces: `blogs/multi-agents/promo-standalone-blog.md` — consumed by Task 2

- [ ] **Step 1: Create the file with Section 1 — The Failure Moment**

Create `blogs/multi-agents/promo-standalone-blog.md` with this exact content for the opening:

```markdown
# When Your .NET AI Agent Starts Failing, Build This Instead

*[Building Multi-Agent Systems with .NET 10](https://medium.com/scrum-and-coke/why-one-agent-is-not-enough-9872c10a2a70)*

---

You built a .NET AI agent. It connects to your MCP server, calls your tools, returns intelligent answers. The demo went well. Then someone asks:

> "Find an open IT Specialist position, write a job description for it, then check if it passes OPM compliance."

Here is what a single general-purpose agent does with that:

```
You: Find an open IT Specialist position, write a job description for it,
     then check if it passes OPM compliance.

Agent: [calls GetOpenPositions → finds position ID 3, GS-12]
Agent: [calls WriteJobDescription with positionId=3]
Agent: Draft complete.

  ## Qualifications Required
  One year of specialized experience in a related field...

Agent: [calls RunFullComplianceCheck with positionId=3]
Agent: QualificationsGradeReference: WARNING
       The qualifications text does not reference the advertised grade GS-12.
```

Three problems in one conversation. The qualifications text the agent just generated is the exact text that fails the compliance rule it then runs. It did not route the failure back to the writer to fix. The draft was never saved — close this session and it disappears.

This is not a bug in your code. This is the structural limit of a single general-purpose agent.
```

- [ ] **Step 2: Append Section 2 — Two Root Causes**

Append this to the file:

```markdown
---

## Two Things Are Working Against You

**Tool overload.** Your agent has twelve tools — position search, job description writer, compliance checker, org lookup. On every turn, the LLM scans all twelve, ranks them by relevance to the query, and picks the right one. When tools overlap — `GetPositionById` and `GetOpenPositions` both relate to positions — the model hedges. Research consistently shows LLM accuracy degrades above 8–10 tools. With twelve, you are starting from a position of handicap before the user types a single character.

**Prompt dilution.** Your system prompt says "you are a helpful federal HR assistant who can search positions, write job descriptions, check OPM compliance, and summarize organizations." That is four jobs in one prompt. A job description writer needs specific instructions: second-person voice, grade-level experience equivalency, duty bullets in active voice. A compliance checker needs different instructions: Pass/Fail per rule, OPM standard references, specific correction suggestions. You cannot fit both instruction sets in one prompt without watering both down.

The failure log above is not bad luck. It is the predictable output of asking one agent to be too many things at once.
```

- [ ] **Step 3: Append Section 3 — The Multi-Agent Answer**

Append this:

```markdown
---

## The Fix Is a Different Architecture

Give each concern its own agent: a focused system prompt, a small tool subset. A job description writer with five tools and a prompt written specifically for federal HR style. A compliance checker with seven tools and a prompt about OPM standards. Neither agent knows about the other's concerns. Neither carries the other's tool list.

Add a lightweight router — an LLM call with no tools at all — that reads each user message and outputs a single label: `job_description`, `compliance`, `position_search`. The router picks the specialist. The specialist handles the turn.

Four patterns cover every multi-agent scenario you will encounter in production:

- **Selector** — routes each query to one specialist; best for discrete, categorised requests
- **Pipe** — chains agents sequentially, each stage's output feeding the next; best for ordered workflows
- **Group Chat** — runs agents in parallel, a moderator synthesizes; best for multi-perspective review
- **Evaluator-Optimizer** — a critic scores output and loops until a quality threshold is met; best for generation tasks where consistency matters

All four run on a single `IChatClient` abstraction. No framework lock-in.
```

- [ ] **Step 4: Append Section 4 — The Same System Rebuilt**

Append this:

```markdown
---

## The Same Request, Rebuilt

Here is the identical three-part request handled by a multi-agent system:

```
You: Find an open IT Specialist position, write a job description for it,
     then check if it passes OPM compliance.

[Router → job_description]

JobDescription specialist:
  [calls GetPositionById → full data: GS-12, series 2210, DHS]
  [calls WriteJobDescription → draft with grade-specific qualifications]
  [calls SaveJobAnnouncement → ID: 5, Status: Draft]
  Draft saved. Announcement ID: 5.

[Router → compliance]

OPMCompliance specialist:
  [calls RunFullComplianceCheck with positionId=3]
  Overall: PASS
  QualificationsGradeReference: PASS — text references GS-12
  [calls UpdateAnnouncementStatus → ID: 5, Status: CompliancePassed]
```

The draft references the correct grade because the JobDescription specialist had access to the full position data and a prompt written for federal HR writing. The compliance check passes because the writer got it right. The draft persists because `SaveJobAnnouncement` is in the specialist's tool set and is always called.

The orchestrator loop that made this happen is three lines:

```csharp
var intent = await router.ClassifyAsync(input, ct);
var agent  = SelectAgent(intent);
var reply  = await agent.HandleAsync(input, ct);
```

Same two MCP servers. Same Ollama model running locally. Different architecture.
```

- [ ] **Step 5: Append Section 5 — CTA**

Append this:

```markdown
---

If any of that failure log looked familiar, this series is for you.

[Part 1 — The .NET Agent Framework: IChatClient and MCP Clients](https://medium.com/scrum-and-coke/part-1-the-net-agent-framework-ichatclient-and-mcp-clients-4b52cc179e26) covers the `IChatClient` abstraction and MCP tool setup that make running five specialist agents practical without duplicating infrastructure. It is the foundation the other nine parts build on.

The full source — both MCP servers, all four orchestrators — is on GitHub: [DotnetMultiAgentsTutorial](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial).
```

- [ ] **Step 6: Verify word count**

Open `blogs/multi-agents/promo-standalone-blog.md` and count words.
Expected: 800–1,000 words (excluding code blocks).

If under 800: expand Section 2 or Section 4 with one additional concrete sentence each.
If over 1,000: trim Section 3 (the four-pattern list descriptions can lose a few words each).

- [ ] **Step 7: Commit**

```bash
git add blogs/multi-agents/promo-standalone-blog.md
git commit -m "content: add standalone promo blog post for multi-agent series"
```

---

### Task 2: Convert to Medium HTML

**Files:**
- Read: `blogs/multi-agents/promo-standalone-blog.md`
- Read: one existing `.medium.html` in `blogs/multi-agents/` for format reference (e.g. `blogs/multi-agents/part-6-selector-pattern.medium.html`)
- Create: `blogs/multi-agents/promo-standalone-blog.medium.html`

**Interfaces:**
- Consumes: `blogs/multi-agents/promo-standalone-blog.md` from Task 1
- Produces: `blogs/multi-agents/promo-standalone-blog.medium.html` — ready to paste into Medium editor

**Two-representation principle:** This post uses only absolute URLs (Medium article links and GitHub links) — no relative GitHub file links. No in-memory URL substitution needed. The `.medium.html` content is identical to what the `.md` renders to.

- [ ] **Step 1: Read the existing Medium HTML format**

Read `blogs/multi-agents/part-6-selector-pattern.medium.html` to understand the exact HTML structure used for:
- `<h3>` for section headings
- `<p>` for paragraphs
- `<pre><code>` for code blocks
- `<ul><li>` for bullet lists
- `<a href="...">` for links
- `<em>` for italics (series attribution line)
- `<strong>` for bold (pattern names in bullets)

- [ ] **Step 2: Write the Medium HTML file**

Create `blogs/multi-agents/promo-standalone-blog.medium.html` converting the `.md` using these rules (verified from the reference file):

- Title `# ...` → `<h3>...</h3>`
- Section headings `## ...` → `<h3>...</h3>`
- Italic series link `*[text](url)*` → `<p><em><a href="url">text</a></em></p>`
- Paragraphs → `<p>...</p>`
- Blockquote `> ...` → `<p><em>...</em></p>` (Medium renders blockquotes as emphasis)
- Fenced code blocks → `<pre><code>...</code></pre>`
- Bullet lists → `<ul><li>...</li></ul>` with `<strong>Pattern Name</strong>` for bold names
- Inline code `` `...` `` → `<code>...</code>`
- Inline links `[text](url)` → `<a href="url">text</a>`
- Horizontal rules `---` → omit (Medium uses section spacing natively)

**Important for code blocks containing C# `${...}` or `\n`:** Not applicable here — the one C# snippet (`var intent = await router.ClassifyAsync...`) has no string interpolation or escape sequences. Write it verbatim.

**Important for the console log code blocks:** These contain `→` arrows and `[...]` annotations — write them as-is inside `<pre><code>`.

- [ ] **Step 3: Verify the HTML file**

Open `blogs/multi-agents/promo-standalone-blog.medium.html` and confirm:
- No `${}` template literal conflicts
- All `<a href>` URLs are absolute (start with `https://`)
- Series attribution line is present as `<p><em><a href="...">Building Multi-Agent Systems with .NET 10</a></em></p>`
- The CTA section ends the file (no trailing content)

- [ ] **Step 4: Commit**

```bash
git add blogs/multi-agents/promo-standalone-blog.medium.html
git commit -m "content: add Medium HTML for promo standalone blog post"
```
