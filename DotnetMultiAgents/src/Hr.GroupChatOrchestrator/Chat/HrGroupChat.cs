// src/Hr.GroupChatOrchestrator/Chat/HrGroupChat.cs
using System.Text;
using Hr.ConsoleShared.Ai;
using Hr.GroupChatOrchestrator.Agents;
using Microsoft.Extensions.AI;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Hr.GroupChatOrchestrator.Chat;

/// <summary>
/// Implements the Group Chat (Debate) pattern.
///
/// Round 1: Three specialists critique the draft in parallel via Task.WhenAll.
///          No reviewer sees another's feedback, which avoids anchoring bias.
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
    AITool saveAnnouncementTool,
    int? numCtx = null)
{
    public async Task RunAsync(int announcementId, int positionId, CancellationToken ct = default)
    {
        AnsiConsole.Write(
            new Panel($"[bold]HR Group Chat[/]\n[grey]Announcement {announcementId}[/]")
                .Header("[cyan]Session[/]")
                .BorderColor(Color.Cyan1)
                .Padding(1, 0));
        AnsiConsole.WriteLine();

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Loading draft from database...", _ => { });

        var loadMessages = new List<ChatMessage>
        {
            new(ChatRole.System,
                "Retrieve the job announcement with the given ID and return its full text verbatim. Do not add commentary."),
            new(ChatRole.User, $"Get job announcement ID {announcementId}."),
        };
        var loadOptions = ChatOptionsFactory.Create([getAnnouncementTool], numCtx);
        var loadResponse = await mcpClient.GetResponseAsync(loadMessages, loadOptions, ct);
        var draftText = loadResponse.Text ?? string.Empty;

        RenderMarkdownPanel("Current Draft", draftText, Color.Grey);

        if (!Confirm("Start group review (3 specialists will critique in parallel)?"))
            return;

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("[yellow]Round 1[/] Running parallel expert review...", _ => { });

        var hrTask = hrSpecialist.ReviewAsync(draftText, ct);
        var legalTask = legalReviewer.ReviewAsync(draftText, ct);
        var budgetTask = budgetAnalyst.ReviewAsync(draftText, ct);
        await Task.WhenAll(hrTask, legalTask, budgetTask);

        RenderMarkdownPanel(hrSpecialist.Name, hrTask.Result, Color.Yellow3);
        RenderMarkdownPanel(legalReviewer.Name, legalTask.Result, Color.Orange1);
        RenderMarkdownPanel(budgetAnalyst.Name, budgetTask.Result, Color.Khaki1);

        if (!Confirm("Continue to Round 2 - Moderator synthesis?"))
            return;

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("[yellow]Round 2[/] Moderator synthesizing critiques into revised draft...", _ => { });

        var critiques = new List<(string, string)>
        {
            (hrSpecialist.Name, hrTask.Result),
            (legalReviewer.Name, legalTask.Result),
            (budgetAnalyst.Name, budgetTask.Result),
        };
        var revisedDraft = await moderator.SynthesizeAsync(draftText, critiques, ct);

        RenderMarkdownPanel("Revised Draft", revisedDraft, Color.Green3);

        if (!Confirm("Save revised draft to database?"))
            return;

        var saveMessages = new List<ChatMessage>
        {
            new(ChatRole.System,
                "Save the provided job announcement draft for the given position. Return only the save confirmation."),
            new(ChatRole.User,
                $"Save this announcement for position ID {positionId}:\n\n{revisedDraft}"),
        };
        var saveOptions = ChatOptionsFactory.Create([saveAnnouncementTool], numCtx);
        var saveResponse = await mcpClient.GetResponseAsync(saveMessages, saveOptions, ct);

        AnsiConsole.MarkupLine($"\n[green]Group chat complete.[/] {Markup.Escape(saveResponse.Text ?? string.Empty)}");
    }

    private static void RenderMarkdownPanel(string title, string text, Color borderColor)
    {
        AnsiConsole.Write(
            new Panel(BuildRenderable(text))
                .Header($"[bold]{Markup.Escape(title)}[/]")
                .BorderColor(borderColor)
                .Padding(1, 0));
        AnsiConsole.WriteLine();
    }

    private static bool Confirm(string prompt) =>
        AnsiConsole.Confirm(prompt, defaultValue: false);

    private static IRenderable BuildRenderable(string text)
    {
        var renderables = SplitIntoSegments(text)
            .Select(BuildSegment)
            .Where(renderable => renderable is not null)
            .Select(renderable => renderable!)
            .ToList();

        return renderables.Count switch
        {
            0 => new Markup("[grey](No content)[/]"),
            1 => renderables[0],
            _ => new Rows(renderables)
        };
    }

    private static IRenderable? BuildSegment(Segment segment)
    {
        if (segment.IsTable)
        {
            var table = BuildMarkdownTable(segment.Text);
            return table is not null ? table : new Markup(Markup.Escape(segment.Text));
        }

        return BuildMarkdownProse(segment.Text.Trim());
    }

    private static IRenderable? BuildMarkdownProse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var lines = new List<IRenderable>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                lines.Add(Text.Empty);
                continue;
            }

            if (IsHorizontalRule(line))
            {
                lines.Add(new Rule().RuleStyle("grey"));
                continue;
            }

            if (TryBuildHeading(line, out var heading))
            {
                lines.Add(heading);
                continue;
            }

            if (IsBulletLine(line, out var bulletContent))
                lines.Add(new Markup("  [grey]-[/] " + ConvertInlineBold(bulletContent)));
            else
                lines.Add(new Markup(ConvertInlineBold(line)));
        }

        return new Rows(lines);
    }

    private static bool TryBuildHeading(string line, out IRenderable heading)
    {
        var trimmed = line.TrimStart();
        var level = 0;
        while (level < trimmed.Length && trimmed[level] == '#')
            level++;

        if (level is < 1 or > 6 || level >= trimmed.Length || !char.IsWhiteSpace(trimmed[level]))
        {
            heading = Text.Empty;
            return false;
        }

        var headingText = trimmed[level..].Trim().Trim('*').Trim();
        var color = level <= 3 ? "deepskyblue1" : "grey70";
        heading = new Markup($"[bold {color}]{Markup.Escape(headingText)}[/]");
        return true;
    }

    private static bool IsHorizontalRule(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= 3 && trimmed.All(c => c == '*' || c == '-' || char.IsWhiteSpace(c));
    }

    private static bool IsBulletLine(string line, out string content)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length >= 2 && (trimmed[0] == '*' || trimmed[0] == '-') && char.IsWhiteSpace(trimmed[1]))
        {
            content = trimmed[1..].TrimStart();
            return true;
        }

        content = string.Empty;
        return false;
    }

    private static string ConvertInlineBold(string text)
    {
        var normalized = ConvertInlineEmphasis(text);
        var parts = normalized.Split("**");
        var sb = new StringBuilder();
        for (var i = 0; i < parts.Length; i++)
            sb.Append(i % 2 == 0
                ? Markup.Escape(parts[i])
                : $"[bold]{Markup.Escape(parts[i])}[/]");
        return sb.ToString();
    }

    private static string ConvertInlineEmphasis(string text)
    {
        var sb = new StringBuilder(text.Length);
        var inEmphasis = false;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '*' &&
                (i == 0 || text[i - 1] != '*') &&
                (i == text.Length - 1 || text[i + 1] != '*'))
            {
                inEmphasis = !inEmphasis;
                continue;
            }

            sb.Append(text[i]);
        }

        return sb.ToString();
    }

    private static List<Segment> SplitIntoSegments(string text)
    {
        var segments = new List<Segment>();
        var buffer = new List<string>();
        var inTable = false;

        foreach (var line in text.Split('\n'))
        {
            var isTableLine = line.TrimEnd().StartsWith('|');
            if (isTableLine != inTable)
            {
                if (buffer.Count > 0)
                    segments.Add(new Segment(string.Join('\n', buffer), inTable));
                buffer.Clear();
                inTable = isTableLine;
            }

            buffer.Add(line.TrimEnd());
        }

        if (buffer.Count > 0)
            segments.Add(new Segment(string.Join('\n', buffer), inTable));

        return segments;
    }

    private static Table? BuildMarkdownTable(string tableText)
    {
        var rows = tableText.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith('|') && !IsSeparatorRow(line))
            .ToList();

        if (rows.Count == 0)
            return null;

        var headers = ParseCells(rows[0]);
        var dataRows = rows.Skip(1).ToList();

        var table = new Table().BorderColor(Color.Teal).Expand();
        foreach (var header in headers)
            table.AddColumn(new TableColumn($"[bold cyan]{Markup.Escape(header)}[/]"));

        foreach (var row in dataRows)
        {
            var cells = ParseCells(row);
            while (cells.Count < headers.Count)
                cells.Add(string.Empty);
            table.AddRow(cells.Take(headers.Count).Select(Markup.Escape).ToArray());
        }

        return table;
    }

    private static bool IsSeparatorRow(string row) =>
        row.Replace("|", "").Replace("-", "").Replace(":", "").Replace(" ", "").Length == 0;

    private static List<string> ParseCells(string line) =>
        line.Trim('|').Split('|').Select(cell => cell.Trim()).ToList();

    private sealed record Segment(string Text, bool IsTable);
}
