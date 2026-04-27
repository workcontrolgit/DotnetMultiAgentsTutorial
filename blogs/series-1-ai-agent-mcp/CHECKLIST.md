# Series 1 Publishing & Code Checklist

Complete all items in a part's checklist before starting the next part.

---

## Series Status

| Part | Title | Blog | Code | Published |
|------|-------|------|------|-----------|
| 1 | Clean Architecture Foundation with HR Domain | ⬜ | ✅ | ⬜ |
| 2 | Introduction to Model Context Protocol | ⬜ | ⬜ | ⬜ |
| 3 | Building an MCP Server in .NET 10 | ⬜ | ⬜ | ⬜ |
| 4 | AI Agent with Microsoft.Extensions.AI + Ollama | ⬜ | ⬜ | ⬜ |
| 5 | Claude Desktop Integration & End-to-End Demo | ⬜ | ⬜ | ⬜ |
| 6 | Securing the MCP Server with OIDC | ⬜ | ⬜ | ⬜ |

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
- [x] GitHub commit tagged: `part-1`
- [ ] Blog post published to target platform
- [ ] GitHub repo link in blog post verified and live

**Update Series Status table ↑ when this gate is cleared.**

---

## Part 2 — Introduction to Model Context Protocol

**File:** `blogs/series-1-ai-agent-mcp/part-2-intro-to-mcp.md`  
**Code:** None (concepts-only post)

### Blog Content
- [x] N×M integration problem explained with diagram
- [x] MCP primitives defined: Tools, Resources, Prompts
- [x] Architecture diagram present (Host → Client → Server)
- [x] stdio vs HTTP/SSE transports explained
- [x] MCP vs other patterns comparison present (as prose sections — no table, Medium-compatible)
- [x] .NET NuGet package (`ModelContextProtocol`) mentioned
- [x] Preview of Part 3 tools listed: `GetOpenPositions`, `GetHiringOrganizations`, `GetPositionsByOrganization`, `WriteJobDescription`
- [x] "Next Up" footer links to Part 3
- [x] No code blocks (verified — 0 code fences)

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
- [x] `ModelContextProtocol` and `ModelContextProtocol.AspNetCore` package install commands included
- [x] All 3 tool classes shown: `PositionTools`, `HiringOrganizationTools`, `JobDescriptionTools`
- [x] `WriteJobDescription` clearly marked as a stub (LLM added in Part 4)
- [x] Both transports explained: stdio and HTTP/SSE
- [x] `Program.cs` updated version shown (with `--stdio` flag detection)
- [x] **MCP Inspector section** — full named section "Testing with MCP Inspector — Your MCP Postman + Swagger"; includes: Swagger UI + Postman analogy, Node.js 22.7.5+ prerequisite, `npx @modelcontextprotocol/inspector http://localhost:5100/mcp` run command, walkthrough calling all 4 tools with expected output
- [x] "Next Up" footer links to Part 4
- [x] Sources section complete (include MCP Inspector GitHub link)

### Code
- [x] `dotnet build DotnetAiAgentMcp.sln` → 0 errors
- [ ] Server starts on `http://localhost:5100`
- [ ] `npx @modelcontextprotocol/inspector http://localhost:5100/mcp` shows 4 tools
- [ ] `GetHiringOrganizations` returns 4 organizations
- [ ] `GetOpenPositions` returns open positions with remuneration
- [ ] `GetPositionById` returns full position detail
- [ ] `WriteJobDescription` returns stub output (not LLM-generated yet)
- [ ] `--stdio` flag silences all logging (stdout clean for JSON-RPC)
- [x] Code in blog matches code in repo exactly — no drift

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
- [x] Ollama setup commands included (`ollama pull llama3.2`, sanity check curl)
- [x] All 4 NuGet packages for `HrMcp.Agent` listed with version constraints
- [x] `HrAgent.cs` shown in full with system prompt
- [x] `Program.cs` for `HrMcp.Agent` shown in full
- [x] `WriteJobDescription` upgrade shown: before (stub) and after (LLM)
- [x] `IChatClient` registration in `McpServer/Program.cs` shown
- [x] Sample conversation transcript included (at least 2 turns)
- [x] `Microsoft.Extensions.AI` used as abstraction — Ollama is a detail, not the focus
- [x] "Next Up" footer links to Part 5
- [x] Sources section complete

