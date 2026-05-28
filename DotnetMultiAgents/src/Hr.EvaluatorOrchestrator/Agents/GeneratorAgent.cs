// src/Hr.EvaluatorOrchestrator/Agents/GeneratorAgent.cs
using Microsoft.Extensions.AI;

namespace Hr.EvaluatorOrchestrator.Agents;

/// <summary>
/// Generates a job announcement draft for a given position.
/// On subsequent iterations, receives evaluator feedback and improves the draft.
/// </summary>
public sealed class GeneratorAgent(IChatClient chatClient, IReadOnlyList<AITool> tools, int? numCtx = null)
{
    public async Task<string> GenerateAsync(
        int positionId,
        string? previousFeedback = null,
        CancellationToken ct = default)
    {
        var improvementGuidance = previousFeedback is null
            ? string.Empty
            : $"\n\nPrevious attempt feedback — address all points:\n{previousFeedback}";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, $"""
                You are a federal HR writing specialist.
                Call WriteJobDescription with the given position ID to generate a job announcement draft.
                Return the full draft text and nothing else — no commentary, no preamble.{improvementGuidance}
                """),
            new(ChatRole.User, $"Generate a job announcement for position ID {positionId}."),
        };

        var options = CreateChatOptions([.. tools], numCtx);
        var response = await chatClient.GetResponseAsync(
            messages, options, ct);

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
