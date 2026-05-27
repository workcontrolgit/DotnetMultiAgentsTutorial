using System.ComponentModel;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Hr.Application.Services;
using Hr.Core.Entities;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SS = DocumentFormat.OpenXml.Spreadsheet;
using Table = DocumentFormat.OpenXml.Wordprocessing.Table;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;
using HrPosition = Hr.Core.Entities.Position;

namespace Hr.Jobs.Mcp.Tools;

[McpServerToolType]
public sealed class ExportTools(PositionService positions, ILogger<ExportTools> logger)
{
    [McpServerTool(Name = "ExportPositionToWord"),
     Description("Exports a position's full structured data to a Word (.docx) file. Returns a JSON payload with fileName and base64-encoded content for the agent to save locally.")]
    public async Task<string> ExportPositionToWord(
        [Description("The numeric ID of the position to export")] int positionId,
        CancellationToken ct = default)
    {
        logger.LogInformation("[Request ] ExportPositionToWord positionId={PositionId}", positionId);
        var p = await positions.GetPositionByIdAsync(positionId, ct);
        if (p is null)
        {
            logger.LogWarning("[Response] ExportPositionToWord positionId={PositionId} => not found", positionId);
            return $"Position {positionId} not found.";
        }

        var bytes = BuildPositionDocx(p);
        var result = ToBase64Json($"position-{positionId}.docx", bytes);
        logger.LogInformation("[Response] ExportPositionToWord positionId={PositionId} => {Bytes} bytes", positionId, bytes.Length);
        return result;
    }

    [McpServerTool(Name = "ExportDraftToWord"),
     Description("Exports an AI-generated job description draft to an editable Word (.docx) file. Pass the full current draft text, including any user edits. Returns a JSON payload with fileName and base64-encoded content for the agent to save locally.")]
    public async Task<string> ExportDraftToWord(
        [Description("The numeric ID of the position the draft is for")] int positionId,
        [Description("The full job description draft text. Markdown headings (##) become Word headings.")] string draftContent,
        CancellationToken ct = default)
    {
        logger.LogInformation("[Request ] ExportDraftToWord positionId={PositionId}", positionId);

        if (string.IsNullOrWhiteSpace(draftContent))
            return "Draft content is empty - nothing to export.";

        var p = await positions.GetPositionByIdAsync(positionId, ct);
        var title = p?.Title ?? $"Position {positionId}";
        var org = p is null ? string.Empty : $"{p.HiringOrganization?.DepartmentName} | {p.HiringOrganization?.OrganizationName}";

        var bytes = BuildDraftDocx(title, org, draftContent);
        var result = ToBase64Json($"position-{positionId}-draft.docx", bytes);
        logger.LogInformation("[Response] ExportDraftToWord positionId={PositionId} => {Bytes} bytes", positionId, bytes.Length);
        return result;
    }

    [McpServerTool(Name = "ExportPositionsToExcel"),
     Description("Exports all open positions to an Excel (.xlsx) spreadsheet. Returns a JSON payload with fileName and base64-encoded content for the agent to save locally.")]
    public async Task<string> ExportPositionsToExcel(CancellationToken ct = default)
    {
        logger.LogInformation("[Request ] ExportPositionsToExcel");
        var list = await positions.GetOpenPositionsAsync(ct);
        var positionList = list.ToList();

        var bytes = BuildPositionsExcel(positionList);
        var result = ToBase64Json("positions.xlsx", bytes);
        logger.LogInformation("[Response] ExportPositionsToExcel => {Count} rows, {Bytes} bytes", positionList.Count, bytes.Length);
        return result;
    }

    private static byte[] BuildPositionDocx(HrPosition p)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            AddStylesPart(mainPart);
            mainPart.Document = new Document();
            var body = new Body();
            mainPart.Document.AppendChild(body);

