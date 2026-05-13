// src/Hr.PipeOrchestrator/Pipeline/HrPipeline.cs
using Hr.PipeOrchestrator.Agents;
using Microsoft.Extensions.AI;

namespace Hr.PipeOrchestrator.Pipeline;

/// <summary>
/// Coordinates the three-stage HR announcement pipeline.
/// Pauses for user confirmation between stages (semi-automated).
///
/// Pattern: Pipe — each stage's output is the next stage's input.
/// No stage can be skipped; the user controls pace via y/n prompts.
/// </summary>
public sealed class HrPipeline(
    DraftAgent draftAgent,
    ComplianceAgent complianceAgent,
    IChatClient statusClient,
    AITool updateStatusTool)
{
    public async Task RunAsync(int positionId, CancellationToken ct = default)
    {
        Console.WriteLine($"\n{"",60}".Replace(' ', '='));
        Console.WriteLine($"  HR Pipeline — Position {positionId}");
        Console.WriteLine($"{"",60}".Replace(' ', '=') + "\n");

        // ── Stage 1: Draft ───────────────────────────────────────────
        PrintStageHeader(1, "Generating job announcement draft");
        var (draftReply, announcementId) = await draftAgent.RunAsync(positionId, ct);
        Console.WriteLine($"\n{draftReply}\n");

        if (announcementId is null)
            Console.WriteLine("[Warning] Could not extract announcement ID — Stage 3 status update will be skipped.");

        if (!Confirm("Continue to Stage 2 — Compliance Check?")) return;

        // ── Stage 2: Compliance ──────────────────────────────────────
        PrintStageHeader(2, "Running OPM compliance check");
        var (report, passed) = await complianceAgent.RunAsync(positionId, ct);
        Console.WriteLine($"\n{report}\n");

        if (!Confirm("Continue to Stage 3 — Update Status?")) return;

        // ── Stage 3: Update status ───────────────────────────────────
        PrintStageHeader(3, "Recording compliance outcome");

        if (announcementId is null)
        {
            Console.WriteLine("[Skipped] No announcement ID available — cannot update status.");
            return;
        }

        var statusLabel = passed ? "CompliancePassed" : "ComplianceFailed";
        var summary = passed
            ? "All OPM compliance rules passed. Announcement is ready for publication."
            : "One or more OPM compliance rules failed. Review the compliance report above.";

        var statusMessages = new List<ChatMessage>
        {
            new(ChatRole.System,
                "You are an HR status recorder. Call UpdateAnnouncementStatus immediately with the details given. Do not ask questions."),
            new(ChatRole.User,
                $"Update announcement {announcementId} to status {statusLabel} with summary: {summary}"),
        };

        var statusResponse = await statusClient.GetResponseAsync(
            statusMessages, new ChatOptions { Tools = [updateStatusTool] }, ct);

        Console.WriteLine($"\n{statusResponse.Text}\n");

        Console.ForegroundColor = passed ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine($"Pipeline complete. Final status: {statusLabel}");
        Console.ResetColor();
    }

    private static void PrintStageHeader(int stage, string description)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[Stage {stage}/3] {description}...");
        Console.ResetColor();
    }

    private static bool Confirm(string prompt)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{prompt} (y/n): ");
        Console.ResetColor();
        return Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true;
    }
}
