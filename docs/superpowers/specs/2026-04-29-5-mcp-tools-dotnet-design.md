# Spec: "5 MCP Tools Every .NET Developer Should Know"

**Date:** 2026-04-29
**Type:** Standalone Medium blog post (not part of Series 1)
**Author voice:** First-person, personal stack recommendations
**Target read time:** ~5 minutes

---

## Target Reader

.NET developers who already know MCP exists and want concrete tools to try. Not beginners needing MCP explained — readers who've heard the buzz and want to know what's actually worth installing.

## Goal

Inspire. Not tutorial depth. Each tool should make the reader think "I want to try that." The post closes with MCP feeling like a platform, not a spec.

---

## Structure

### Title

"6 MCP Tools Every .NET Developer Should Know (From Someone Who's Actually Used Them)"

### Intro (3 short paragraphs)

- Para 1: Set the scene. You've been building MCP servers in .NET. Agents that actually do things. Here's what you reach for.
- Para 2: MCP is only as good as the tools around it. The spec is the foundation — the ecosystem is where it gets interesting.
- Para 3: Quick Ollama callout — "I run everything locally against Ollama during development. If you're not already using it, stop here and set it up first — it's the LLM backbone behind everything below." Then transition: "Now, here's what's actually in my MCP stack."

### Tool 1 — `ModelContextProtocol` NuGet Package

**Type:** SDK (build)
**Tagline:** The official .NET MCP server SDK — where everything starts.

- What it does: Lets you build a fully spec-compliant MCP server in .NET with minimal boilerplate. Attribute-based tool registration, stdio + SSE transports out of the box.
- Why I use it: It's the official Microsoft SDK. No wrapper debt, no translation layer. Your tools are just C# methods decorated with `[McpServerTool]`.
- Real-world beat: I used this to expose an HR query service as an MCP server. The LLM called it like a function. That moment changed how I think about AI integrations.

### Tool 2 — Claude Desktop

**Type:** MCP Host (test & demo)
**Tagline:** The fastest way to see your MCP server come alive.

- What it does: A desktop app that acts as an MCP host. Point it at your server via `claude_desktop_config.json` and it wires up your tools automatically.
- Why I use it: Zero boilerplate client code. You can test your MCP server without writing a single line of agent logic. It's the best feedback loop I've found.
- Real-world beat: I had a Claude Desktop session calling my locally-running .NET MCP server in under 10 minutes. It felt like cheating.

### Tool 3 — GitHub MCP Server

**Type:** Community MCP Server (connect)
**Tagline:** Give your agent eyes on your codebase.

- What it does: An official MCP server from GitHub that exposes repos, issues, PRs, code search, and file contents as MCP tools.
- Why I use it: Your AI agent can search issues, read source code, and summarize PRs — without a single API key in your app code. The MCP host handles auth.
- Real-world beat: I asked an agent to find all open issues mentioning "performance" in a repo. It returned structured results. No SDK integration, no OAuth dance in my code.

### Tool 4 — Playwright MCP Server

**Type:** Community MCP Server (connect)
**Tagline:** Give your agent a real browser.

- What it does: Wraps Playwright as an MCP server — tools for navigation, screenshots, clicking, form filling, and DOM inspection.
- Why I use it: Web scraping, UI testing automation, and research agents all become dramatically simpler. Your .NET agent doesn't need to know anything about browsers.
- Real-world beat: I used it to have an agent screenshot a competitor's pricing page and summarize changes. Two MCP tool calls. No Selenium, no browser driver config.

### Tool 5 — MCP Inspector

**Type:** Dev tool (debug)
**Tagline:** See exactly what your MCP server is doing — before your agent does.

- What it does: A browser-based visual debugger for MCP servers. Connect it to any running server and it lists all available tools, lets you invoke them manually, and shows the full JSON-RPC traffic.
- Why I use it: Debugging blind is painful. With MCP Inspector I can verify tool schemas, test inputs, and confirm responses before writing a single line of agent code. It's the first thing I open after `dotnet run`.
- Real-world beat: I caught a malformed tool description that would have confused the LLM — spotted it immediately in the Inspector's tool list. Saved me an hour of prompt debugging.

### Tool 6 — Docker MCP Toolkit

**Type:** Platform (run)
**Tagline:** Run any MCP server as a container — no runtime dependencies required.

- What it does: Docker's MCP Toolkit lets you run MCP servers as Docker containers. Browse a catalog of pre-built servers, pull one down, and it runs isolated — no Node.js, Python, or other runtimes to install on your machine.
- Why I use it: As a .NET developer I don't want to install Python just to run a Python-based MCP server. Docker handles it. The server runs, the tools are available, my machine stays clean.
- Real-world beat: I needed a community MCP server built in Python. With Docker MCP Toolkit I had it running in two commands. No virtual environments, no version conflicts, no friction.

### Outro (2–3 sentences)

These six tools aren't the whole MCP ecosystem — it's growing fast. But they're the ones that made MCP stop feeling like a spec to me and start feeling like a platform. If you're a .NET developer and you haven't wired these up yet, now's a good time to start.

### CTA (optional, 1 line)

"If you want to see these tools in action together, I've been writing a hands-on series: [link to Series 1 Part 1]."

---

## Content Rules (per cerebrum.md)

- No markdown tables in the published post — convert to bullet lists or prose
- No relative image paths — use hosted URLs if images are included
- This post contains no code blocks (by design — punchy, not tutorial)

---

## Publishing Notes

- This is a **standalone post**, not part of Series 1
- Can cross-link to Series 1 in the CTA for funnel effect
- Tag suggestions: .NET, AI, MCP, Software Development, Programming
