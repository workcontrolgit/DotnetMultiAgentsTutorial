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

var defaultTransport = args.Contains("--stream-http") ? "streamHttp"
    : args.Contains("--stdio") ? "stdio"
    : "streamHttp";

var hrServer = new McpServerDefinition("Hr", "McpServers:Hr", configuration["McpServers:Hr:Transport:Type"] ?? defaultTransport);
var complianceServer = new McpServerDefinition("Compliance", "McpServers:Compliance", configuration["McpServers:Compliance:Transport:Type"] ?? defaultTransport);

await using var hrMcpClient = await McpClient.CreateAsync(await CreateClientTransportAsync(configuration, hrServer));
await using var complianceMcpClient = await McpClient.CreateAsync(await CreateClientTransportAsync(configuration, complianceServer));

var hrTools = (await hrMcpClient.ListToolsAsync()).Cast<AITool>().ToList();
var complianceTools = (await complianceMcpClient.ListToolsAsync()).Cast<AITool>().ToList();

Console.WriteLine($"HR tools:         {string.Join(", ", hrTools.Select(t => t.Name))}");
Console.WriteLine($"Compliance tools: {string.Join(", ", complianceTools.Select(t => t.Name))}\n");

int? numCtx = int.TryParse(configuration["AI:Ollama:NumCtx"], out var parsedNumCtx) ? parsedNumCtx : null;

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
    new DraftAgent(chatClient, draftTools, numCtx),
    new ComplianceAgent(chatClient, complianceAgentTools, numCtx),
    chatClient,
    updateStatusTool,
    numCtx);

await pipeline.RunAsync(positionId);

static async Task<IClientTransport> CreateClientTransportAsync(IConfiguration configuration, McpServerDefinition server)
{
    if (string.Equals(server.TransportType, "stdio", StringComparison.OrdinalIgnoreCase))
    {
        var command = configuration[$"{server.ConfigPath}:Transport:Stdio:Command"] ?? "dotnet";
        var workingDirectory = configuration[$"{server.ConfigPath}:Transport:Stdio:WorkingDirectory"];
        if (string.IsNullOrWhiteSpace(workingDirectory))
            workingDirectory = FindWorkspaceRoot();

        var projectPath = configuration[$"{server.ConfigPath}:Transport:Stdio:ProjectPath"]
            ?? throw new InvalidOperationException($"Missing configuration: {server.ConfigPath}:Transport:Stdio:ProjectPath");

        return new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = command,
            Arguments = ["run", "--project", projectPath, "--", "--stdio"],
            WorkingDirectory = workingDirectory,
            Name = $"{server.Name.ToLowerInvariant()}-mcp-stdio"
        });
    }

    var url = configuration[$"{server.ConfigPath}:Transport:StreamHttp:Url"]
        ?? throw new InvalidOperationException($"Missing configuration: {server.ConfigPath}:Transport:StreamHttp:Url");

    return new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri(url),
        TransportMode = HttpTransportMode.StreamableHttp,
        Name = $"{server.Name.ToLowerInvariant()}-mcp-stream-http"
    });
}

static string FindWorkspaceRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "DotnetMultiAgents")))
            return dir.FullName;
    }

    return AppContext.BaseDirectory;
}

internal sealed record McpServerDefinition(string Name, string ConfigPath, string TransportType);
