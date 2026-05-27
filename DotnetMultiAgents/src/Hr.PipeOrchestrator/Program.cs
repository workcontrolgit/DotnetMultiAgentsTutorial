// src/Hr.PipeOrchestrator/Program.cs
using Hr.PipeOrchestrator.Agents;
using Hr.PipeOrchestrator.Pipeline;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OllamaSharp;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var hrMcpUrl = configuration["McpServers:Hr:Url"]
    ?? throw new InvalidOperationException("Missing configuration: McpServers:Hr:Url");
var complianceMcpUrl = configuration["McpServers:Compliance:Url"]
    ?? throw new InvalidOperationException("Missing configuration: McpServers:Compliance:Url");

await using var hrMcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(hrMcpUrl) }));

await using var complianceMcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(complianceMcpUrl) }));

var hrTools = (await hrMcpClient.ListToolsAsync()).Cast<AITool>().ToList();
var complianceTools = (await complianceMcpClient.ListToolsAsync()).Cast<AITool>().ToList();

Console.WriteLine($"HR tools:         {string.Join(", ", hrTools.Select(t => t.Name))}");
Console.WriteLine($"Compliance tools: {string.Join(", ", complianceTools.Select(t => t.Name))}\n");

IChatClient chatClient = ((IChatClient)new OllamaApiClient(
        new Uri(configuration["AI:Ollama:Endpoint"] ?? "http://localhost:11434"),
        configuration["AI:Ollama:Model"] ?? "gemma4:latest"))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var draftTools = hrTools
    .Where(t => t.Name is "WriteJobDescription" or "GetPositionById" or "SaveJobAnnouncement")
    .ToList();

var complianceAgentTools = complianceTools
    .Where(t => t.Name == "RunFullComplianceCheck")
    .ToList();

var updateStatusTool = hrTools.First(t => t.Name == "UpdateAnnouncementStatus");

Console.Write("Enter position ID to process: ");
if (!int.TryParse(Console.ReadLine(), out var positionId))
{
    Console.WriteLine("Invalid position ID. Exiting.");
    return;
}

var pipeline = new HrPipeline(
    new DraftAgent(chatClient, draftTools),
    new ComplianceAgent(chatClient, complianceAgentTools),
    chatClient,
    updateStatusTool);

await pipeline.RunAsync(positionId);
