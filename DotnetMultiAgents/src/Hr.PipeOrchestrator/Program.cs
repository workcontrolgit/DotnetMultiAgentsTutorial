// src/Hr.PipeOrchestrator/Program.cs
using Hr.PipeOrchestrator.Agents;
using Hr.PipeOrchestrator.Pipeline;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OllamaSharp;

// ── MCP connections ──────────────────────────────────────────────────────────
var hrMcpUrl = Environment.GetEnvironmentVariable("HR_MCP_SERVER_URL")
    ?? "http://localhost:5100/mcp";
var complianceMcpUrl = Environment.GetEnvironmentVariable("COMPLIANCE_MCP_SERVER_URL")
    ?? "http://localhost:5200/compliance";

await using var hrMcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(hrMcpUrl) }));

await using var complianceMcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(complianceMcpUrl) }));

var hrTools         = (await hrMcpClient.ListToolsAsync()).Cast<AITool>().ToList();
var complianceTools = (await complianceMcpClient.ListToolsAsync()).Cast<AITool>().ToList();

Console.WriteLine($"HR tools:         {string.Join(", ", hrTools.Select(t => t.Name))}");
Console.WriteLine($"Compliance tools: {string.Join(", ", complianceTools.Select(t => t.Name))}\n");

// ── Chat client ──────────────────────────────────────────────────────────────
IChatClient chatClient = ((IChatClient)new OllamaApiClient(
        new Uri("http://localhost:11434"), "llama3.2"))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

// ── Tool subsets ─────────────────────────────────────────────────────────────
var draftTools = hrTools
    .Where(t => t.Name is "WriteJobDescription" or "GetPositionById" or "SaveJobAnnouncement")
    .ToList();

var complianceAgentTools = complianceTools
    .Where(t => t.Name == "RunFullComplianceCheck")
    .ToList();

var updateStatusTool = hrTools.First(t => t.Name == "UpdateAnnouncementStatus");

// ── User input ───────────────────────────────────────────────────────────────
Console.Write("Enter position ID to process: ");
if (!int.TryParse(Console.ReadLine(), out var positionId))
{
    Console.WriteLine("Invalid position ID. Exiting.");
    return;
}

// ── Run pipeline ─────────────────────────────────────────────────────────────
var pipeline = new HrPipeline(
    new DraftAgent(chatClient, draftTools),
    new ComplianceAgent(chatClient, complianceAgentTools),
    chatClient,
    updateStatusTool);

await pipeline.RunAsync(positionId);