            var rem = p.PositionRemuneration;
            var grade = p.PayGradeMin == p.PayGradeMax ? p.PayGradeMin : $"{p.PayGradeMin}-{p.PayGradeMax}";
            var minSal = rem?.MinimumRange is > 0 ? $"${rem.MinimumRange:N0}" : "N/A";
            var maxSal = rem?.MaximumRange is > 0 ? $"${rem.MaximumRange:N0}" : "N/A";

            body.AppendChild(StyledParagraph("Heading1", p.Title ?? string.Empty));
            body.AppendChild(StyledParagraph("Normal", $"{p.HiringOrganization?.DepartmentName} | {p.HiringOrganization?.OrganizationName}"));
            body.AppendChild(new Paragraph());

            body.AppendChild(StyledParagraph("Heading2", "Overview"));
            body.AppendChild(BuildOverviewTable(p, rem, grade, minSal, maxSal));
            body.AppendChild(new Paragraph());

            AppendSection(body, "Summary", p.Description);
            AppendSection(body, "Duties", p.Duties);
            body.AppendChild(StyledParagraph("Heading2", "Requirements"));
            AppendSection(body, "Conditions of Employment", p.ConditionsOfEmployment, "Heading3");
            AppendSection(body, "Qualifications", p.Qualifications, "Heading3");
            AppendSection(body, "Education", p.Education, "Heading3");
            AppendSection(body, "Additional Information", p.AdditionalInformation, "Heading3");
            AppendSection(body, "How You Will Be Evaluated", p.Evaluations);
            AppendSection(body, "Required Documents", p.RequiredDocuments);
            AppendSection(body, "How to Apply", p.HowToApply);

            body.AppendChild(StyledParagraph("Heading3", "Agency Contact Information"));
            body.AppendChild(NormalParagraph($"Name: {p.ContactName}"));
            body.AppendChild(NormalParagraph($"Phone: {p.ContactPhone}"));
            body.AppendChild(NormalParagraph($"Email: {p.ContactEmail}"));
            body.AppendChild(NormalParagraph($"Address: {p.ContactAddress}"));

            AppendSection(body, "Next Steps", p.NextSteps, "Heading3");

