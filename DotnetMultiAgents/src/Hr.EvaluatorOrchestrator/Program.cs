// src/Hr.EvaluatorOrchestrator/Program.cs
using Hr.EvaluatorOrchestrator.Agents;
using Hr.EvaluatorOrchestrator.Loop;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OllamaSharp;

// ── MCP connection ─────────────────────────────────────────────────────────────
var hrMcpUrl = Environment.GetEnvironmentVariable("HR_MCP_SERVER_URL")
    ?? "http://localhost:5100/mcp";

await using var hrMcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(hrMcpUrl) }));

var hrTools = (await hrMcpClient.ListToolsAsync()).Cast<AITool>().ToList();
Console.WriteLine($"HR tools: {string.Join(", ", hrTools.Select(t => t.Name))}\n");

// ── Chat clients ───────────────────────────────────────────────────────────────
// Generator and Saver need function invocation; Evaluator reasons over text only.
IChatClient generatorClient = ((IChatClient)new OllamaApiClient(
        new Uri("http://localhost:11434"), "llama3.2"))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

IChatClient evaluatorClient = ((IChatClient)new OllamaApiClient(
        new Uri("http://localhost:11434"), "llama3.2"))
    .AsBuilder()
    .Build();

IChatClient saverClient = ((IChatClient)new OllamaApiClient(
        new Uri("http://localhost:11434"), "llama3.2"))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

// ── Tool subsets ───────────────────────────────────────────────────────────────
var generatorTools = hrTools
    .Where(t => t.Name is "WriteJobDescription" or "GetPositionById")
    .ToList();

var saveAnnouncementTool = hrTools.First(t => t.Name == "SaveJobAnnouncement");

// ── User input ─────────────────────────────────────────────────────────────────
Console.Write("Enter position ID to optimize: ");
if (!int.TryParse(Console.ReadLine(), out var positionId))
{
    Console.WriteLine("Invalid position ID. Exiting.");
    return;
}

// ── Run loop ───────────────────────────────────────────────────────────────────
var loop = new EvaluatorOptimizerLoop(
    new GeneratorAgent(generatorClient, generatorTools),
    new EvaluatorAgent(evaluatorClient),
    saverClient,
    saveAnnouncementTool);

await loop.RunAsync(positionId);
