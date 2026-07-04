// src/Hr.Agent/Program.cs
using Azure.AI.OpenAI;
using Azure.Identity;
using Hr.Agent;
using Hr.Mcp.Shared.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using OllamaSharp;
using Spectre.Console;
using System.Net.Http.Json;
using System.Text.Json;

var numCtxArg = ParseIntArg(args, "--num-ctx");
var configOverrides = new Dictionary<string, string?>();
if (numCtxArg.HasValue)
    configOverrides["AI:Ollama:NumCtx"] = numCtxArg.Value.ToString();

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile(
        $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json",
        optional: true)
    .AddEnvironmentVariables()
    .AddInMemoryCollection(configOverrides)
    .Build();

var transportType = args.Contains("--stream-http") ? "streamHttp"
    : args.Contains("--stdio") ? "stdio"
    : configuration["McpServer:Transport:Type"] ?? "streamHttp";

var enableDebug = args.Contains("--debug")
    || bool.TryParse(configuration["Features:EnableDebug"], out var debugFlag) && debugFlag;

var enableOidc = bool.TryParse(configuration["Features:EnableOidc"], out var oidcFlag) && oidcFlag;

Dictionary<string, string> additionalHeaders = [];

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

    additionalHeaders["Authorization"] = $"Bearer {accessToken}";
}

var aiProvider = configuration["AI:Provider"] ?? "Ollama";
var aiModel = string.Equals(aiProvider, "AzureOpenAI", StringComparison.OrdinalIgnoreCase)
    ? configuration["AI:AzureOpenAI:Deployment"] ?? "unknown"
    : configuration["AI:Ollama:Model"] ?? "unknown";

var mcpMinLevel = enableDebug ? LogLevel.Debug : LogLevel.Warning;
using var mcpLoggerFactory = LoggerFactory.Create(b => b
    .AddFilter((category, level) =>
        category?.StartsWith("ModelContextProtocol", StringComparison.Ordinal) == true && level >= mcpMinLevel)
    .AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; }));

var serverDefinitions = GetServerDefinitions(configuration, transportType);
var mcpClients = new List<McpClient>();
var toolEntries = new List<(string Server, AITool Tool)>();

try
{
    foreach (var server in serverDefinitions)
    {
        var clientTransport = await McpClientTransportFactory.CreateAsync(configuration, server, additionalHeaders);
        var mcpClient = await McpClient.CreateAsync(clientTransport, loggerFactory: mcpLoggerFactory);
        mcpClients.Add(mcpClient);

        var tools = (await mcpClient.ListToolsAsync()).Cast<AITool>().ToList();
        toolEntries.AddRange(tools.Select(tool => (server.Name, tool)));
    }

    const int W = 43;
    string L(string s) => $"|  {s.PadRight(W)}|";
    string T(string s) => $"|    - {s.PadRight(W - 4)}|";
    Console.WriteLine($"+{new string('-', W + 2)}+");
    Console.WriteLine(L("Hr.Agent"));
    Console.WriteLine(L($"Transport : {transportType}"));
    Console.WriteLine(L($"Provider  : {aiProvider}"));
    Console.WriteLine(L($"Model     : {aiModel}"));
    int? numCtx = int.TryParse(configuration["AI:Ollama:NumCtx"], out var parsedNumCtx) ? parsedNumCtx : null;
    if (numCtx.HasValue)
        Console.WriteLine(L($"NumCtx    : {numCtx.Value:N0}"));
    Console.WriteLine(L($"Servers ({serverDefinitions.Count}) :"));
    foreach (var server in serverDefinitions)
        Console.WriteLine(T(server.Name));
    Console.WriteLine(L($"Tools ({toolEntries.Count})  :"));
    foreach (var entry in toolEntries.OrderBy(t => t.Server).ThenBy(t => t.Tool.Name))
        Console.WriteLine(T($"{entry.Server}: {entry.Tool.Name}"));
    Console.WriteLine(L("Status    : READY"));
    Console.WriteLine($"+{new string('-', W + 2)}+");
    Console.WriteLine();

    var style = UiStyle.Structured;
    AnsiConsole.MarkupLine("[green]Structured[/]\n");

    IChatClient chatClient = CreateChatClient(configuration);
    var exportFolder = configuration["Output:ExportFolder"] ?? "usajobs/output";
    var agent = new HrAgent(chatClient, toolEntries.Select(t => t.Tool).ToList(), style, numCtx, exportFolder);
    await agent.RunAsync();
}
finally
{
    foreach (var mcpClient in mcpClients)
        await mcpClient.DisposeAsync();
}

static List<McpServerDefinition> GetServerDefinitions(IConfiguration configuration, string defaultTransportType)
{
    var configuredServers = configuration.GetSection("McpServers").GetChildren().ToList();
    if (configuredServers.Count == 0)
    {
        return
        [
            new McpServerDefinition("Hr", "McpServer", defaultTransportType)
        ];
    }

    return configuredServers
        .Select(section => new McpServerDefinition(
            section.Key,
            $"McpServers:{section.Key}",
            configuration[$"McpServers:{section.Key}:Transport:Type"] ?? defaultTransportType))
        .ToList();
}

static int? ParseIntArg(string[] args, string flag)
{
    var idx = Array.IndexOf(args, flag);
    return idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out var v) ? v : null;
}

static IChatClient CreateChatClient(IConfiguration configuration)
{
    var provider = configuration["AI:Provider"] ?? "Ollama";

    if (string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase))
    {
        var endpoint = configuration["AI:Ollama:Endpoint"] ?? "http://localhost:11434";
        var model = configuration["AI:Ollama:Model"] ?? "llama3.2";

        return ((IChatClient)new OllamaApiClient(new Uri(endpoint), model))
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();
    }

    var azureEndpoint = configuration["AI:AzureOpenAI:Endpoint"]
        ?? throw new InvalidOperationException("Missing configuration: AI:AzureOpenAI:Endpoint");
    var azureDeployment = configuration["AI:AzureOpenAI:Deployment"]
        ?? throw new InvalidOperationException("Missing configuration: AI:AzureOpenAI:Deployment");
    var apiKey = configuration["AI:AzureOpenAI:ApiKey"];

    var client = string.IsNullOrWhiteSpace(apiKey)
        ? new AzureOpenAIClient(new Uri(azureEndpoint), new DefaultAzureCredential())
        : new AzureOpenAIClient(new Uri(azureEndpoint), new System.ClientModel.ApiKeyCredential(apiKey));

    return ((IChatClient)client.GetChatClient(azureDeployment))
        .AsBuilder()
        .UseFunctionInvocation()
        .Build();
}
