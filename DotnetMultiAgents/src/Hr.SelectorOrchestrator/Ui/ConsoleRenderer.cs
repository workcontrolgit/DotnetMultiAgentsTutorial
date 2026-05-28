using System.Text;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Hr.SelectorOrchestrator.Ui;

internal static class ConsoleRenderer
{
    public static void RenderWelcome()
    {
        AnsiConsole.Write(
            new Panel("[bold]HR Selector Orchestrator[/]\n[grey]Routes each request to the most appropriate specialist.[/]")
                .Header("[cyan]Ready[/]")
                .BorderColor(Color.Cyan1)
                .Padding(1, 0));
        AnsiConsole.MarkupLine("[grey]Type [bold]exit[/] to quit.[/]\n");
    }

    public static string ReadUserInput() =>
        AnsiConsole.Ask<string>("[bold yellow]You >[/]");

    public static void RenderRoute(string agentName) =>
        AnsiConsole.MarkupLine($"[grey][[Router -> {Markup.Escape(agentName)}]][/]");

    public static void RenderReply(string agentName, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            AnsiConsole.MarkupLine("[grey](No response)[/]");
            return;
        }

        AnsiConsole.Write(
            new Panel(BuildRenderable(text))
                .Header($"[bold green]Assistant ({Markup.Escape(agentName)})[/]")
                .BorderColor(Color.Aquamarine3)
                .Padding(1, 0));
        AnsiConsole.WriteLine();
    }

    private static IRenderable BuildRenderable(string text)
    {
        var sections = ParseSections(text);
        if (sections.Count > 0)
        {
            var sectionPanels = sections
                .Select(section => (IRenderable)new Panel(BuildSectionBody(section.Body))
                    .Header($"[bold]{Markup.Escape(section.Title)}[/]")
                    .BorderColor(Color.Grey))
                .ToList();
            return new Rows(sectionPanels);
        }

        var renderables = SplitIntoSegments(text)
            .Select(BuildSegment)
            .Where(renderable => renderable is not null)
            .Select(renderable => renderable!)
            .ToList();

        return renderables.Count == 1 ? renderables[0] : new Rows(renderables);
    }

    private static IRenderable BuildSectionBody(string body)
    {
        var renderables = SplitIntoSegments(body)
            .Select(BuildSegment)
            .Where(renderable => renderable is not null)
            .Select(renderable => renderable!)
            .ToList();

        return renderables.Count == 1 ? renderables[0] : new Rows(renderables);
    }

    private static IRenderable? BuildSegment(Segment segment)
    {
        if (segment.IsTable)
            return BuildMarkdownTable(segment.Text) is { } table
                ? table
                : new Markup(Markup.Escape(segment.Text));

        return BuildMarkdownProse(segment.Text.Trim());
    }

    private static List<Section> ParseSections(string text)
    {
        var sections = new List<Section>();
        string? currentTitle = null;
        var body = new StringBuilder();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.StartsWith("## "))
            {
                if (currentTitle is not null)
                    sections.Add(new Section(currentTitle, body.ToString().Trim()));

                currentTitle = line[3..].Trim();
                body.Clear();
                continue;
            }

            if (currentTitle is not null)
                body.AppendLine(line);
        }

        if (currentTitle is not null)
            sections.Add(new Section(currentTitle, body.ToString().Trim()));

        return sections;
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

            if (IsBulletLine(line, out var bulletContent))
                lines.Add(new Markup("  [grey]•[/] " + ConvertInlineBold(bulletContent)));
            else
                lines.Add(new Markup(ConvertInlineBold(line)));
        }

        return new Rows(lines);
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
        var parts = text.Split("**");
        var sb = new StringBuilder();
        for (var i = 0; i < parts.Length; i++)
            sb.Append(i % 2 == 0
                ? Markup.Escape(parts[i])
                : $"[bold]{Markup.Escape(parts[i])}[/]");
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
    private sealed record Section(string Title, string Body);
}
