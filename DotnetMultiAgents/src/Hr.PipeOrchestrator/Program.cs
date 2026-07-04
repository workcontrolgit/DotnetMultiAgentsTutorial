using Hr.PipeOrchestrator.Agents;
using Hr.PipeOrchestrator.Pipeline;
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
var complianceServer = new McpServerDefinition("Compliance", "McpServers:Compliance", configuration["McpServers:Compliance:Transport:Type"] ?? defaultTransport);

await using var hrMcpClient = await McpClient.CreateAsync(await McpClientTransportFactory.CreateAsync(configuration, hrServer));
await using var complianceMcpClient = await McpClient.CreateAsync(await McpClientTransportFactory.CreateAsync(configuration, complianceServer));

var hrTools = (await hrMcpClient.ListToolsAsync()).Cast<AITool>().ToList();
var complianceTools = (await complianceMcpClient.ListToolsAsync()).Cast<AITool>().ToList();

int? numCtx = int.TryParse(configuration["AI:Ollama:NumCtx"], out var parsedNumCtx) ? parsedNumCtx : null;
var ollamaEndpoint = configuration["AI:Ollama:Endpoint"] ?? "http://localhost:11434";
var ollamaModel = configuration["AI:Ollama:Model"] ?? "gemma4:latest";

IChatClient chatClient = ((IChatClient)new OllamaApiClient(
        new Uri(ollamaEndpoint),
        ollamaModel))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

StartupBannerWriter.Write(
    "Hr.PipeOrchestrator",
    defaultTransport,
    "Ollama",
    ollamaModel,
    numCtx,
    [complianceServer.Name, hrServer.Name],
    hrTools.Select(tool => (hrServer.Name, tool))
        .Concat(complianceTools.Select(tool => (complianceServer.Name, tool)))
        .ToList());

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
    new DraftAgent(chatClient, draftTools, numCtx),
    new ComplianceAgent(chatClient, complianceAgentTools, numCtx),
    chatClient,
    updateStatusTool,
    numCtx);

await pipeline.RunAsync(positionId);

