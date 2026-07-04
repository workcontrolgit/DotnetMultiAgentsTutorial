// src/Hr.EvaluatorOrchestrator/Loop/EvaluatorOptimizerLoop.cs
using Hr.EvaluatorOrchestrator.Agents;
using Hr.EvaluatorOrchestrator.Models;
using Hr.ConsoleShared.Ai;
using Microsoft.Extensions.AI;

namespace Hr.EvaluatorOrchestrator.Loop;

/// <summary>
/// Implements the Evaluator-Optimizer pattern.
///
/// Each iteration:
///   1. GeneratorAgent produces (or improves) a draft using evaluator feedback.
///   2. EvaluatorAgent scores the draft on four criteria (0–100).
///   3. Score ≥ 80 → exit; else inject feedback and retry.
/// Maximum 3 iterations. The highest-scoring draft is saved.
/// </summary>
public sealed class EvaluatorOptimizerLoop(
    GeneratorAgent generator,
    EvaluatorAgent evaluator,
    IChatClient saverClient,
    AITool saveAnnouncementTool,
    int? numCtx = null,
    int maxIterations = 3,
    int threshold = 80)
{
    public async Task RunAsync(int positionId, CancellationToken ct = default)
    {
        Console.WriteLine($"\n{"",60}".Replace(' ', '='));
        Console.WriteLine($"  Evaluator-Optimizer — Position {positionId}");
        Console.WriteLine($"  Threshold: {threshold}/100 | Max iterations: {maxIterations}");
        Console.WriteLine($"{"",60}".Replace(' ', '=') + "\n");

        string bestDraft    = string.Empty;
        int    bestScore    = -1;
        string? lastFeedback = null;

        for (var i = 1; i <= maxIterations; i++)
        {
            PrintHeader(i, maxIterations, "Generating draft");
            var draft = await generator.GenerateAsync(positionId, lastFeedback, ct);

            PrintHeader(i, maxIterations, "Evaluating draft");
            var result = await evaluator.EvaluateAsync(draft, ct);

            PrintEvaluation(i, draft, result);

            if (result.Score > bestScore)
            {
                bestScore = result.Score;
                bestDraft = draft;
            }

            if (result.MeetsThreshold)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\nQuality threshold met ({result.Score}/100). Exiting loop.\n");
                Console.ResetColor();
                break;
            }

            if (i < maxIterations)
            {
                lastFeedback = BuildFeedbackPrompt(result);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Score {result.Score}/100 — below threshold.");
                Console.ResetColor();
                if (!Confirm("Continue to next iteration with feedback?")) break;
                Console.WriteLine();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\nMax iterations reached. Best score: {bestScore}/100.\n");
                Console.ResetColor();
            }
        }

        // Save the highest-scoring draft
        Console.WriteLine("Saving best draft to database...");
        var saveMessages = new List<ChatMessage>
        {
            new(ChatRole.System,
                "Save the provided job announcement draft for the given position. Return only the save confirmation."),
            new(ChatRole.User,
                $"Save this announcement for position ID {positionId}:\n\n{bestDraft}"),
        };
        var saveOptions = ChatOptionsFactory.Create([saveAnnouncementTool], numCtx);
        var saveResponse = await saverClient.GetResponseAsync(
            saveMessages, saveOptions, ct);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\nDone. {saveResponse.Text}");
        Console.ResetColor();
    }

    private static void PrintHeader(int iteration, int max, string action)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[Iteration {iteration}/{max}] {action}...");
        Console.ResetColor();
    }

    private static void PrintEvaluation(int iteration, string draft, EvaluationResult result)
    {
        Console.WriteLine($"\n--- Draft (Iteration {iteration}) ---");
        Console.WriteLine(draft.Length > 500 ? draft[..500] + "..." : draft);
        Console.WriteLine($"\n--- Evaluation Score: {result.Score}/100 ---");
        foreach (var (criterion, note) in result.Feedback)
            Console.WriteLine($"  {criterion,-15}: {note}");
        Console.WriteLine();
    }

    private static bool Confirm(string prompt)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{prompt} (y/n): ");
        Console.ResetColor();
        return Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string BuildFeedbackPrompt(EvaluationResult result)
    {
        var lines = result.Feedback.Select(kv => $"- {kv.Key}: {kv.Value}");
        return $"Previous attempt scored {result.Score}/100. Specific weaknesses to address:\n{string.Join("\n", lines)}";
    }
}
