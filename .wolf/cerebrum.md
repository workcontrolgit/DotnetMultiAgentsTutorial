# Cerebrum

> OpenWolf's learning memory. Updated automatically as the AI learns from interactions.
> Do not edit manually unless correcting an error.
> Last updated: 2026-05-07

## User Preferences

- **No markdown tables in blog posts.** Convert to bullet lists or prose sections for Medium.com compatibility. When checklist items say "table", write a bullet list instead and update the checklist item to say "bullet list — no markdown table".
- **Blog post structure:** Use numbered steps, prose explanations, and code blocks with language tags. No tables anywhere.
- **Blog images must render outside local repo previews.** Prefer absolute hosted image URLs for post content intended for Medium/remote platforms, not relative local file paths.
- **When a pattern works in existing parts, keep consistency.** Do not switch image link style globally based on one failed render; follow the already-working convention unless asked.
- **Blog series content direction:** Remove DotnetFastMCP mentions/callouts from the series when requested; keep the narrative focused on the official MCP SDK path.
- **Future blog planning:** When requesting future-series direction, provide a concrete draft roadmap with placeholders, especially for data scaling and reference-based generation improvements.

## Key Learnings

- **Project:** DotnetMultiAgentsTutorial
- **Description:** A tutorial repository demonstrating **Multi-Agent systems** using Microsoft Agent Framework, .NET 10, MCP, and Clean Architecture.
- **Solution file:** `DotnetMultiAgents/DotnetMultiAgents.slnx` (renamed from DotnetAiAgentMcp — not `.sln`)
- **Project naming convention:** `Hr.[Subdomain].[Technology]` — e.g., `Hr.Core`, `Hr.Jobs.Mcp`, `Hr.Compliance.Mcp`, `Hr.Orchestrator`. Root namespace matches project name.
- **Blog series location:** `blogs/` — new multi-agent series in progress; old series-1-ai-agent-mcp removed.
- **Blog diagram convention:** Place diagram/image assets under `blogs/<series>/diagrams/` and reference them using relative paths like `diagrams/<filename>.png`.
- **OllamaSharp vs deprecated package:** `Microsoft.Extensions.AI.Ollama` is deprecated in the GA release. Use `OllamaSharp` (`OllamaApiClient`) instead. `OllamaApiClient` implements `IChatClient` natively.
- **OllamaApiClient implements both IChatClient and IEmbeddingGenerator.** Calling `.AsBuilder()` directly on `new OllamaApiClient(...)` is ambiguous. Must cast first: `((IChatClient)new OllamaApiClient(...)).AsBuilder()...`
- **ModelContextProtocol package split (1.x):** `ModelContextProtocol` is server-side only. Client API (`McpClient`, `HttpClientTransport`, `McpClientTool`) lives in `ModelContextProtocol.Core` (pulled in transitively). Use namespace `ModelContextProtocol.Client`.
- **M.E.AI 9.x renames:** `CompleteAsync` → `GetResponseAsync`, `ChatCompletion` → `ChatResponse`, use `.Text` for assistant text, `_history.AddMessages(response)` instead of `_history.Add(response.Message)`.
- **stdio transport rule:** stdout must be pure JSON-RPC when `--stdio` flag is set. Clear all log providers and redirect to stderr. `builder.WebHost.UseUrls()` with no args prevents HTTP listener.
- **`.vscode/mcp.json` gitignore:** `.vscode/` is gitignored. Use `.vscode/*` + `!.vscode/mcp.json` negation to track only the shared MCP config.

## Do-Not-Repeat

- **[2026-04-26]** Do NOT use `Microsoft.Extensions.AI.Ollama` (`OllamaChatClient`). It is deprecated in GA. Use `OllamaSharp` (`OllamaApiClient`) instead.
- **[2026-04-26]** Do NOT call `.AsBuilder()` directly on `new OllamaApiClient(...)` — ambiguous overload. Always cast: `((IChatClient)new OllamaApiClient(...)).AsBuilder()`.
- **[2026-04-26]** Do NOT use `McpClientFactory` or `SseClientTransport` — removed in `ModelContextProtocol` 1.x. Use `McpClient.CreateAsync(new HttpClientTransport(...))`.
- **[2026-04-26]** Do NOT use `IChatClient.CompleteAsync` — renamed to `GetResponseAsync` in M.E.AI 9.10.2 GA.
- **[2026-04-26]** Do NOT pin `Microsoft.Extensions.AI` at `9.*` when `OllamaSharp 5.*` is in the project. OllamaSharp 5.4+ pulls `Microsoft.Extensions.AI.Abstractions 10.4.x` transitively, causing a `TypeLoadException` at runtime on `FunctionApprovalRequestContent`. Pin `Microsoft.Extensions.AI` at `10.*`.

## Decision Log

- **[2026-05-07] Rename DotnetAiAgentMcp → DotnetMultiAgents:** Project renamed to match repo name `DotnetMultiAgentsTutorial`. Solution file also renamed to `DotnetMultiAgents.slnx`. New series focuses on multi-agent patterns with Microsoft Agent Framework.
- **[2026-05-07] Rename HrMcp.* → Hr.* namespace:** All 7 projects renamed from `HrMcp.*` to consistent `Hr.[Subdomain].[Technology]` pattern. `HrMcp.McpServer` → `Hr.Jobs.Mcp`, `HrMcp.ComplianceMcp` → `Hr.Compliance.Mcp`, `HrMcp.Infrastructure.Persistence` → `Hr.Infrastructure`, etc. Reason: old names were inconsistent (McpServer vs ComplianceMcp style). Solution builds clean with 0 errors/warnings.
- **[2026-05-07] Remove series-1-ai-agent-mcp blogs:** Old single-agent MCP blog series removed. New blog series covers multi-agent development targeting Medium.com.
- **[2026-04-26] Part 5 before Part 6 (Claude Desktop before OIDC security):** Keep Claude Desktop as Part 5 because it's the payoff demo. Security (OIDC) added in Part 6 on top of a working system. stdio transport is local-only so no network exposure risk in the demo.
- **[2026-04-26] OllamaSharp version constraint `5.*`:** Matches version `5.3.4` used in reference project. Stable GA release.
- **[2026-04-26] Duende IdentityServer container setup (local dev):** STS at `https://localhost:44310` (nginx proxy, self-signed cert). Authority = `https://localhost:44310`, Audience = API resource name (e.g., `hr-mcp`). JWT Bearer backchannel needs `DangerousAcceptAnyServerCertificateValidator` in Development. Client credentials grant: client ID `hr-mcp-agent`, secret `hr-mcp-agent-secret`, scope `hr-mcp-api`. Token endpoint: `https://localhost:44310/connect/token`. Agent HTTP client also needs cert bypass for the token request.
