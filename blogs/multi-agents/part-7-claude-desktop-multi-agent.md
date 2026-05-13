# Part 7 — Claude Desktop as Your Multi-Agent Platform

*Part 7 of: Building Multi-Agent Systems with .NET 10*

---

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · **Part 7** · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 6 — The Selector Pattern](part-6-selector-pattern.md) &nbsp;|&nbsp; [Part 8 — The Pipe Pattern →](part-8-pipe-pattern.md)

*Medium: [← Part 6](MEDIUM_URL_PART_6) | [Part 8 →](MEDIUM_URL_PART_8)*

---

You have built a complete multi-agent system: two MCP servers, five specialist agents, an intent router, and an orchestrator loop. It works with Ollama running locally.

Now close `Hr.SelectorOrchestrator`. You will not need it for this post.

This post shows how to connect the same two MCP servers — unchanged — to Claude Desktop, and let Claude act as the orchestrator. The demo produces the same results as Part 6 without a single line of orchestrator code.

---

## How Claude Desktop Works with MCP

Claude Desktop supports MCP natively. You configure servers in `claude_desktop_config.json`, and when Claude starts, it connects to each server using the stdio transport, lists all available tools, and makes them available in every conversation.

From Claude's perspective, it has one flat list of all tools from all connected servers. When you ask a question, Claude decides which tool to call — just like `HrOrchestrator` does, except the routing is Claude's own reasoning rather than a coded intent classifier.

The two MCP servers already support stdio transport. The `--stdio` flag was built in Part 3 specifically for this purpose. There is nothing to change in the server code.

---

## Configuring Claude Desktop

Find the config file:

- Windows: `%APPDATA%\Claude\claude_desktop_config.json`
- macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`

Add both MCP servers:

```json
{
  "mcpServers": {
    "hr-jobs": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "src/Hr.Jobs.Mcp",
        "--",
        "--stdio"
      ],
      "cwd": "C:/apps/DotnetMultiAgentsTutorial/DotnetMultiAgents"
    },
    "hr-compliance": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "src/Hr.Compliance.Mcp",
        "--",
        "--stdio"
      ],
      "cwd": "C:/apps/DotnetMultiAgentsTutorial/DotnetMultiAgents"
    }
  }
}
```

Restart Claude Desktop. In the bottom-left of the input area you will see a tool count badge — click it to verify all 14 tools appear: 9 from `hr-jobs` and 5 from `hr-compliance`.

---

## The Same Five Queries, No Orchestrator

Open a new conversation in Claude Desktop and run the same queries from Part 6:

**Query 1: Position search**

> What IT Specialist positions are currently open?

Claude calls `GetOpenPositions`, filters the results, and presents a readable summary. No routing code involved — Claude recognises this as a search query and picks the right tool.

**Query 2: Job description with save**

> Write a job description for position 7 and save it.

Claude calls `WriteJobDescription(7)`, receives the draft, then calls `SaveJobAnnouncement(7, draftText)`. You get back the announcement ID. Claude assembled this two-step tool chain from your single natural language request.

**Query 3: Compliance check with status update**

> Check if position 7 passes OPM compliance. The announcement ID is 4.

Claude calls `RunFullComplianceCheck(7)` on the compliance server, reads the 7-rule report, then calls `UpdateAnnouncementStatus(4, "CompliancePassed", "All 7 rules passed...")` on the HR server. Two servers, two tool calls, one conversation turn.

**Query 4: Org summary**

> How many organizations are hiring for IT roles?

Claude calls `GetHiringOrganizations` and `GetPositionsByOrganization` to assemble the answer.

**Query 5: General**

> Thanks, that's all for now.

No tool calls. Claude responds directly.

The results are equivalent to Part 6. The only difference is who is routing: a `switch` expression in `HrOrchestrator.cs`, or Claude's reasoning.

---

## Coded Orchestrator vs. Claude Desktop

Both approaches work. The choice depends on your requirements.

**Use Claude Desktop when:**
- You are prototyping or demonstrating the system
- You want to explore what the tools can do without writing orchestrator code
- The quality of Claude's routing and reasoning outweighs the cost
- You are the only user — Claude Desktop is single-user by design

**Use the coded orchestrator when:**
- You need predictable, auditable routing — a log that says exactly which agent handled each query
- You need cost control — routing classification to a cheap local model, specialist work to a capable model
- You need a multi-user system — the orchestrator is a service, Claude Desktop is a desktop app
- You need to customize agent behavior — the specialist system prompts in `Hr.SelectorOrchestrator` are tuned for federal HR writing standards in ways you cannot replicate in Claude Desktop
- You are running in an air-gapped environment where cloud APIs are not available

The coded orchestrator and Claude Desktop are not competing solutions. They serve different stages of the same development lifecycle. Build and test with Claude Desktop. Deploy with the orchestrator.

---

## What Claude Desktop Does Differently

One difference worth noting: Claude Desktop does not implement the Selector pattern. It is a single agent with access to all 14 tools — closer to `Hr.Agent` than to `Hr.SelectorOrchestrator`.

This means:
- Claude may call tools from both servers in a single turn if it judges them relevant — the compliance server and HR server are not isolated by query type
- There are no specialist system prompts — Claude uses its own general HR knowledge rather than the focused federal writing guidance in the specialist prompts
- Draft quality will differ from `WriteJobDescription` results because the grade-ladder context injection (sibling positions selected by richness) is handled server-side regardless of who calls the tool — that part works the same

For most demo scenarios, these differences are invisible. For production HR writing quality and strict agent isolation, the coded orchestrator is the right choice.

---

## Enabling OIDC on the Servers

If you want to run the servers with authentication enabled (for a team demo where multiple people connect), set `Features:EnableOidc: true` in `appsettings.json` and configure the Duende IdentityServer authority. Claude Desktop's stdio transport does not support adding HTTP headers, so OIDC-protected servers require a custom transport proxy.

For local development and demos, keep `EnableOidc: false`. The servers accept any connection.

---

## What Comes Next

The Selector pattern routes each turn to one specialist. But some workflows are inherently sequential — a job announcement must be drafted before it can be compliance-checked, and the compliance outcome must be recorded before the announcement moves forward. Part 8 introduces the Pipe pattern, where each agent's output becomes the next stage's input and no stage runs until the previous one completes.

---

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · [Part 5](part-5-persisting-ai-artifacts.md) · [Part 6](part-6-selector-pattern.md) · **Part 7** · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 6 — The Selector Pattern](part-6-selector-pattern.md) &nbsp;|&nbsp; [Part 8 — The Pipe Pattern →](part-8-pipe-pattern.md)

*Medium: [← Part 6](MEDIUM_URL_PART_6) | [Part 8 →](MEDIUM_URL_PART_8)*

---

## References

### NuGet Packages

- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) — MCP server SDK; `--stdio` flag enables Claude Desktop transport

### Microsoft Documentation

- [.NET MCP SDK — Getting Started](https://learn.microsoft.com/en-us/dotnet/ai/model-context-protocol) — stdio transport configuration
- [Claude Desktop MCP documentation](https://modelcontextprotocol.io/quickstart/user) — `claude_desktop_config.json` format and server registration

### GitHub

- [DotnetMultiAgentsTutorial](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial) — Full source for all patterns in this series
- [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) — Official C# MCP SDK
