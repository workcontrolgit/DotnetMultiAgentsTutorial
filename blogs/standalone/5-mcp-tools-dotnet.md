

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
