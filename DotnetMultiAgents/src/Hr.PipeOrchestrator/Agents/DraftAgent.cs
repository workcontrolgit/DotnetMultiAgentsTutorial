// src/Hr.PipeOrchestrator/Agents/DraftAgent.cs
using Microsoft.Extensions.AI;

namespace Hr.PipeOrchestrator.Agents;

/// <summary>
/// Stage 1 of the HR pipeline. Calls WriteJobDescription then SaveJobAnnouncement.
/// Parses the saved announcement ID from the LLM reply.
/// </summary>
public sealed class DraftAgent(IChatClient chatClient, IReadOnlyList<AITool> tools)
{
    public async Task<(string Reply, int? AnnouncementId)> RunAsync(int positionId, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, """
                You are a federal HR writing specialist operating in an automated pipeline.
                When given a position ID:
                1. Call WriteJobDescription to generate the announcement draft.
                2. Call SaveJobAnnouncement with the position ID and the draft text.
                3. Include the saved announcement ID in your reply using this exact format on its own line:
                   ANNOUNCEMENT_ID:<id>
                Do not ask questions. Complete both tool calls before responding.
                """),
            new(ChatRole.User, $"Generate and save a job announcement draft for position ID {positionId}."),
        };

        var response = await chatClient.GetResponseAsync(
            messages, new ChatOptions { Tools = [.. tools] }, ct);

        var text = response.Text ?? string.Empty;
        return (text, ParseAnnouncementId(text));
    }

    private static int? ParseAnnouncementId(string text)
    {
        const string prefix = "ANNOUNCEMENT_ID:";
        var idx = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var token = text[(idx + prefix.Length)..].Trim().Split([' ', '\n', '\r'], 2)[0];
        return int.TryParse(token, out var id) ? id : null;
    }
}
