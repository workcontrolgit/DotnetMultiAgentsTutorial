using Hr.GroupChatOrchestrator.Agents;
using Hr.GroupChatOrchestrator.Chat;
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

IChatClient reviewerClient = ((IChatClient)new OllamaApiClient(new Uri(ollamaEndpoint), ollamaModel))
    .AsBuilder()
    .Build();

IChatClient mcpClient = ((IChatClient)new OllamaApiClient(new Uri(ollamaEndpoint), ollamaModel))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var getAnnouncementTool = hrTools.First(t => t.Name == "GetJobAnnouncement");
var saveAnnouncementTool = hrTools.First(t => t.Name == "SaveJobAnnouncement");

var hrSpecialist = new ReviewerAgent(
    name: "HR Specialist",
    systemPrompt: """
        You are a senior federal HR specialist reviewing a job announcement draft.
        Focus on: accuracy of position title and series, clarity of duties section,
        qualification requirements alignment with OPM standards, and overall compliance
        with federal hiring language. Be specific - cite exact lines that need improvement.
        """,
    chatClient: reviewerClient,
    numCtx: numCtx);

var legalReviewer = new ReviewerAgent(
    name: "Legal Reviewer",
    systemPrompt: """
        You are a federal employment law specialist reviewing a job announcement draft.
        Focus on: EEO statement completeness, non-discriminatory language throughout,
        reasonable accommodation language, and any phrases that could create legal risk.
        Flag any missing required legal statements. Be specific and cite exact text.
        """,
    chatClient: reviewerClient,
    numCtx: numCtx);

var budgetAnalyst = new ReviewerAgent(
    name: "Budget Analyst",
    systemPrompt: """
        You are a federal budget and compensation analyst reviewing a job announcement draft.
        Focus on: pay grade accuracy, salary range correctness for the grade and location,
        benefits summary completeness, and whether the compensation package is competitive.
        Note any discrepancies between stated grade and salary figures.
        """,
    chatClient: reviewerClient,
    numCtx: numCtx);

var moderator = new ReviewerAgent(
    name: "Moderator",
    systemPrompt: """
        You are a senior HR editor moderating a panel review of a federal job announcement.
        You will receive the original draft and critiques from three experts: HR Specialist,
        Legal Reviewer, and Budget Analyst. Your job is to produce a revised announcement
        that incorporates all valid improvements from each expert. Do not favor any single
        reviewer - synthesize all perspectives. The output must be a complete, polished
        job announcement ready for publication.
        """,
    chatClient: reviewerClient,
    numCtx: numCtx);

Console.Write("Enter announcement ID to review: ");
if (!int.TryParse(Console.ReadLine(), out var announcementId))
{
    Console.WriteLine("Invalid announcement ID. Exiting.");
    return;
}

Console.Write("Enter position ID (needed to save revised draft): ");
if (!int.TryParse(Console.ReadLine(), out var positionId))
{
    Console.WriteLine("Invalid position ID. Exiting.");
    return;
}

var groupChat = new HrGroupChat(
    hrSpecialist, legalReviewer, budgetAnalyst, moderator,
    mcpClient, getAnnouncementTool, saveAnnouncementTool, numCtx);

await groupChat.RunAsync(announcementId, positionId);

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
