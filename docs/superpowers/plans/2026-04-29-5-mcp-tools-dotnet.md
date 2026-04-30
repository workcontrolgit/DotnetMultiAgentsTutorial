# 5 MCP Tools Every .NET Developer Should Know — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Write and save a standalone Medium-ready blog post titled "5 MCP Tools Every .NET Developer Should Know (From Someone Who's Actually Used Them)".

**Architecture:** Single markdown file written section-by-section per the approved spec. No code snippets in the post (punchy/inspirational tone). Medium formatting rules apply: no markdown tables, no relative image paths.

**Tech Stack:** Markdown, Medium.com publishing conventions, spec at `docs/superpowers/specs/2026-04-29-5-mcp-tools-dotnet-design.md`

---

### Task 1: Create post file with title and intro

**Files:**
- Create: `blogs/standalone/5-mcp-tools-dotnet.md`

- [ ] **Step 1: Create the file with title and intro**

Write the following to `blogs/standalone/5-mcp-tools-dotnet.md`:

```markdown
# 5 MCP Tools Every .NET Developer Should Know (From Someone Who's Actually Used Them)

I've been building MCP servers in .NET for a while now — agents that query databases, read files, call APIs, and actually *do things* inside a running system. Along the way, I've accumulated a short list of tools I keep coming back to.

MCP is only as good as the ecosystem around it. The spec is the foundation. But the tools — the servers you connect to, the SDKs you build with, the clients that make it click — that's where it gets interesting.

Before I get into the list: I run everything locally against **Ollama** during development. If you're not already using it, stop here and set it up first — it's the LLM backbone behind everything below. Once you have that running, here's what's actually in my MCP stack.
```

- [ ] **Step 2: Verify the file was created**

Confirm `blogs/standalone/5-mcp-tools-dotnet.md` exists and opens cleanly.

- [ ] **Step 3: Commit**

```bash
git add blogs/standalone/5-mcp-tools-dotnet.md
git commit -m "feat: start 5-mcp-tools post — title and intro"
```

---

### Task 2: Write Tool 1 — ModelContextProtocol NuGet

**Files:**
- Modify: `blogs/standalone/5-mcp-tools-dotnet.md`

- [ ] **Step 1: Append Tool 1 section**

Append to `blogs/standalone/5-mcp-tools-dotnet.md`:

```markdown

---

## 1. ModelContextProtocol NuGet Package

**The official .NET MCP server SDK — where everything starts.**

This is the Microsoft-maintained NuGet package for building spec-compliant MCP servers in .NET. Attribute-based tool registration, stdio and SSE transports out of the box, and minimal boilerplate. Your tools are just C# methods decorated with `[McpServerTool]`.

I used this to expose an HR query service as an MCP server. The LLM called it like a function. That moment changed how I think about AI integrations entirely.

**Install:** `dotnet add package ModelContextProtocol`
```

- [ ] **Step 2: Commit**

```bash
git add blogs/standalone/5-mcp-tools-dotnet.md
git commit -m "feat: add tool 1 — ModelContextProtocol NuGet"
```

---

### Task 3: Write Tool 2 — Claude Desktop

**Files:**
- Modify: `blogs/standalone/5-mcp-tools-dotnet.md`

- [ ] **Step 1: Append Tool 2 section**

Append to `blogs/standalone/5-mcp-tools-dotnet.md`:

```markdown

---

## 2. Claude Desktop

**The fastest way to see your MCP server come alive.**

Claude Desktop is an MCP host. Point it at your server via `claude_desktop_config.json` and it wires up your tools automatically — no agent code, no client boilerplate. It's the fastest feedback loop I've found for MCP development.

I had a Claude Desktop session calling my locally-running .NET MCP server in under 10 minutes. It felt like cheating. If you're building an MCP server and haven't tested it here yet, you're missing out.

**Get it:** [claude.ai/download](https://claude.ai/download)
```

- [ ] **Step 2: Commit**

```bash
git add blogs/standalone/5-mcp-tools-dotnet.md
git commit -m "feat: add tool 2 — Claude Desktop"
```

---

### Task 4: Write Tool 3 — GitHub MCP Server

**Files:**
- Modify: `blogs/standalone/5-mcp-tools-dotnet.md`

- [ ] **Step 1: Append Tool 3 section**

Append to `blogs/standalone/5-mcp-tools-dotnet.md`:

```markdown

---

## 3. GitHub MCP Server

**Give your agent eyes on your codebase.**

This is an official MCP server from GitHub that exposes repos, issues, PRs, code search, and file contents as MCP tools. Your AI agent can search issues, read source code, and summarize PRs — without a single API call baked into your app code. The MCP host handles authentication.

I asked an agent to find all open issues mentioning "performance" in a repo. It returned structured results. No SDK integration, no OAuth dance in my code. Just a tool call.

**Get it:** [github.com/github/github-mcp-server](https://github.com/github/github-mcp-server)
```

