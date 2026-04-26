# DotnetAiAgentMcp

A tutorial repository demonstrating **AI Agents** and the **Model Context Protocol (MCP)** using .NET 10 and Clean Architecture.

This standalone series builds a fully functional MCP server that exposes HR data (positions, departments, salary ranges) as tools any MCP-compatible AI client can call — including Claude Desktop, VS Code Copilot, and your own .NET agents.

📖 **Blog Series:** [Series 1: AI Agents & MCP with .NET 10](blogs/series-1-ai-agent-mcp/)

## What You'll Build

* A Clean Architecture .NET 10 solution with HR domain data
* An AI agent with inline tool use (Microsoft.Extensions.AI + Ollama)
* A standalone MCP server exposing position data and an AI-powered job description writer
* Claude Desktop integration via stdio transport

## Quick Start

```bash
# Prerequisites: .NET 10 SDK, SQL Server LocalDB, Ollama with llama3.2

# Clone
git clone https://github.com/workcontrolgit/DotnetAiAgentMcp.git
cd DotnetAiAgentMcp

# Restore and build
dotnet build DotnetAiAgentMcp.sln

# Run database migrations
dotnet ef database update --project src/HrMcp.Infrastructure.Persistence --startup-project src/HrMcp.McpServer

# Start MCP server (HTTP mode, port 5100)
dotnet run --project src/HrMcp.McpServer
```

## Related Series

* [AngularNetTutorial](https://github.com/workcontrolgit/AngularNetTutorial) — Full-stack Angular 20 / .NET 10 / Duende IdentityServer tutorial

## License

MIT
