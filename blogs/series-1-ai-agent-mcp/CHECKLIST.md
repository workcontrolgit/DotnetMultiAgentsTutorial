# Series 1 Publishing & Code Checklist

Complete all items in a part's checklist before starting the next part.

---

## Series Status

| Part | Title | Blog | Code | Published |
|------|-------|------|------|-----------|
| 1 | Clean Architecture Foundation with HR Domain | ⬜ | ⬜ | ⬜ |
| 2 | Introduction to Model Context Protocol | ⬜ | ⬜ | ⬜ |
| 3 | Building an MCP Server in .NET 10 | ⬜ | ⬜ | ⬜ |
| 4 | AI Agent with Microsoft.Extensions.AI + Ollama | ⬜ | ⬜ | ⬜ |
| 5 | Claude Desktop Integration & End-to-End Demo | ⬜ | ⬜ | ⬜ |

---

## Part 1 — Clean Architecture Foundation with HR Domain

**File:** `blogs/series-1-ai-agent-mcp/part-1-clean-architecture-hr-domain.md`  
**Code:** `src/HrMcp.Core`, `src/HrMcp.Application`, `src/HrMcp.Infrastructure.Persistence`, `src/HrMcp.McpServer`

### Blog Content
- [x] Intro states what the reader will have by the end
- [x] All prerequisites listed with version check commands
- [x] Every step is numbered and sequential
- [x] All code blocks have a language tag (`csharp`, `bash`, `json`, `xml`)
- [x] File paths in code snippets match the actual solution structure
- [x] USAJobs API field mapping table is accurate
- [x] Step 8 (USAJobs seed) marked clearly as optional
- [x] "Next Up" footer links to Part 2
- [x] Sources section is complete

### Code
- [x] `dotnet build DotnetAiAgentMcp.slnx` → 0 errors, 0 warnings
- [x] Every `dotnet add package` command includes a version constraint (`--version 9.*`)
- [x] EF Core migration `InitialCreate` runs without error on a clean DB
- [x] `DbSeeder` seeds 4 organizations and 5 positions on first run
- [x] `DbSeeder` skips seeding on subsequent runs (idempotent)
- [x] `DbSeeder` falls back to hand-crafted data when `data/usajobs-seed.json` is absent
- [x] `tools/UsaJobsFetcher` builds and runs independently (not in `.sln`)
- [x] Code in blog matches code in repo exactly — no drift

### Publish Gate
- [ ] All blog content and code items above are checked
- [ ] `data/usajobs-seed.json` generated and committed (if Step 8 is included)
- [ ] GitHub commit tagged: `part-1`
- [ ] Blog post published to target platform
- [ ] GitHub repo link in blog post verified and live

**Update Series Status table ↑ when this gate is cleared.**

---

## Part 2 — Introduction to Model Context Protocol

**File:** `blogs/series-1-ai-agent-mcp/part-2-intro-to-mcp.md`  
**Code:** None (concepts-only post)

### Blog Content
- [ ] N×M integration problem explained with diagram
- [ ] MCP primitives defined: Tools, Resources, Prompts
- [ ] Architecture diagram present (Host → Client → Server)
- [ ] stdio vs HTTP/SSE transports explained
- [ ] MCP vs other patterns comparison table present
- [ ] .NET NuGet package (`ModelContextProtocol`) mentioned
- [ ] Preview of Part 3 tools listed: `GetOpenPositions`, `GetHiringOrganizations`, `GetPositionsByOrganization`, `WriteJobDescription`
- [ ] "Next Up" footer links to Part 3
- [ ] No code blocks (this is a concepts post — verify none crept in)

### Code
- [ ] N/A — no code deliverable for this part

### Publish Gate
- [ ] All blog content items above are checked
- [ ] GitHub commit tagged: `part-2`
- [ ] Blog post published to target platform
- [ ] GitHub repo link in blog post verified and live

**Update Series Status table ↑ when this gate is cleared.**

---

## Part 3 — Building an MCP Server in .NET 10

**File:** `blogs/series-1-ai-agent-mcp/part-3-mcp-server-dotnet.md`  
**Code:** `src/HrMcp.McpServer/Tools/`

### Blog Content
- [ ] `ModelContextProtocol` and `ModelContextProtocol.AspNetCore` package install commands included
- [ ] All 3 tool classes shown: `PositionTools`, `HiringOrganizationTools`, `JobDescriptionTools`
- [ ] `WriteJobDescription` clearly marked as a stub (LLM added in Part 4)
- [ ] Both transports explained: stdio and HTTP/SSE
- [ ] `Program.cs` updated version shown (with `--stdio` flag detection)
- [ ] MCP Inspector test walkthrough included with expected output for all 4 tools
- [ ] "Next Up" footer links to Part 4
- [ ] Sources section complete