### Code
- [x] `dotnet build DotnetAiAgentMcp.slnx` → 0 errors
- [ ] `ollama run llama3.2` available locally before testing
- [ ] Agent connects to MCP server and lists tools on startup
- [ ] Agent answers "What positions are open?" using `GetOpenPositions`
- [ ] Agent answers org-scoped questions using `GetHiringOrganizations` + `GetPositionsByOrganization`
- [ ] `WriteJobDescription` returns real LLM-generated content (not stub)
- [x] Code in blog matches code in repo exactly — no drift

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
- [x] How stdio transport works with Claude Desktop explained (diagram present)
- [x] `dotnet publish` command for self-contained win-x64 executable included
- [x] `claude_desktop_config.json` location shown for both Windows and macOS
- [x] Full `claude_desktop_config.json` snippet shown
- [x] "Restart Claude Desktop" step and hammer icon confirmation mentioned
- [x] Live demo walkthrough present as prose sections (prompt → tools called → result; no markdown table — Medium-compatible)
- [x] VS Code Copilot section present with `.vscode/mcp.json` snippet
- [x] Debugging section present (5 common problems with causes and fixes; bullet list — no markdown table)
- [x] stdout contamination pitfall explicitly called out
- [x] **Inspector as first debugging step** — "Tools not showing in Claude Desktop? Test with MCP Inspector in HTTP mode first to isolate server vs transport issues"
- [x] Series recap "What was built" summary present
- [x] "Next Steps" section lists future topics
- [x] No "Next Up" footer (this is the final part)
- [x] **Tease Part 6** — "Next Steps" section mentions adding OIDC security as the next evolution
- [x] Sources section complete

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

## Part 6 — Securing the MCP Server with OIDC

**File:** `blogs/series-1-ai-agent-mcp/part-6-mcp-security-oidc.md`  
**Code:** `src/HrMcp.McpServer/Program.cs` (auth middleware), `src/HrMcp.Agent/Program.cs` (token acquisition)

### Blog Content
- [x] Problem framing: why unauthenticated MCP servers are a risk in real deployments
- [x] Architecture diagram: OIDC provider → Agent (client credentials) → McpServer (resource server)
- [x] Provider options compared (Okta, Duende IdentityServer, Azure AD/Entra, Google Cloud Identity Platform) — as bullet list, not table
- [x] JWT Bearer middleware added to `HrMcp.McpServer/Program.cs` shown
- [x] `app.MapMcp("/mcp").RequireAuthorization()` change shown
- [x] `appsettings.json` snippet for `Authority` and `Audience` shown
- [x] Agent token acquisition shown: client credentials flow against chosen provider
- [x] `HttpClientTransportOptions.AdditionalHeaders` snippet shown (Authorization: Bearer) — note: `SseClientTransportOptions` renamed to `HttpClientTransportOptions` in ModelContextProtocol 1.x
- [x] Optional: tool-level role check via `IHttpContextAccessor` shown
- [x] Okta free-tier setup walkthrough (or IdentityServer dev license note)
- [x] "What We Built" summary present
- [x] No "Next Up" footer (this is the final part)
- [x] Sources section complete

### Code
- [x] `dotnet build DotnetAiAgentMcp.slnx` → 0 errors after auth middleware added
- [x] Unauthenticated request to `/mcp` returns `401 Unauthorized`
- [x] Agent successfully acquires token and calls MCP server end-to-end
- [x] `GetOpenPositions` and at least one other tool verified with auth in place
- [ ] Role-based tool guard verified (if included)
- [x] `appsettings.Development.json` has placeholder values only — no real secrets committed
- [x] Code in blog matches code in repo exactly — no drift

### Publish Gate
- [ ] All blog content and code items above are checked
- [ ] GitHub commit tagged: `part-6`
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
