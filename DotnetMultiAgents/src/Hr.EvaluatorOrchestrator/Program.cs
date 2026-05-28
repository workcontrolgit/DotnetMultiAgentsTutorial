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

var defaultTransport = args.Contains("--stream-http") ? "streamHttp"
    : args.Contains("--stdio") ? "stdio"
    : "streamHttp";

var hrServer = new McpServerDefinition("Hr", "McpServers:Hr", configuration["McpServers:Hr:Transport:Type"] ?? defaultTransport);

await using var hrMcpClient = await McpClient.CreateAsync(await CreateClientTransportAsync(configuration, hrServer));

var hrTools = (await hrMcpClient.ListToolsAsync()).Cast<AITool>().ToList();
Console.WriteLine($"HR tools: {string.Join(", ", hrTools.Select(t => t.Name))}\n");

var ollamaEndpoint = configuration["AI:Ollama:Endpoint"] ?? "http://localhost:11434";
var ollamaModel = configuration["AI:Ollama:Model"] ?? "gemma4:latest";
int? numCtx = int.TryParse(configuration["AI:Ollama:NumCtx"], out var parsedNumCtx) ? parsedNumCtx : null;

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
