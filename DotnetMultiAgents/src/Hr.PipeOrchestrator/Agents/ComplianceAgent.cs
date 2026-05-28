// src/Hr.PipeOrchestrator/Agents/ComplianceAgent.cs
using Microsoft.Extensions.AI;

namespace Hr.PipeOrchestrator.Agents;

/// <summary>
/// Stage 2 of the HR pipeline. Runs RunFullComplianceCheck and returns
/// the full report plus a pass/fail flag parsed from a sentinel line.
/// </summary>
public sealed class ComplianceAgent(IChatClient chatClient, IReadOnlyList<AITool> tools, int? numCtx = null)
{
    public async Task<(string Report, bool Passed)> RunAsync(int positionId, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, """
                You are a federal HR compliance specialist operating in an automated pipeline.
                Run RunFullComplianceCheck for the given position ID and report all results clearly.
                End your reply with exactly one of these lines (no extra text after it):
                COMPLIANCE_RESULT:PASSED
                COMPLIANCE_RESULT:FAILED
                """),
            new(ChatRole.User, $"Run a full OPM compliance check for position ID {positionId}."),
        };

        var options = CreateChatOptions([.. tools], numCtx);
        var response = await chatClient.GetResponseAsync(
            messages, options, ct);

        var text = response.Text ?? string.Empty;
        var passed = text.Contains("COMPLIANCE_RESULT:PASSED", StringComparison.OrdinalIgnoreCase);
        return (text, passed);
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
