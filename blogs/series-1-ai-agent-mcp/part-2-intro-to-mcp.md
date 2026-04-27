# Part 2: Introduction to Model Context Protocol

**Series:** AI Agents & MCP with .NET 10 | **Part 2 of 5**  
**GitHub:** [workcontrolgit/DotnetAiAgentMcp](https://github.com/workcontrolgit/DotnetAiAgentMcp)

---

## Introduction

In Part 1 we built a federal HR domain with Clean Architecture. The data is structured, the services are in place, and the application compiles cleanly. But an AI cannot reach any of it yet.

That is what Model Context Protocol (MCP) is for. Before we write a single line of MCP code in Part 3, this post builds the mental model: what problem MCP solves, how it works, and why it matters specifically for .NET developers.

No code in this post. Only concepts.

---

## The N×M Integration Problem

Every AI application needs to reach external data. Before MCP, that meant writing a custom integration for each combination of AI host and data source.

Imagine you have:

- **N AI hosts** — Claude Desktop, a VS Code extension, a custom chatbot, a Slack bot
- **M data sources** — a SQL database, a REST API, a file system, a third-party SaaS

Without a standard protocol, you need **N × M custom integrations**. Four hosts × four sources = **16 separate integrations** to build, maintain, and keep synchronized as both sides evolve. Each integration speaks a different language: some REST, some SDK-specific, some bespoke. A change to one data source breaks some integrations but not others. Security policies are inconsistent. Testing coverage is incomplete.

MCP collapses N×M down to **N + M**:

- Each AI host implements the MCP **client** once
- Each data source implements the MCP **server** once
- Any client connects to any server through the shared protocol

Four hosts + four sources = **8 implementations** instead of 16. And as the ecosystem grows, you add one server — every existing host can use it immediately, without changes on either side.

---

## What MCP Is

The [Model Context Protocol](https://modelcontextprotocol.io) is an open standard published by Anthropic in November 2024. It defines how AI applications connect to external data sources and tools: the message format, transport layer, capability negotiation, and session lifecycle for that connection.

Think of it as **HTTP for AI tool use** — a universal contract that any host and any server can implement independently, without knowledge of each other's internals.

---

## MCP Primitives

MCP defines three building blocks that a server can expose to a client. Every capability maps to one of them.

### Tools

Tools are **functions the AI can call**. They take structured inputs, perform an action, and return a result. The AI decides when to call a tool based on conversation context; the server executes it and returns the output.

In our project, `GetOpenPositions` and `WriteJobDescription` are tools. When a user asks "what federal jobs are available?", the AI calls `GetOpenPositions` — the server queries the database and returns the positions. The AI never touches the database directly.

Tools are the most-used MCP primitive. They are the `.NET method` of the MCP world.

### Resources

Resources are **data the AI can read**, identified by URIs. Unlike tools, resources are not invoked with arguments — they are fetched by address and return content: text, JSON, or binary.

Think of resources as files or reference material the AI needs to understand context: a README, a schema definition, a policy document. They provide information; they do not perform actions.

### Prompts

Prompts are **reusable message templates** stored on the server. A client fetches a named prompt, fills in its parameters, and injects the result into a conversation.

They are useful for encoding domain knowledge that should stay close to the data. For example, a `job_description_template` prompt can carry OPM formatting rules and federal plain-language writing standards — every AI host that connects gets the same authoritative template without the host needing to know those rules itself.

---

## Architecture: Host → Client → Server

![MCP Architecture: Host, Client, and Server roles](diagrams/part-2-diagram-1-host-client-server.png)

MCP defines three roles in every interaction.

**Host** is the AI application the user interacts with — Claude Desktop, VS Code Copilot Chat, a custom .NET console agent. The host embeds one or more MCP clients and decides which tools to surface to the language model.

**Client** is a component inside the host that manages the connection to one MCP server. It handles session lifecycle, capability negotiation, and message routing. One host can hold multiple clients — one per server it connects to.

**Server** is the process that exposes tools, resources, and prompts. It does not know which host is calling it. It only speaks MCP.

The flow for a single tool call:

> **User** sends a message → **Host** passes context to the **LLM** → LLM decides to call a tool → Host routes the call through a **Client** → Client sends a JSON-RPC request to the **Server** → Server executes the tool → Server returns the result → Client delivers it to the Host → Host feeds it to the LLM → LLM generates the final response for the **User**

The critical insight: **the LLM never calls the server directly**. The host mediates everything. The server has no knowledge of the conversation, the model, or the user — it only receives and responds to well-formed JSON-RPC messages.

---

## Transports

MCP separates the protocol (what messages mean) from the transport (how they travel). Two transports are defined in the specification.

### stdio

The server runs as a child process. The host writes JSON-RPC messages to the server's **stdin** and reads responses from **stdout**. Stderr is available for logs and diagnostics.

This is the transport used by Claude Desktop, VS Code Copilot Chat, and any other desktop AI host. It is simple, secure (no network exposure), and requires no port configuration. The server binary is launched and killed by the host — it has no independent lifetime outside that relationship.

The critical rule for stdio: **stdout must contain only JSON-RPC messages**. Any log output, startup banners, or debug text written to stdout corrupts the message stream and breaks the session. In Part 3 we handle this by detecting a `--stdio` flag at startup and suppressing all logging when it is set.

### HTTP with SSE (Server-Sent Events)

The server runs as a persistent HTTP process. The client sends requests via HTTP POST and receives streaming responses over a persistent Server-Sent Events connection.

This transport suits remote servers, shared team servers, and multi-client scenarios. It also enables interactive testing via the MCP Inspector — you point the inspector at `http://localhost:5100/mcp` and call tools directly without any AI host involved.

In this series, we support both transports from the same binary: `dotnet run` starts HTTP mode, `dotnet run -- --stdio` starts stdio mode.

---

## MCP vs Other Patterns

MCP is not the first attempt to give AI models access to external capabilities.

**MCP vs Function Calling (OpenAI / Anthropic)**

Function calling is a model-level feature: you define functions in the API request and the model returns structured call arguments. It is tightly coupled to the API provider and the calling application. MCP is transport-level and provider-agnostic — the same MCP server works with Claude Desktop, VS Code, and any future MCP-compatible host without modification.

**MCP vs REST APIs**

A REST API requires the AI to know the URL structure, authentication scheme, request format, and error handling for each service. MCP presents a uniform interface: discover tools, call tools, read resources. No per-service knowledge is required in the host.

**MCP vs LangChain / Semantic Kernel Tools**

Framework-specific tool definitions are coupled to the framework. A tool written for Semantic Kernel does not work in LangChain without a rewrite. An MCP server is framework-agnostic — any MCP-compatible host can call it, regardless of what AI framework it uses internally.

**MCP vs ChatGPT Plugins**

ChatGPT Plugins were OpenAI-specific and required a public HTTP endpoint with a registered manifest. MCP supports local stdio transport (no network exposure, no public URL), works with multiple hosts, and is a vendor-neutral open standard with a published specification.

The common thread: **MCP is a protocol, not a framework or an API**. Like HTTP, it outlives any individual implementation.

---

## The ModelContextProtocol NuGet Package

Anthropic publishes an official .NET SDK for MCP under two packages:

- **`ModelContextProtocol`** — core protocol, types, and client/server primitives
- **`ModelContextProtocol.AspNetCore`** — ASP.NET Core middleware and service registration for hosting MCP over HTTP/SSE

Both are maintained at [github.com/modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) and target .NET 8+.

We use these in Part 3. The SDK handles JSON-RPC serialization, session management, capability negotiation, and transport routing — we write C# classes with attributes, and the protocol plumbing is invisible.

---

## Preview: What We Build in Part 3

Part 3 wires the official SDK into `HrMcp.McpServer` and exposes four tools over both HTTP/SSE and stdio.

- **`GetOpenPositions`** — returns all positions where `IsOpen = true`, including pay ranges, duty locations, and clearance requirements
- **`GetHiringOrganizations`** — returns the federal hiring offices in the database with their department affiliations
- **`GetPositionsByOrganization`** — returns positions filtered to a specific organization ID
- **`WriteJobDescription`** — accepts a position ID and returns a formatted job description draft (stub in Part 3; upgraded to real LLM output in Part 4)

After Part 3, you can open MCP Inspector, point it at `http://localhost:5100/mcp`, and call all four tools interactively against real DHS position data — no AI host required.

---

## Next Up

**[Part 3: Building an MCP Server in .NET 10 →](part-3-mcp-server-dotnet.md)**

We install the official `ModelContextProtocol` SDK, implement the four tool classes, configure both transports, and verify everything end-to-end with MCP Inspector.

---

## Sources

- [Model Context Protocol — Official Documentation](https://modelcontextprotocol.io/introduction)
- [MCP Specification](https://spec.modelcontextprotocol.io/)
- [ModelContextProtocol C# SDK — GitHub](https://github.com/modelcontextprotocol/csharp-sdk)
- [ModelContextProtocol NuGet Package](https://www.nuget.org/packages/ModelContextProtocol)
- [Anthropic MCP Announcement — November 2024](https://www.anthropic.com/news/model-context-protocol)
