using System.Net.Http.Json;
using System.Text.Json;
using Hr.SelectorOrchestrator.Agents;
using Hr.SelectorOrchestrator.Orchestration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OllamaSharp;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var enableOidc = bool.TryParse(configuration["Features:EnableOidc"], out var oidcFlag) && oidcFlag;
Dictionary<string, string> authHeaders = [];

if (enableOidc)
{
    var tokenEndpoint = configuration["Oidc:TokenEndpoint"];
    if (string.IsNullOrWhiteSpace(tokenEndpoint))
    {
        var authority = configuration["Oidc:Authority"]
            ?? throw new InvalidOperationException("Missing configuration: Oidc:Authority");
        tokenEndpoint = $"{authority.TrimEnd('/')}/connect/token";
    }

    var clientId = configuration["Oidc:ClientId"]
        ?? throw new InvalidOperationException("Missing configuration: Oidc:ClientId");
    var clientSecret = configuration["Oidc:ClientSecret"]
        ?? throw new InvalidOperationException("Missing configuration: Oidc:ClientSecret");
    var scope = configuration["Oidc:Scope"]
        ?? throw new InvalidOperationException("Missing configuration: Oidc:Scope");

    using var tokenHandler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
    using var tokenClient = new HttpClient(tokenHandler);

    var tokenResponse = await tokenClient.PostAsync(
        tokenEndpoint,
        new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = scope,
        }));
    tokenResponse.EnsureSuccessStatusCode();

    var tokenDoc = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
    var accessToken = tokenDoc.GetProperty("access_token").GetString()!;
    Console.WriteLine("Token acquired.\n");
    authHeaders["Authorization"] = $"Bearer {accessToken}";
}

var defaultTransport = args.Contains("--stream-http") ? "streamHttp"
    : args.Contains("--stdio") ? "stdio"
    : "streamHttp";

var hrServer = new McpServerDefinition("Hr", "McpServers:Hr", configuration["McpServers:Hr:Transport:Type"] ?? defaultTransport);
var complianceServer = new McpServerDefinition("Compliance", "McpServers:Compliance", configuration["McpServers:Compliance:Transport:Type"] ?? defaultTransport);

await using var hrMcpClient = await McpClient.CreateAsync(await CreateClientTransportAsync(configuration, hrServer, authHeaders));
var hrTools = (await hrMcpClient.ListToolsAsync()).Cast<AITool>().ToList();
Console.WriteLine($"HR MCP tools:         {string.Join(", ", hrTools.Select(t => t.Name))}");

await using var complianceMcpClient = await McpClient.CreateAsync(await CreateClientTransportAsync(configuration, complianceServer, authHeaders));
var complianceTools = (await complianceMcpClient.ListToolsAsync()).Cast<AITool>().ToList();
Console.WriteLine($"Compliance MCP tools: {string.Join(", ", complianceTools.Select(t => t.Name))}\n");

IChatClient BuildClient(bool withFunctionInvocation)
{
    var builder = ((IChatClient)new OllamaApiClient(
            new Uri(configuration["AI:Ollama:Endpoint"] ?? "http://localhost:11434"),
            configuration["AI:Ollama:Model"] ?? "gemma4:latest"))
        .AsBuilder();

    if (withFunctionInvocation)
        builder.UseFunctionInvocation();

    return builder.Build();
}

IChatClient routerClient = BuildClient(withFunctionInvocation: false);
IChatClient agentClient = BuildClient(withFunctionInvocation: true);

var router = new AgentRouter(routerClient);

var positionTools = hrTools
    .Where(t => t.Name is "GetOpenPositions" or "GetPositionById"
                       or "GetPositionsByOrganization" or "GetHiringOrganizations")
    .ToList();

var jdTools = hrTools
    .Where(t => t.Name is "WriteJobDescription" or "GetPositionById"
                       or "SaveJobAnnouncement" or "GetJobAnnouncement"
                       or "ListJobAnnouncements")
    .ToList();