### Code
- [ ] `dotnet build DotnetAiAgentMcp.sln` → 0 errors
- [ ] Server starts on `http://localhost:5100`
- [ ] `npx @modelcontextprotocol/inspector http://localhost:5100/mcp` shows 4 tools
- [ ] `GetHiringOrganizations` returns 4 organizations
- [ ] `GetOpenPositions` returns open positions with remuneration
- [ ] `GetPositionById` returns full position detail
- [ ] `WriteJobDescription` returns stub output (not LLM-generated yet)
- [ ] `--stdio` flag silences all logging (stdout clean for JSON-RPC)
- [ ] Code in blog matches code in repo exactly — no drift

### Publish Gate
- [ ] All blog content and code items above are checked
- [ ] GitHub commit tagged: `part-3`
- [ ] Blog post published to target platform
- [ ] GitHub repo link in blog post verified and live

**Update Series Status table ↑ when this gate is cleared.**

---

## Part 4 — AI Agent with Microsoft.Extensions.AI + Ollama

**File:** `blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md`  
**Code:** `src/HrMcp.Agent/`, `src/HrMcp.McpServer/Tools/JobDescriptionTools.cs` (upgraded)

### Blog Content
- [ ] Ollama setup commands included (`ollama pull llama3.2`, sanity check curl)
- [ ] All 4 NuGet packages for `HrMcp.Agent` listed with version constraints
- [ ] `HrAgent.cs` shown in full with system prompt
- [ ] `Program.cs` for `HrMcp.Agent` shown in full
- [ ] `WriteJobDescription` upgrade shown: before (stub) and after (LLM)
- [ ] `IChatClient` registration in `McpServer/Program.cs` shown
- [ ] Sample conversation transcript included (at least 2 turns)
- [ ] `Microsoft.Extensions.AI` used as abstraction — Ollama is a detail, not the focus
- [ ] "Next Up" footer links to Part 5
- [ ] Sources section complete

### Code
- [ ] `dotnet build DotnetAiAgentMcp.sln` → 0 errors
- [ ] `ollama run llama3.2` available locally before testing
- [ ] Agent connects to MCP server and lists tools on startup
- [ ] Agent answers "What positions are open?" using `GetOpenPositions`
- [ ] Agent answers org-scoped questions using `GetHiringOrganizations` + `GetPositionsByOrganization`
- [ ] `WriteJobDescription` returns real LLM-generated content (not stub)
- [ ] Code in blog matches code in repo exactly — no drift

### Publish Gate
- [ ] All blog content and code items above are checked
- [ ] GitHub commit tagged: `part-4`
- [ ] Blog post published to target platform
- [ ] GitHub repo link in blog post verified and live

**Update Series Status table ↑ when this gate is cleared.**

---

## Part 5 — Claude Desktop Integration & End-to-End Demo

**File:** `blogs/series-1-ai-agent-mcp/part-5-claude-desktop-integration.md`  
**Code:** `publish/McpServer/` (release build), `.vscode/mcp.json`

### Blog Content
- [ ] How stdio transport works with Claude Desktop explained (diagram present)
- [ ] `dotnet publish` command for self-contained win-x64 executable included
- [ ] `claude_desktop_config.json` location shown for both Windows and macOS
- [ ] Full `claude_desktop_config.json` snippet shown
- [ ] "Restart Claude Desktop" step and hammer icon confirmation mentioned
- [ ] Live demo walkthrough table present (prompt → tools called → result)
- [ ] VS Code Copilot section present with `.vscode/mcp.json` snippet
- [ ] Debugging table present (5 common problems with causes and fixes)
- [ ] stdout contamination pitfall explicitly called out
- [ ] Series recap "What was built" summary present
- [ ] "Next Steps" section lists future topics
- [ ] No "Next Up" footer (this is the final part)
- [ ] Sources section complete

### Code
- [ ] `dotnet publish` produces a runnable `HrMcp.McpServer.exe`
- [ ] Server starts correctly with `--stdio` flag (no log output on stdout)
- [ ] Claude Desktop loads tools (hammer icon visible after restart)
- [ ] At least 3 demo prompts verified end-to-end in Claude Desktop
- [ ] `.vscode/mcp.json` works in VS Code Copilot Chat (`@hr-mcp`)
- [ ] MCP log commands verified on target OS
- [ ] Code in blog matches code in repo exactly — no drift

### Publish Gate
- [ ] All blog content and code items above are checked
- [ ] GitHub commit tagged: `part-5`
- [ ] Blog post published to target platform
- [ ] GitHub repo link in blog post verified and live
- [ ] Series Status table at top of this file fully complete

**Update Series Status table ↑ when this gate is cleared.**

---

## How to Use This Checklist

1. Work through the items for the **current part** top to bottom
2. Check each item only when it is **fully done** — not "good enough"
3. The **Publish Gate** is the release condition: do not start the next part until the gate is cleared
4. Update the **Series Status** table at the top when a gate clears — one glance shows where the series stands