            doc.Save();
        }

        return ms.ToArray();
    }

    private static byte[] BuildDraftDocx(string title, string org, string markdownDraft)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            AddStylesPart(mainPart);
            mainPart.Document = new Document();
            var body = new Body();
            mainPart.Document.AppendChild(body);

            body.AppendChild(StyledParagraph("Heading1", title));
            if (!string.IsNullOrWhiteSpace(org))
                body.AppendChild(StyledParagraph("Normal", org));

            var notice = new Paragraph(new Run(
                new RunProperties(new Italic(), new Color { Val = "888888" }),
                new Text("AI Draft - For Review and Editing")
            ));
            body.AppendChild(notice);
            body.AppendChild(new Paragraph());

            AppendMarkdownContent(body, markdownDraft);

            doc.Save();
        }

        return ms.ToArray();
    }

    private static void AppendMarkdownContent(Body body, string markdown)
    {
        foreach (var rawLine in markdown.Split('\n'))
        {
            var line = rawLine.TrimEnd();

            if (line.StartsWith("## "))
                body.AppendChild(RichParagraph("Heading2", line[3..].Trim()));
            else if (line.StartsWith("### "))
                body.AppendChild(RichParagraph("Heading3", line[4..].Trim()));
            else if (IsBulletLine(line, out var bulletText))
                body.AppendChild(BulletParagraph(bulletText));
            else if (string.IsNullOrWhiteSpace(line))
                body.AppendChild(new Paragraph());
            else
                body.AppendChild(RichParagraph(null, line));
        }
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

    private static Paragraph RichParagraph(string? styleId, string text)
    {
        var para = new Paragraph();
        if (styleId is not null)
            para.AppendChild(new ParagraphProperties(new ParagraphStyleId { Val = styleId }));
        AppendInlineRuns(para, text);
        return para;
    }

    private static Paragraph BulletParagraph(string text)
    {
        var para = new Paragraph();
        var pp = new ParagraphProperties();
        pp.AppendChild(new Indentation { Left = "360", Hanging = "360" });
        para.AppendChild(pp);
        para.AppendChild(new Run(new Text("• ") { Space = SpaceProcessingModeValues.Preserve }));
        AppendInlineRuns(para, text);
        return para;
    }

    private static void AppendInlineRuns(Paragraph para, string text)
    {
        var parts = text.Split("**");
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
                continue;

            var run = new Run(new Text(parts[i]) { Space = SpaceProcessingModeValues.Preserve });
            if (i % 2 == 1)
                run.PrependChild(new RunProperties(new Bold()));
            para.AppendChild(run);
        }
    }

    private static byte[] BuildPositionsExcel(IEnumerable<HrPosition> positionList)
    {
        using var ms = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = doc.AddWorkbookPart();
            workbookPart.Workbook = new SS.Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SS.SheetData();
            worksheetPart.Worksheet = new SS.Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new SS.Sheets());
            sheets.AppendChild(new SS.Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "Open Positions"
            });

            string[] headers =
            [
                "ID", "Title", "Series", "Pay Grade",
                "Salary Min", "Salary Max", "Location",
                "Telework", "Security Clearance", "Who May Apply",
                "Department", "Organization", "Open Date", "Close Date"
            ];

            sheetData.AppendChild(ExcelRow(1, headers));

            uint rowIndex = 2;
            foreach (var p in positionList)
            {
                var rem = p.PositionRemuneration;
                var grade = p.PayGradeMin == p.PayGradeMax ? p.PayGradeMin : $"{p.PayGradeMin}-{p.PayGradeMax}";
                sheetData.AppendChild(ExcelRow(rowIndex++,
                [
                    p.Id.ToString(),
                    p.Title ?? string.Empty,
                    p.OccupationalSeries ?? string.Empty,
                    grade,
                    rem?.MinimumRange.ToString("N0") ?? string.Empty,
                    rem?.MaximumRange.ToString("N0") ?? string.Empty,
                    p.DutyLocation ?? string.Empty,
                    p.TeleworkEligible ? "Yes" : "No",
                    p.SecurityClearance.ToString(),
                    p.WhoMayApply ?? string.Empty,
                    p.HiringOrganization?.DepartmentName ?? string.Empty,
                    p.HiringOrganization?.OrganizationName ?? string.Empty,
                    p.OpenDate.ToString("yyyy-MM-dd"),
                    p.CloseDate?.ToString("yyyy-MM-dd") ?? string.Empty
                ]));
            }

            workbookPart.Workbook.Save();
        }

        return ms.ToArray();
    }

    private static SS.Row ExcelRow(uint rowIndex, string[] values)
    {
        var row = new SS.Row { RowIndex = rowIndex };
        for (var i = 0; i < values.Length; i++)
        {
            var colLetter = ((char)('A' + i)).ToString();
            var cell = new SS.Cell
            {
                CellReference = $"{colLetter}{rowIndex}",
                DataType = SS.CellValues.String,
                CellValue = new SS.CellValue(values[i])
            };
            row.AppendChild(cell);
        }

        return row;
    }

    private static Table BuildOverviewTable(HrPosition p, PositionRemuneration? rem, string grade, string minSal, string maxSal)
    {
        var table = new Table();
        var tblProps = new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" }
            )
        );
        table.AppendChild(tblProps);

        void Row(string label, string value) => table.AppendChild(OverviewRow(label, value));

        Row("Status", p.IsOpen ? "Accepting applications" : "Closed");
        Row("Open / Closing", $"{p.OpenDate:MM/dd/yyyy} - {p.CloseDate?.ToString("MM/dd/yyyy") ?? "Open until filled"}");
        Row("Salary", $"{minSal} - {maxSal} per year");
        Row("Pay grade", grade);
        Row("Location", p.DutyLocation ?? string.Empty);
        Row("Openings", string.IsNullOrWhiteSpace(p.TotalOpenings) ? "1 vacancy" : $"{p.TotalOpenings} vacancies");
        Row("Remote", p.RemoteEligible ? "Yes" : "No");
        Row("Telework", p.TeleworkEligible ? "Yes - Per Agency Policy" : "No");
        Row("Travel required", p.TravelRequired.ToString());
        Row("Relocation", p.RelocationAuthorized ? "Yes" : "No");
        Row("Appointment type", p.AppointmentType.ToString());
        Row("Work schedule", p.WorkSchedule.ToString());
        Row("Service", p.ServiceType ?? string.Empty);
        Row("Promotion potential", p.PromotionPotential ?? string.Empty);
        Row("Job series", $"{p.OccupationalSeries} {p.OccupationalSeriesTitle}".Trim());
        Row("Supervisory", p.SupervisoryStatus ? "Yes" : "No");
        Row("Security clearance", p.SecurityClearance.ToString());
        Row("Drug test", p.DrugTestRequired ? "Yes" : "No");
        Row("Announcement #", p.AnnouncementNumber ?? string.Empty);
        Row("Control #", p.UsaJobsId ?? string.Empty);

        return table;
    }

    private static TableRow OverviewRow(string label, string value)
    {
        var labelCell = new TableCell(
            new TableCellProperties(new TableCellWidth { Width = "1800", Type = TableWidthUnitValues.Dxa }),
            new Paragraph(new Run(new RunProperties(new Bold()), new Text(label)))
        );
        var valueCell = new TableCell(
            new Paragraph(new Run(new Text(value) { Space = SpaceProcessingModeValues.Preserve }))
        );
        return new TableRow(labelCell, valueCell);
    }

    private static void AppendSection(Body body, string heading, string? text, string styleId = "Heading2")
    {
        body.AppendChild(StyledParagraph(styleId, heading));
        if (string.IsNullOrWhiteSpace(text))
        {
            body.AppendChild(NormalParagraph("N/A"));
            return;
        }

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var t = line.Trim();
            if (!string.IsNullOrWhiteSpace(t))
                body.AppendChild(NormalParagraph(t));
        }
    }

    private static Paragraph StyledParagraph(string styleId, string text)
    {
        return new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = styleId }),
            new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve })
        );
    }

    private static Paragraph NormalParagraph(string text)
    {
        return new Paragraph(
            new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve })
        );
    }

    private static void AddStylesPart(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        var normal = new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true };
        normal.AppendChild(new StyleName { Val = "Normal" });
        var normalRp = new StyleRunProperties();
        normalRp.AppendChild(new FontSize { Val = "22" });
        normal.AppendChild(normalRp);
        styles.AppendChild(normal);

        styles.AppendChild(HeadingStyle("Heading1", "heading 1", "28"));
        styles.AppendChild(HeadingStyle("Heading2", "heading 2", "26"));
        styles.AppendChild(HeadingStyle("Heading3", "heading 3", "24"));

        stylesPart.Styles = styles;
    }

    private static Style HeadingStyle(string styleId, string styleName, string halfPoints)
    {
        var style = new Style { Type = StyleValues.Paragraph, StyleId = styleId };
        style.AppendChild(new StyleName { Val = styleName });
        style.AppendChild(new BasedOn { Val = "Normal" });
        var spp = new StyleParagraphProperties();
        spp.AppendChild(new SpacingBetweenLines { Before = "240", After = "80" });
        style.AppendChild(spp);
        var srp = new StyleRunProperties();
        srp.AppendChild(new Bold());
        srp.AppendChild(new FontSize { Val = halfPoints });
        srp.AppendChild(new FontSizeComplexScript { Val = halfPoints });
        style.AppendChild(srp);
        return style;
    }

    private static string ToBase64Json(string fileName, byte[] bytes)
    {
        return JsonSerializer.Serialize(new { fileName, content = Convert.ToBase64String(bytes) });
    }
}
