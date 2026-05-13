// src/Hr.GroupChatOrchestrator/Program.cs
using Hr.GroupChatOrchestrator.Agents;
using Hr.GroupChatOrchestrator.Chat;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OllamaSharp;

// ── MCP connection ────────────────────────────────────────────────────────────
var hrMcpUrl = Environment.GetEnvironmentVariable("HR_MCP_SERVER_URL")
    ?? "http://localhost:5100/mcp";

await using var hrMcpClient = await McpClient.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(hrMcpUrl) }));

var hrTools = (await hrMcpClient.ListToolsAsync()).Cast<AITool>().ToList();
Console.WriteLine($"HR tools: {string.Join(", ", hrTools.Select(t => t.Name))}\n");

// ── Reviewer client (no function invocation — reviewers don't call MCP tools) ─
IChatClient reviewerClient = ((IChatClient)new OllamaApiClient(
        new Uri("http://localhost:11434"), "llama3.2"))
    .AsBuilder()
    .Build();

// ── MCP client (needs function invocation for GetJobAnnouncement + Save) ──────
IChatClient mcpClient = ((IChatClient)new OllamaApiClient(
        new Uri("http://localhost:11434"), "llama3.2"))
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var getAnnouncementTool  = hrTools.First(t => t.Name == "GetJobAnnouncement");
var saveAnnouncementTool = hrTools.First(t => t.Name == "SaveJobAnnouncement");

// ── Reviewer agents ───────────────────────────────────────────────────────────
var hrSpecialist = new ReviewerAgent(
    name: "HR Specialist",
    systemPrompt: """
        You are a senior federal HR specialist reviewing a job announcement draft.
        Focus on: accuracy of position title and series, clarity of duties section,
        qualification requirements alignment with OPM standards, and overall compliance
        with federal hiring language. Be specific — cite exact lines that need improvement.
        """,
    chatClient: reviewerClient);

var legalReviewer = new ReviewerAgent(
    name: "Legal Reviewer",
    systemPrompt: """
        You are a federal employment law specialist reviewing a job announcement draft.
        Focus on: EEO statement completeness, non-discriminatory language throughout,
        reasonable accommodation language, and any phrases that could create legal risk.
        Flag any missing required legal statements. Be specific and cite exact text.
        """,
    chatClient: reviewerClient);

var budgetAnalyst = new ReviewerAgent(
    name: "Budget Analyst",
    systemPrompt: """
        You are a federal budget and compensation analyst reviewing a job announcement draft.
        Focus on: pay grade accuracy, salary range correctness for the grade and location,
        benefits summary completeness, and whether the compensation package is competitive.
        Note any discrepancies between stated grade and salary figures.
        """,
    chatClient: reviewerClient);

var moderator = new ReviewerAgent(
    name: "Moderator",
    systemPrompt: """
        You are a senior HR editor moderating a panel review of a federal job announcement.
        You will receive the original draft and critiques from three experts: HR Specialist,
        Legal Reviewer, and Budget Analyst. Your job is to produce a revised announcement
        that incorporates all valid improvements from each expert. Do not favor any single
        reviewer — synthesize all perspectives. The output must be a complete, polished
        job announcement ready for publication.
        """,
    chatClient: reviewerClient);

// ── User input ────────────────────────────────────────────────────────────────
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

// ── Run ───────────────────────────────────────────────────────────────────────
var groupChat = new HrGroupChat(
    hrSpecialist, legalReviewer, budgetAnalyst, moderator,
    mcpClient, getAnnouncementTool, saveAnnouncementTool);

await groupChat.RunAsync(announcementId, positionId);
