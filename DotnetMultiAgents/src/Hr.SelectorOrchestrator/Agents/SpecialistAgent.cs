// Agents/SpecialistAgent.cs
using Microsoft.Extensions.AI;

namespace Hr.SelectorOrchestrator.Agents;

/// <summary>
/// A focused agent with a fixed system prompt and a curated subset of MCP tools.
/// Each specialist handles one category of user intent well rather than all categories adequately.
/// </summary>
public sealed class SpecialistAgent(
    string name,
    string systemPrompt,
    IChatClient chatClient,
    IReadOnlyList<AITool> tools,
    int? numCtx = null)
{
    public string Name { get; } = name;

    public async Task<string> HandleAsync(string userQuery, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userQuery),
        };

        var options = CreateChatOptions([.. tools], numCtx);
        var response = await chatClient.GetResponseAsync(
            messages,
            options,
            ct);

        return response.Text ?? string.Empty;
    }

    private static ChatOptions CreateChatOptions(IReadOnlyList<AITool> toolList, int? numCtx)
    {
        var options = new ChatOptions { Tools = [.. toolList] };
        if (numCtx.HasValue)
        {
            var additional = new AdditionalPropertiesDictionary
            {
                ["num_ctx"] = numCtx.Value
            };
            options.AdditionalProperties = additional;
        }

        return options;
    }
}