var orgTools = hrTools
    .Where(t => t.Name is "GetHiringOrganizations" or "GetPositionsByOrganization")
    .ToList();

var complianceAgentTools = complianceTools
    .Concat(hrTools.Where(t => t.Name is "GetPositionById" or "UpdateAnnouncementStatus"))
    .ToList();

var positionSearchAgent = new SpecialistAgent(
    name: "PositionSearch",
    systemPrompt: """
        You are a federal job search assistant. Help users find and understand open positions.
        - Use GetOpenPositions to list all open roles.
        - Use GetHiringOrganizations then GetPositionsByOrganization to scope by department.
        - Use GetPositionById for full detail on a specific role.
        - Present pay ranges in a readable format (e.g., "$68,000 - $107,000 per year").
        - Be concise; offer to go deeper when the user wants more detail.
        """,
    chatClient: agentClient,
    tools: positionTools);

var jobDescriptionAgent = new SpecialistAgent(
    name: "JobDescription",
    systemPrompt: """
        You are a federal HR writing specialist. Your job is to generate professional job descriptions.
        - Always call WriteJobDescription with the position ID - never write a description yourself.
        - If the user hasn't given you a position ID, ask them which role they want a description for,
          or use GetPositionById if they gave you the title.
        - Keep your framing minimal - let the generated description speak for itself.
        """,
    chatClient: agentClient,
    tools: jdTools);

var orgSummaryAgent = new SpecialistAgent(
    name: "OrgSummary",
    systemPrompt: """
        You are a federal agency structure assistant. Help users understand hiring organizations.
        - Use GetHiringOrganizations to list all agencies and their position counts.
        - Use GetPositionsByOrganization to show what roles exist in a given department.
        - Summarize clearly: organization name, parent department, and any notable openings.
        """,
    chatClient: agentClient,
    tools: orgTools);

var complianceAgent = new SpecialistAgent(
    name: "OPMCompliance",
    systemPrompt: """
        You are a federal HR compliance specialist. Your job is to check whether job positions
        meet OPM (Office of Personnel Management) standards before announcement.

        Guidelines:
        - Use RunFullComplianceCheck with the position ID for a complete compliance report.
        - Use ValidatePayGrade to check a specific pay grade range against OPM standards.
        - Use CheckApplicationPeriod to verify announcement duration meets the 5-business-day minimum.
        - Use GetOPMStandard to look up the qualification standard for an occupational series.
        - Use ListOPMSeries to show all series you know about.
        - If the user gives you a position title but no ID, use GetPositionById first.
        - If the user provides an announcement ID alongside the position ID, call
          UpdateAnnouncementStatus after the compliance check completes:
          set status to CompliancePassed or ComplianceFailed and include a brief summary.

        When reporting results:
        - Clearly state the overall status: PASS, WARNING, or FAIL.
        - For each failed rule, explain what is wrong and suggest a specific correction.
        - For warnings, explain the concern and what to verify manually.
        - Keep the tone professional and actionable.
        """,
    chatClient: agentClient,
    tools: complianceAgentTools);

var generalAgent = new SpecialistAgent(
    name: "General",
    systemPrompt: """
        You are a helpful HR assistant. Answer the user's question directly.
        Let them know you can help with: position search, job descriptions,
        organization summaries, and OPM compliance checks.
        """,
    chatClient: agentClient,
    tools: []);

var orchestrator = new HrOrchestrator(
    router,
    positionSearchAgent,
    jobDescriptionAgent,
    orgSummaryAgent,
    complianceAgent,
    generalAgent);

await orchestrator.RunAsync();

static async Task<IClientTransport> CreateClientTransportAsync(
    IConfiguration configuration,
    McpServerDefinition server,
    Dictionary<string, string> additionalHeaders)
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
        AdditionalHeaders = additionalHeaders,
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
