// src/Hr.GroupChatOrchestrator/Agents/ReviewerAgent.cs
using Microsoft.Extensions.AI;
using Hr.ConsoleShared.Ai;

namespace Hr.GroupChatOrchestrator.Agents;

/// <summary>
/// Single-turn agent used for both reviewers and the Moderator in the group chat.
/// Reviewers call ReviewAsync; the Moderator calls SynthesizeAsync.
/// No MCP tools — all agents reason over draft text passed in the prompt.
/// </summary>
public sealed class ReviewerAgent(string name, string systemPrompt, IChatClient chatClient, int? numCtx = null)
{
    public string Name { get; } = name;

    /// <summary>Critiques the draft from this agent's specialist perspective.</summary>
    public async Task<string> ReviewAsync(string draftText, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, $"Review the following job announcement draft:\n\n{draftText}"),
        };

        var response = await chatClient.GetResponseAsync(messages, ChatOptionsFactory.Create(numCtx), ct);
        return response.Text ?? string.Empty;
    }

    /// <summary>
    /// Synthesizes multiple critiques into a revised draft.
    /// Used by the Moderator only.
    /// </summary>
    public async Task<string> SynthesizeAsync(
        string draftText,
        IReadOnlyList<(string ReviewerName, string Critique)> critiques,
        CancellationToken ct = default)
    {
        var critiqueBlock = string.Join("\n\n", critiques
            .Select(c => $"--- {c.ReviewerName} ---\n{c.Critique}"));

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, $"""
                Original draft:
                {draftText}

                Expert critiques:
                {critiqueBlock}

                Produce a revised draft that addresses all valid critique points.
                Return only the revised announcement text — no commentary, no preamble.
                """),
        };

        var response = await chatClient.GetResponseAsync(messages, ChatOptionsFactory.Create(numCtx), ct);
        return response.Text ?? string.Empty;
    }
}
