// src/Hr.EvaluatorOrchestrator/Program.cs
using Hr.EvaluatorOrchestrator.Agents;
using Hr.EvaluatorOrchestrator.Loop;
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

await using var hrMcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(hrMcpUrl) }));

var hrTools = (await hrMcpClient.ListToolsAsync()).Cast<AITool>().ToList();
Console.WriteLine($"HR tools: {string.Join(", ", hrTools.Select(t => t.Name))}\n");

var ollamaEndpoint = configuration["AI:Ollama:Endpoint"] ?? "http://localhost:11434";
var ollamaModel = configuration["AI:Ollama:Model"] ?? "gemma4:latest";

IChatClient generatorClient = ((IChatClient)new OllamaApiClient(new Uri(ollamaEndpoint), ollamaModel))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

IChatClient evaluatorClient = ((IChatClient)new OllamaApiClient(new Uri(ollamaEndpoint), ollamaModel))
    .AsBuilder()
    .Build();

IChatClient saverClient = ((IChatClient)new OllamaApiClient(new Uri(ollamaEndpoint), ollamaModel))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var generatorTools = hrTools
    .Where(t => t.Name is "WriteJobDescription" or "GetPositionById")
    .ToList();

var saveAnnouncementTool = hrTools.First(t => t.Name == "SaveJobAnnouncement");

Console.Write("Enter position ID to optimize: ");
if (!int.TryParse(Console.ReadLine(), out var positionId))
{
    Console.WriteLine("Invalid position ID. Exiting.");
    return;
}

var loop = new EvaluatorOptimizerLoop(
    new GeneratorAgent(generatorClient, generatorTools),
    new EvaluatorAgent(evaluatorClient),
    saverClient,
    saveAnnouncementTool);

await loop.RunAsync(positionId);
