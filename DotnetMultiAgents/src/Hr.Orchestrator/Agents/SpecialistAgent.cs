// Agents/SpecialistAgent.cs
using Microsoft.Extensions.AI;

namespace Hr.Orchestrator.Agents;

/// <summary>
/// A focused agent with a fixed system prompt and a curated subset of MCP tools.
/// Each specialist handles one category of user intent well rather than all categories adequately.
/// </summary>
public sealed class SpecialistAgent(
    string name,
    string systemPrompt,
    IChatClient chatClient,
    IReadOnlyList<AITool> tools)
{
    public string Name { get; } = name;

    public async Task<string> HandleAsync(string userQuery, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userQuery),
        };

        var response = await chatClient.GetResponseAsync(
            messages,
            new ChatOptions { Tools = [.. tools] },
            ct);

        return response.Text ?? string.Empty;
    }
}
