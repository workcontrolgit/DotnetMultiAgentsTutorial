// src/Hr.EvaluatorOrchestrator/Agents/EvaluatorAgent.cs
using System.Text.Json;
using Hr.EvaluatorOrchestrator.Models;
using Microsoft.Extensions.AI;

namespace Hr.EvaluatorOrchestrator.Agents;

/// <summary>
/// Scores a job announcement draft against a 4-criterion rubric (25 pts each, 100 max).
/// Returns a structured <see cref="EvaluationResult"/> parsed from the LLM's JSON response.
/// No MCP tools — reasons purely over the draft text passed in the prompt.
/// </summary>
public sealed class EvaluatorAgent(IChatClient chatClient)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<EvaluationResult> EvaluateAsync(string draftText, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, """
                You are an expert evaluator of federal job announcement drafts.
                Score the draft on these four criteria (0–25 points each, 100 total):
                - Clarity:       Is the writing clear, concise, and professionally structured?
                - OPM Language:  Does it use correct federal HR terminology and OPM style?
                - Completeness:  Does it include all standard sections: duties, qualifications, pay, how to apply?
                - Tone:          Is the tone formal, inclusive, and appropriate for a federal posting?

                Reply with ONLY a valid JSON object — no markdown fences, no extra text:
                {"score":<0-100>,"feedback":{"Clarity":"<note>","OPM Language":"<note>","Completeness":"<note>","Tone":"<note>"}}
                """),
            new(ChatRole.User, $"Evaluate this job announcement:\n\n{draftText}"),
        };

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        var json = (response.Text ?? "{}").Trim();

        try
        {
            var dto = JsonSerializer.Deserialize<EvaluationResultDto>(json, JsonOpts);
            return new EvaluationResult(
                dto?.Score ?? 0,
                dto?.Feedback ?? []);
        }
        catch
        {
            // LLM returned non-JSON — score 0 forces another iteration
            return new EvaluationResult(0, new Dictionary<string, string>
            {
                ["Parse Error"] = $"Non-JSON response: {json[..Math.Min(json.Length, 200)]}"
            });
        }
    }

    private sealed record EvaluationResultDto(int Score, Dictionary<string, string> Feedback);
}
