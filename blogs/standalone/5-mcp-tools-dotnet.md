

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
