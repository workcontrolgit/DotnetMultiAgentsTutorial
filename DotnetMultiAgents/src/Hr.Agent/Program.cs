// src/Hr.Agent/Program.cs
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using OllamaSharp;
using Hr.Agent;
using System.Net.Http.Json;
using System.Text.Json;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

var mcpServerUrl    = config["McpServer:Url"]!;
var tokenEndpoint   = config["Oidc:TokenEndpoint"]!;
var clientId        = config["Oidc:ClientId"]!;
var clientSecret    = config["Oidc:ClientSecret"]!;
var scope           = config["Oidc:Scope"]!;
var ollamaBaseUrl   = config["Ollama:BaseUrl"]!;
var ollamaModel     = config["Ollama:Model"]!;

// --- Token acquisition (client credentials flow) ---
// Trust self-signed cert used by the local Duende IdentityServer container
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
        ["grant_type"]    = "client_credentials",
        ["client_id"]     = clientId,
        ["client_secret"] = clientSecret,
        ["scope"]         = scope,
    }));
tokenResponse.EnsureSuccessStatusCode();

var tokenDoc    = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
var accessToken = tokenDoc.GetProperty("access_token").GetString()!;
Console.WriteLine("Token acquired.\n");

// --- Connect to MCP server with bearer token ---
await using var mcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri(mcpServerUrl),
        AdditionalHeaders = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {accessToken}"
        }
    }));

var mcpTools = await mcpClient.ListToolsAsync();
Console.WriteLine($"Connected. Tools: {string.Join(", ", mcpTools.Select(t => t.Name))}\n");

// OllamaApiClient implements IChatClient (Microsoft.Extensions.AI) natively.
// Cast to IChatClient explicitly to resolve AsBuilder() overload ambiguity.
IChatClient chatClient = ((IChatClient)new OllamaApiClient(
        new Uri(ollamaBaseUrl), ollamaModel))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var agent = new HrAgent(chatClient, mcpTools.Cast<AITool>().ToList());
await agent.RunAsync();
