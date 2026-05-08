// src/Hr.Orchestrator/Program.cs
using System.Net.Http.Json;
using System.Text.Json;
using Hr.Orchestrator.Agents;
using Hr.Orchestrator.Orchestration;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OllamaSharp;

// ---------------------------------------------------------------------------
// 1. Acquire bearer token (client credentials)
// ---------------------------------------------------------------------------
using var tokenHandler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};
using var tokenClient = new HttpClient(tokenHandler);

var tokenResponse = await tokenClient.PostAsync(
    "https://localhost:44310/connect/token",
    new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["grant_type"]    = "client_credentials",
        ["client_id"]     = "hr-mcp-agent",
        ["client_secret"] = "hr-mcp-agent-secret",
        ["scope"]         = "hr-mcp-api",
    }));
tokenResponse.EnsureSuccessStatusCode();

var tokenDoc    = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
var accessToken = tokenDoc.GetProperty("access_token").GetString()!;
Console.WriteLine("Token acquired.\n");

// ---------------------------------------------------------------------------
// 2. Connect to Hr.Jobs.Mcp (HR data tools — port 5100)
// ---------------------------------------------------------------------------
var hrMcpUrl = Environment.GetEnvironmentVariable("HR_MCP_SERVER_URL") ?? "http://localhost:5100/mcp";

await using var hrMcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint          = new Uri(hrMcpUrl),
        AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {accessToken}" }
    }));

var hrTools = (await hrMcpClient.ListToolsAsync()).Cast<AITool>().ToList();
Console.WriteLine($"HR MCP tools:         {string.Join(", ", hrTools.Select(t => t.Name))}");

// ---------------------------------------------------------------------------
// 3. Connect to Hr.Compliance.Mcp (OPM rule engine — port 5200)
// ---------------------------------------------------------------------------
var complianceMcpUrl = Environment.GetEnvironmentVariable("COMPLIANCE_MCP_SERVER_URL") ?? "http://localhost:5200/compliance";

await using var complianceMcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri(complianceMcpUrl),
        // ComplianceMcp uses same OIDC token when auth is enabled;
        // remove the header when Features:EnableOidc = false on the compliance server.
        AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {accessToken}" }
    }));

var complianceTools = (await complianceMcpClient.ListToolsAsync()).Cast<AITool>().ToList();
Console.WriteLine($"Compliance MCP tools: {string.Join(", ", complianceTools.Select(t => t.Name))}\n");

// ---------------------------------------------------------------------------
// 4. Build chat clients
//    - routerClient : pure text classification, no function invocation
//    - agentClient  : full function-invocation pipeline for specialist agents
// ---------------------------------------------------------------------------
IChatClient BuildClient(bool withFunctionInvocation)
{
    var builder = ((IChatClient)new OllamaApiClient(
            new Uri("http://localhost:11434"), "llama3.2"))
        .AsBuilder();

    if (withFunctionInvocation)
        builder.UseFunctionInvocation();

    return builder.Build();
}

IChatClient routerClient = BuildClient(withFunctionInvocation: false);
IChatClient agentClient  = BuildClient(withFunctionInvocation: true);

// ---------------------------------------------------------------------------
// 5. Create the router
// ---------------------------------------------------------------------------
var router = new AgentRouter(routerClient);

// ---------------------------------------------------------------------------
// 6. Define specialist agents — each gets focused tools and a focused prompt
// ---------------------------------------------------------------------------

// HR data tool subsets
var positionTools = hrTools
    .Where(t => t.Name is "GetOpenPositions" or "GetPositionById"
                       or "GetPositionsByOrganization" or "GetHiringOrganizations")
    .ToList();

var jdTools = hrTools
    .Where(t => t.Name is "WriteJobDescription" or "GetPositionById")
    .ToList();

var orgTools = hrTools
    .Where(t => t.Name is "GetHiringOrganizations" or "GetPositionsByOrganization")
    .ToList();

// Compliance agent gets ALL compliance tools + GetPositionById to look up position context
var complianceAgentTools = complianceTools
    .Concat(hrTools.Where(t => t.Name == "GetPositionById"))
    .ToList();

var positionSearchAgent = new SpecialistAgent(
    name: "PositionSearch",
    systemPrompt: """
        You are a federal job search assistant. Help users find and understand open positions.
        - Use GetOpenPositions to list all open roles.
        - Use GetHiringOrganizations then GetPositionsByOrganization to scope by department.
        - Use GetPositionById for full detail on a specific role.
        - Present pay ranges in a readable format (e.g., "$68,000 – $107,000 per year").
        - Be concise; offer to go deeper when the user wants more detail.
        """,
    chatClient: agentClient,
    tools: positionTools);

var jobDescriptionAgent = new SpecialistAgent(
    name: "JobDescription",
    systemPrompt: """
        You are a federal HR writing specialist. Your job is to generate professional job descriptions.
        - Always call WriteJobDescription with the position ID — never write a description yourself.
        - If the user hasn't given you a position ID, ask them which role they want a description for,
          or use GetPositionById if they gave you the title.
        - Keep your framing minimal — let the generated description speak for itself.
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

// ---------------------------------------------------------------------------
// 7. Run the orchestrator
// ---------------------------------------------------------------------------
var orchestrator = new HrOrchestrator(
    router,
    positionSearchAgent,
    jobDescriptionAgent,
    orgSummaryAgent,
    complianceAgent,
    generalAgent);

await orchestrator.RunAsync();
