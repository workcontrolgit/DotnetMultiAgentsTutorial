// src/Hr.Agent/HrAgent.cs
using Microsoft.Extensions.AI;
using Spectre.Console;
using Spectre.Console.Rendering;
using Hr.ConsoleShared.Exports;
using System.Text;
using System.Text.Json;

namespace Hr.Agent;

public enum UiStyle { Structured, Minimal, Panels }

public sealed class HrAgent(IChatClient chatClient, IList<AITool> tools, UiStyle style = UiStyle.Structured, int? numCtx = null, string outputFolder = "usajobs/output")
{
    private const string SystemPrompt = """
        You are an HR assistant for a U.S. federal agency. Help users explore open job
        positions, hiring organizations, and generate job announcements.

        Guidelines:
        - Always call GetHiringOrganizations before GetPositionsByOrganization.
        - Use GetOpenPositions for an overview; GetPositionById for full detail.
        - When asked to write a job description, call WriteJobDescription with the position ID.
        - To export a position or draft, call the appropriate export tool and use the tool result directly.
        - Format pay ranges as "$85,000 - $110,000 per year".
        - When you receive position data, format it as a markdown table with columns:
          ID, Title, Grade, Salary, Location.
        - Keep answers concise; offer to go deeper when asked.
        - Never present a numbered menu of options or ask the user what they want to do.
          Respond directly to what the user said, or call a tool immediately.
        """;

