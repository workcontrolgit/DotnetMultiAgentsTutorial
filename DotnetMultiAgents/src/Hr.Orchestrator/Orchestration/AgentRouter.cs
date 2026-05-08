// Orchestration/AgentRouter.cs
using Microsoft.Extensions.AI;

namespace Hr.Orchestrator.Orchestration;

/// <summary>
/// Uses a lightweight LLM call (no tools) to classify a user query into an <see cref="AgentIntent"/>.
/// Keeping the router model-agnostic means you can swap in a smaller/faster model here
/// while using a more capable model in the specialist agents.
/// </summary>
public sealed class AgentRouter(IChatClient chatClient)
{
    private static readonly string RouterSystemPrompt = """
        You are an intent classifier for an HR assistant application.
        Given a user's message, classify it into exactly one of these categories:

        position_search  — The user wants to list, find, filter, or read details about job positions or openings.
        job_description  — The user wants to write, draft, generate, or review a job description for a role.
        org_summary      — The user wants information about hiring organizations, departments, or agency structures.
        compliance       — The user wants to check OPM compliance, validate a pay grade, check announcement rules, or verify a job posting meets federal standards.
        general          — Anything else: greetings, clarifications, off-topic messages.

        Reply with ONLY the category label — no explanation, no punctuation, no extra words.
        """;

    public async Task<AgentIntent> ClassifyAsync(string userQuery, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, RouterSystemPrompt),
            new(ChatRole.User, userQuery),
        };

        // No tools here — pure text classification keeps latency low.
        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        var label = (response.Text ?? string.Empty).Trim().ToLowerInvariant();

        return label switch
        {
            "position_search" => AgentIntent.PositionSearch,
            "job_description" => AgentIntent.JobDescription,
            "org_summary"     => AgentIntent.OrgSummary,
            "compliance"      => AgentIntent.Compliance,
            _                 => AgentIntent.General,
        };
    }
}