- [ ] **Step 2: Commit**

```bash
git add blogs/standalone/5-mcp-tools-dotnet.md
git commit -m "feat: add tool 3 — GitHub MCP Server"
```

---

### Task 5: Write Tool 4 — Playwright MCP Server

**Files:**
- Modify: `blogs/standalone/5-mcp-tools-dotnet.md`

- [ ] **Step 1: Append Tool 4 section**

Append to `blogs/standalone/5-mcp-tools-dotnet.md`:

```markdown

---

## 4. Playwright MCP Server

**Give your agent a real browser.**

Playwright MCP wraps the Playwright browser automation library as an MCP server. Navigation, screenshots, clicking, form filling, DOM inspection — all exposed as MCP tools. Your .NET agent doesn't need to know a thing about browsers.

I used it to have an agent screenshot a competitor's pricing page and summarize the changes. Two MCP tool calls. No Selenium setup, no browser driver config, no headache.

**Get it:** [github.com/microsoft/playwright-mcp](https://github.com/microsoft/playwright-mcp)
```

- [ ] **Step 2: Commit**

```bash
git add blogs/standalone/5-mcp-tools-dotnet.md
git commit -m "feat: add tool 4 — Playwright MCP Server"
```

---

### Task 6: Write Tool 5 — MCP Inspector

**Files:**
- Modify: `blogs/standalone/5-mcp-tools-dotnet.md`

- [ ] **Step 1: Append Tool 5 section**

Append to `blogs/standalone/5-mcp-tools-dotnet.md`:

```markdown

---

## 5. MCP Inspector

**See exactly what your MCP server is doing — before your agent does.**

MCP Inspector is a browser-based visual debugger for MCP servers. Connect it to any running server and it lists all available tools, lets you invoke them manually, and shows the full JSON-RPC traffic in real time. It's the first thing I open after `dotnet run`.

I caught a malformed tool description that would have confused the LLM — spotted it immediately in the Inspector's tool list. Saved me an hour of prompt debugging that I didn't even know I was about to do.

**Get it:** `npx @modelcontextprotocol/inspector`
```

- [ ] **Step 2: Commit**

```bash
git add blogs/standalone/5-mcp-tools-dotnet.md
git commit -m "feat: add tool 5 — MCP Inspector"
```

---

### Task 7: Write outro and CTA

**Files:**
- Modify: `blogs/standalone/5-mcp-tools-dotnet.md`

- [ ] **Step 1: Append outro and CTA**

Append to `blogs/standalone/5-mcp-tools-dotnet.md`:

```markdown

---

These five tools aren't the whole MCP ecosystem — it's growing fast. But they're the ones that made MCP stop feeling like a spec to me and start feeling like a platform. If you're a .NET developer and you haven't wired any of these up yet, now's a good time to start.

If you want to see them in action together, I've been writing a hands-on series building a real AI Agent with MCP in .NET from scratch — starting with Clean Architecture and working up through Claude Desktop integration and OIDC security. [Check out Part 1 here.]

---

*Tags: .NET, AI, MCP, Model Context Protocol, Software Development*
```

- [ ] **Step 2: Commit**

```bash
git add blogs/standalone/5-mcp-tools-dotnet.md
git commit -m "feat: add outro and CTA to 5-mcp-tools post"
```

---

### Task 8: Medium compliance review

**Files:**
- Modify: `blogs/standalone/5-mcp-tools-dotnet.md` (if issues found)

- [ ] **Step 1: Check for markdown tables**

Scan the post for any `|` table syntax. Per cerebrum.md, Medium does not render markdown tables. There should be none — but verify.

- [ ] **Step 2: Check for relative image paths**

Scan for any `![...](./)` or `![...](../` style image references. None are expected in this post, but verify.

- [ ] **Step 3: Check all links are absolute URLs**

Verify every `[text](url)` link uses a full `https://` URL, not a relative path. Fix any that don't.

- [ ] **Step 4: Read the full post aloud mentally**

Read through the entire post checking:
- Each tool section has: tagline, what it does, why I use it, real-world beat, install/get-it line
- Tone is consistent: first-person, punchy, not tutorial-depth
- No section feels rushed or padded compared to the others
- Ollama callout lands naturally in the intro

- [ ] **Step 5: Commit any fixes**

```bash
git add blogs/standalone/5-mcp-tools-dotnet.md
git commit -m "fix: medium compliance review — 5-mcp-tools post"
```

---
