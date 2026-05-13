// src/Hr.GroupChatOrchestrator/Chat/HrGroupChat.cs
using Hr.GroupChatOrchestrator.Agents;
using Microsoft.Extensions.AI;

namespace Hr.GroupChatOrchestrator.Chat;

/// <summary>
/// Implements the Group Chat (Debate) pattern.
///
/// Round 1: Three specialists critique the draft in parallel via Task.WhenAll.
///          No reviewer sees another's feedback — eliminates anchoring bias.
/// Round 2: Moderator synthesizes all critiques into a revised draft.
/// The revised draft is saved via SaveJobAnnouncement after user confirmation.
/// </summary>
public sealed class HrGroupChat(
    ReviewerAgent hrSpecialist,
    ReviewerAgent legalReviewer,
    ReviewerAgent budgetAnalyst,
    ReviewerAgent moderator,
    IChatClient mcpClient,
    AITool getAnnouncementTool,
    AITool saveAnnouncementTool)
{
    public async Task RunAsync(int announcementId, int positionId, CancellationToken ct = default)
    {
        Console.WriteLine($"\n{"",60}".Replace(' ', '='));
        Console.WriteLine($"  HR Group Chat — Announcement {announcementId}");
        Console.WriteLine($"{"",60}".Replace(' ', '=') + "\n");

        // ── Load draft ───────────────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Loading draft from database...");
        Console.ResetColor();

        var loadMessages = new List<ChatMessage>
        {
            new(ChatRole.System,
                "Retrieve the job announcement with the given ID and return its full text verbatim. Do not add commentary."),
            new(ChatRole.User, $"Get job announcement ID {announcementId}."),
        };
        var loadResponse = await mcpClient.GetResponseAsync(
            loadMessages, new ChatOptions { Tools = [getAnnouncementTool] }, ct);
        var draftText = loadResponse.Text ?? string.Empty;

        Console.WriteLine($"\n--- Current Draft ---\n{draftText}\n");

        if (!Confirm("Start group review (3 specialists will critique in parallel)?")) return;

        // ── Round 1: Parallel debate ─────────────────────────────────
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n[Round 1] Running parallel expert review...");
        Console.ResetColor();

        var hrTask     = hrSpecialist.ReviewAsync(draftText, ct);
        var legalTask  = legalReviewer.ReviewAsync(draftText, ct);
        var budgetTask = budgetAnalyst.ReviewAsync(draftText, ct);
        await Task.WhenAll(hrTask, legalTask, budgetTask);

        PrintCritique(hrSpecialist.Name,  hrTask.Result);
        PrintCritique(legalReviewer.Name, legalTask.Result);
        PrintCritique(budgetAnalyst.Name, budgetTask.Result);

        if (!Confirm("Continue to Round 2 — Moderator synthesis?")) return;

        // ── Round 2: Synthesis ───────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n[Round 2] Moderator synthesizing critiques into revised draft...");
        Console.ResetColor();

        var critiques = new List<(string, string)>
        {
            (hrSpecialist.Name,  hrTask.Result),
            (legalReviewer.Name, legalTask.Result),
            (budgetAnalyst.Name, budgetTask.Result),
        };
        var revisedDraft = await moderator.SynthesizeAsync(draftText, critiques, ct);

        Console.WriteLine($"\n--- Revised Draft ---\n{revisedDraft}\n");

        if (!Confirm("Save revised draft to database?")) return;

        // ── Save revised draft ───────────────────────────────────────
        var saveMessages = new List<ChatMessage>
        {
            new(ChatRole.System,
                "Save the provided job announcement draft for the given position. Return only the save confirmation."),
            new(ChatRole.User,
                $"Save this announcement for position ID {positionId}:\n\n{revisedDraft}"),
        };
        var saveResponse = await mcpClient.GetResponseAsync(
            saveMessages, new ChatOptions { Tools = [saveAnnouncementTool] }, ct);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\nGroup chat complete. {saveResponse.Text}");
        Console.ResetColor();
    }

    private static void PrintCritique(string name, string critique)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine($"\n[{name}]");
        Console.ResetColor();
        Console.WriteLine(critique);
    }

    private static bool Confirm(string prompt)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{prompt} (y/n): ");
        Console.ResetColor();
        return Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true;
    }
}