    private static readonly HashSet<string> ExportToolNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ExportPositionToWord",
            "ExportDraftToWord",
            "ExportPositionsToExcel"
        };

    private readonly string _outputFolder = outputFolder;

    private readonly List<ChatMessage> _history =
    [
        new(ChatRole.System, SystemPrompt)
    ];

    public async Task RunAsync(CancellationToken ct = default)
    {
        RenderWelcome();

        while (!ct.IsCancellationRequested)
        {
            var input = RenderUserPrompt();
            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            _history.Add(new ChatMessage(ChatRole.User, input));

            var text = await RunToolLoopAsync(ct);
            RenderResponse(text);
        }
    }

    private async Task<string> RunToolLoopAsync(CancellationToken ct)
    {
        var additional = new AdditionalPropertiesDictionary();
        if (numCtx.HasValue)
            additional["num_ctx"] = numCtx.Value;

        var options = new ChatOptions { Tools = tools, AdditionalProperties = additional };
        var response = await chatClient.GetResponseAsync(_history, options, ct);

        while (true)
        {
            var toolCalls = response.Messages
                .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                .ToList();

            if (toolCalls.Count == 0)
            {
                _history.AddMessages(response);
                return response.Text ?? string.Empty;
            }

            foreach (var msg in response.Messages)
                _history.Add(msg);

            foreach (var call in toolCalls)
            {
                var fn = tools.FirstOrDefault(t => t.Name == call.Name) as AIFunction;
                object? rawResult;

                if (fn is null)
                {
                    rawResult = $"Tool '{call.Name}' not found.";
                }
                else
                {
                    var fnArgs = call.Arguments is null ? null : new AIFunctionArguments(call.Arguments);
                    try
                    {
                        rawResult = await fn.InvokeAsync(fnArgs, ct);
                    }
                    catch (Exception ex)
                    {
                        rawResult = $"Error: {ex.Message}";
                    }
                }

                if (ExportToolNames.Contains(call.Name ?? string.Empty))
                {
                    var json = rawResult switch
                    {
                        string s => s,
                        TextContent tc => tc.Text ?? string.Empty,
                        JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? string.Empty,
                        JsonElement je => je.GetRawText(),
                        _ => JsonSerializer.Serialize(rawResult)
                    };

                    var saved = ExportFileSaver.TrySaveExportFile(json, _outputFolder);
                    if (saved is not null)
                        rawResult = saved;
                }

                _history.Add(new ChatMessage(ChatRole.Tool,
                    [new FunctionResultContent(call.CallId ?? string.Empty, rawResult)]));
            }

            response = await chatClient.GetResponseAsync(_history, options, ct);
        }
    }

    private void RenderWelcome()
    {
        switch (style)
        {
            case UiStyle.Structured:
                AnsiConsole.Write(new Rule("[bold cyan]HR Assistant[/]").RuleStyle("cyan").LeftJustified());
                AnsiConsole.MarkupLine("[grey]Ask about open positions, organizations, or job descriptions. Type [bold]exit[/] to quit.[/]\n");
                break;
            case UiStyle.Minimal:
                AnsiConsole.MarkupLine("[teal]HR Assistant ready.[/] Ask about open positions, organizations, or job descriptions.");
                AnsiConsole.MarkupLine("[grey]Type [bold]exit[/] to quit.[/]\n");
                break;
            case UiStyle.Panels:
                AnsiConsole.Write(
                    new Panel("[bold]HR Assistant[/]\n[grey]Ask about open positions, organizations, or job descriptions.[/]")
                        .Header("[cyan]Ready[/]")
                        .BorderColor(Color.Cyan1)
                        .Padding(1, 0));
                AnsiConsole.MarkupLine("[grey]Type [bold]exit[/] to quit.[/]\n");
                break;
        }
    }

    private string RenderUserPrompt()
    {
        return style switch
        {
            UiStyle.Structured => ReadWithPrompt("[bold yellow]You >[/] "),
            UiStyle.Minimal => ReadAfterRule(),
            UiStyle.Panels => AnsiConsole.Ask<string>("[bold yellow]You >[/]"),
            _ => string.Empty
        };
    }

    private void RenderResponse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            AnsiConsole.MarkupLine("[grey](No response)[/]");
            return;
        }

        var segments = SplitIntoSegments(text);

        switch (style)
        {
            case UiStyle.Structured:
                AnsiConsole.MarkupLine("\n[bold green]Assistant >[/]");
                RenderSegments(segments);
                AnsiConsole.Write(new Rule().RuleStyle("grey"));
                AnsiConsole.WriteLine();
                break;
            case UiStyle.Minimal:
                AnsiConsole.Write(new Rule("[bold green]Assistant[/]").RuleStyle("grey").LeftJustified());
                RenderSegments(segments);
                AnsiConsole.Write(new Rule().RuleStyle("grey"));
                AnsiConsole.WriteLine();
                break;
            case UiStyle.Panels:
                var renderables = segments
                    .Select(seg =>
                    {
                        if (seg.IsTable)
                            return BuildMarkdownTable(seg.Text) ?? (IRenderable)new Markup(Markup.Escape(seg.Text));
                        var prose = seg.Text.Trim();
                        return string.IsNullOrWhiteSpace(prose) ? null : (IRenderable)new Markup(ConvertInlineBold(prose));
                    })
                    .Where(r => r is not null)
                    .Select(r => r!)
                    .ToList();
                if (renderables.Count > 0)
                {
                    AnsiConsole.Write(new Panel(new Rows(renderables))
                        .Header("[bold green]Assistant[/]")
                        .BorderColor(Color.Aquamarine3)
                        .Padding(1, 0));
                }
                AnsiConsole.WriteLine();
                break;
        }
    }

    private static string ReadWithPrompt(string prompt)
    {
        AnsiConsole.Markup(prompt);
        return Console.ReadLine() ?? string.Empty;
    }

    private static string ReadAfterRule()
    {
        AnsiConsole.Write(new Rule("[bold yellow]You[/]").RuleStyle("grey").LeftJustified());
        return Console.ReadLine() ?? string.Empty;
    }

    private record Segment(string Text, bool IsTable);

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

    private static void RenderSegments(List<Segment> segments)
    {
        foreach (var seg in segments) RenderSegment(seg);
    }

    private static void RenderSegment(Segment seg)
    {
        if (seg.IsTable)
            RenderMarkdownTable(seg.Text);
        else
            RenderMarkdownProse(seg.Text.Trim());
    }

    private static void RenderMarkdownProse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrEmpty(line))
            {
                AnsiConsole.WriteLine();
                continue;
            }

            if (IsHorizontalRule(line))
            {
                AnsiConsole.Write(new Rule().RuleStyle("grey"));
                continue;
            }

            if (TryGetHeadingMarkup(line, out var headingMarkup))
            {
                AnsiConsole.MarkupLine(headingMarkup);
                continue;
            }

            if (IsBulletLine(line, out var bulletContent))
                AnsiConsole.MarkupLine("  [grey]-[/] " + ConvertInlineBold(bulletContent));
            else
                AnsiConsole.MarkupLine(ConvertInlineBold(line));
        }

        AnsiConsole.WriteLine();
    }

    private static bool IsBulletLine(string line, out string content)
    {
        var t = line.TrimStart();
        if (t.Length >= 2 && (t[0] == '*' || t[0] == '-') && char.IsWhiteSpace(t[1]))
        {
            content = t[1..].TrimStart();
            return true;
        }

        content = string.Empty;
        return false;
    }

    private static bool IsHorizontalRule(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= 3 && trimmed.All(c => c == '*' || c == '-' || char.IsWhiteSpace(c));
    }

    private static bool TryGetHeadingMarkup(string line, out string headingMarkup)
    {
        var trimmed = line.TrimStart();
        var level = 0;
        while (level < trimmed.Length && trimmed[level] == '#')
            level++;

        if (level is < 1 or > 6 || level >= trimmed.Length || !char.IsWhiteSpace(trimmed[level]))
        {
            headingMarkup = string.Empty;
            return false;
        }

        var headingText = trimmed[level..].Trim().Trim('*').Trim();
        var color = level <= 3 ? "deepskyblue1" : "grey70";
        headingMarkup = $"[bold {color}]{Markup.Escape(headingText)}[/]";
        return true;
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

    private static void RenderMarkdownTable(string tableText)
    {
        var table = BuildMarkdownTable(tableText);
        if (table is not null)
        {
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }
        else
        {
            AnsiConsole.Write(new Markup(Markup.Escape(tableText)));
            AnsiConsole.WriteLine();
        }
    }

    private static Table? BuildMarkdownTable(string tableText)
    {
        var rows = tableText.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith('|') && !IsSeparatorRow(l))
            .ToList();

        if (rows.Count < 1) return null;

        var headers = ParseCells(rows[0]);
        var dataRows = rows.Skip(1).ToList();

        var table = new Table().BorderColor(Color.Teal).Expand();
        foreach (var h in headers)
            table.AddColumn(new TableColumn($"[bold cyan]{Markup.Escape(h)}[/]"));

        foreach (var row in dataRows)
        {
            var cells = ParseCells(row);
            while (cells.Count < headers.Count) cells.Add(string.Empty);
            table.AddRow(cells.Take(headers.Count).Select(Markup.Escape).ToArray());
        }

        return table;
    }

    private static bool IsSeparatorRow(string row) =>
        row.Replace("|", "").Replace("-", "").Replace(":", "").Replace(" ", "").Length == 0;

    private static List<string> ParseCells(string line) =>
        line.Trim('|').Split('|').Select(c => c.Trim()).ToList();
}
