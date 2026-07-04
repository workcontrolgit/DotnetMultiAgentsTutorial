using Hr.EvaluatorOrchestrator.Agents;
using Hr.EvaluatorOrchestrator.Loop;
using Hr.ConsoleShared.Startup;
using Hr.Mcp.Shared.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OllamaSharp;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var defaultTransport = args.Contains("--stream-http") ? "streamHttp"
    : args.Contains("--stdio") ? "stdio"
    : "streamHttp";

var hrServer = new McpServerDefinition("Hr", "McpServers:Hr", configuration["McpServers:Hr:Transport:Type"] ?? defaultTransport);

await using var hrMcpClient = await McpClient.CreateAsync(await McpClientTransportFactory.CreateAsync(configuration, hrServer));

var hrTools = (await hrMcpClient.ListToolsAsync()).Cast<AITool>().ToList();

var ollamaEndpoint = configuration["AI:Ollama:Endpoint"] ?? "http://localhost:11434";
var ollamaModel = configuration["AI:Ollama:Model"] ?? "gemma4:latest";
int? numCtx = int.TryParse(configuration["AI:Ollama:NumCtx"], out var parsedNumCtx) ? parsedNumCtx : null;

StartupBannerWriter.Write(
    "Hr.EvaluatorOrchestrator",
    defaultTransport,
    "Ollama",
    ollamaModel,
    numCtx,
    [hrServer.Name],
    hrTools.Select(tool => (hrServer.Name, tool)).ToList());

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
    new GeneratorAgent(generatorClient, generatorTools, numCtx),
    new EvaluatorAgent(evaluatorClient, numCtx),
    saverClient,
    saveAnnouncementTool,
    numCtx);

await loop.RunAsync(positionId);

