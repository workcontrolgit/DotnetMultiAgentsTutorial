# 5 MCP Tools Every .NET Developer Should Know (From Someone Who's Actually Used Them)

I've been building MCP servers in .NET for a while now — agents that query databases, read files, call APIs, and actually *do things* inside a running system. Along the way, I've accumulated a short list of tools I keep coming back to.

MCP is only as good as the ecosystem around it. The spec is the foundation. But the tools — the servers you connect to, the SDKs you build with, the clients that make it click — that's what actually determines whether your agent does anything useful.

Before I get into the list: I run everything locally against **Ollama** during development. If you haven't set that up yet, it's what everything below runs on — worth doing first. Once you have that running, here's what's actually in my MCP stack.

---

## 1. ModelContextProtocol NuGet Package

**The official .NET MCP server SDK — where everything starts.**

This is the Microsoft-maintained NuGet package for building spec-compliant MCP servers in .NET. Attribute-based tool registration, stdio and SSE transports out of the box, and minimal boilerplate. Your tools are just C# methods decorated with `[McpServerTool]`.

I used this to expose an HR query service as an MCP server. The LLM called it like a function. That moment changed how I think about AI integrations entirely.

**Install:** `dotnet add package ModelContextProtocol`

---

## 2. Claude Desktop

**The fastest way to see your MCP server come alive.**

Claude Desktop is an MCP host. Point it at your server via `claude_desktop_config.json` and it wires up your tools automatically — no agent code, no client boilerplate. It's the fastest feedback loop I've found for MCP development.

I had a Claude Desktop session calling my locally-running .NET MCP server in under 10 minutes. It felt like cheating. If you're building an MCP server and haven't tested it here yet, you're missing out.

**Get it:** [claude.ai/download](https://claude.ai/download)

---

## 3. GitHub MCP Server

**Give your agent eyes on your codebase.**

This is an official MCP server from GitHub that exposes repos, issues, PRs, code search, and file contents as MCP tools. Your AI agent can search issues, read source code, and summarize PRs — without a single API call baked into your app code. The MCP host handles authentication.

I asked an agent to find all open issues mentioning "performance" in a repo. It returned structured results. No SDK integration, no OAuth dance in my code. Just a tool call.

**Get it:** [github.com/github/github-mcp-server](https://github.com/github/github-mcp-server)

---

## 4. Playwright MCP Server

**Give your agent a real browser.**

Playwright MCP wraps the Playwright browser automation library as an MCP server. Navigation, screenshots, clicking, form filling, DOM inspection — all exposed as MCP tools. Your .NET agent doesn't need to know a thing about browsers.

I used it to have an agent screenshot a competitor's pricing page and summarize the changes. Two MCP tool calls. No Selenium setup, no browser driver config, no headache.

**Get it:** [github.com/microsoft/playwright-mcp](https://github.com/microsoft/playwright-mcp)

---

## 5. MCP Inspector

**See exactly what your MCP server is doing — before your agent does.**

MCP Inspector is a browser-based visual debugger for MCP servers. Connect it to any running server and it lists all available tools, lets you invoke them manually, and shows the full JSON-RPC traffic in real time. It's the first thing I open after `dotnet run`.

I caught a malformed tool description that would have confused the LLM — spotted it immediately in the Inspector's tool list. Saved me an hour of prompt debugging that I didn't even know I was about to do.

**Get it:** `npx @modelcontextprotocol/inspector`

---

These five tools aren't the whole MCP ecosystem — it's growing fast. But they're the ones that made MCP stop feeling like a spec to me and start feeling like a platform. If you're a .NET developer and you haven't wired any of these up yet, now's a good time to start.

If you want to see them in action together, I've been writing a hands-on series building a real AI Agent with MCP in .NET from scratch — starting with Clean Architecture and working up through Claude Desktop integration and OIDC security. [Check out Part 1 here.](https://medium.com/scrum-and-coke/part-1-clean-architecture-foundation-with-hr-domain-f43127400757)

---

*Tags: .NET, AI, MCP, Model Context Protocol, Software Development*
