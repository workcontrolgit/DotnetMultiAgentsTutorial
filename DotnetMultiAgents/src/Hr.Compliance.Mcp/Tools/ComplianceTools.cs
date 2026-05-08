// Tools/ComplianceTools.cs
using System.ComponentModel;
using Hr.Application.Services;
using Hr.Compliance.Mcp.Rules;
using ModelContextProtocol.Server;

namespace Hr.Compliance.Mcp.Tools;

[McpServerToolType]
public sealed class ComplianceTools(
    PositionService positionService,
    OpmRuleEngine ruleEngine,
    OpmStandardsRepository standards)
{
    // ── Full compliance check ────────────────────────────────────────────────

    [McpServerTool(Name = "RunFullComplianceCheck"),
     Description(
         "Runs all OPM compliance rules against a position and returns a structured report. " +
         "Reports each rule as Pass, Warning, or Fail with an explanation. " +
         "Use this as the primary compliance tool before submitting a job announcement.")]
    public async Task<object> RunFullComplianceCheck(
        [Description("The numeric ID of the position to check")] int positionId,
        CancellationToken ct = default)
    {
        var position = await positionService.GetPositionByIdAsync(positionId, ct);
        if (position is null)
            return new { error = $"Position {positionId} not found." };

        var report = ruleEngine.RunAll(position);

        return new
        {
            positionId       = report.PositionId,
            positionTitle    = report.PositionTitle,
            series           = report.OccupationalSeries,
            overallStatus    = report.OverallStatus.ToString(),
            passed           = report.Passes.Count(),
            warnings         = report.Warnings.Count(),
            failed           = report.Failures.Count(),
            results          = report.Results.Select(r => new
            {
                rule    = r.RuleName,
                status  = r.Status.ToString(),
                message = r.Message,
            }),
        };
    }

    // ── Standalone rule tools (callable without a position ID) ───────────────

    [McpServerTool(Name = "ValidatePayGrade"),
     Description(
         "Checks whether a pay grade range is valid for a given OPM occupational series. " +
         "Verifies that grades follow GS-NN format, that min ≤ max, and that both fall within " +
         "the OPM-allowed grade range for the series. " +
         "Example: series '2210', gradeMin 'GS-09', gradeMax 'GS-11'.")]
    public object ValidatePayGrade(
        [Description("OPM occupational series code, e.g. '2210' or '0201'")] string occupationalSeries,
        [Description("Minimum pay grade in GS-NN format, e.g. 'GS-09'")] string gradeMin,
        [Description("Maximum pay grade in GS-NN format, e.g. 'GS-11'")] string gradeMax)
    {
        var standard = standards.GetBySeries(occupationalSeries);

        // Build a lightweight position stub for the rule engine
        var stub = new Hr.Core.Entities.Position
        {
            OccupationalSeries = occupationalSeries,
            PayGradeMin        = gradeMin,
            PayGradeMax        = gradeMax,
        };

        var rangeResult     = ruleEngine.CheckPayGrade(stub);
        var alignmentResult = ruleEngine.CheckPayGradeAlignment(stub);

        return new
        {
            series              = occupationalSeries,
            seriesTitle         = standard?.SeriesTitle ?? "Unknown series",
            gradeMin,
            gradeMax,
            rangeCheck          = FormatResult(rangeResult),
            alignmentCheck      = FormatResult(alignmentResult),
            qualificationStandardUrl = standard?.QualificationStandardUrl,
        };
    }

    [McpServerTool(Name = "CheckApplicationPeriod"),
     Description(
         "Checks whether an open job announcement meets the OPM minimum posting period of 5 business days. " +
         "Pass dates in ISO 8601 format (yyyy-MM-dd). Returns Pass or Fail with the business-day count.")]
    public object CheckApplicationPeriod(
        [Description("Announcement open date in yyyy-MM-dd format")] string openDate,
        [Description("Announcement close date in yyyy-MM-dd format")] string closeDate)
    {
        if (!DateTime.TryParse(openDate, out var open) ||
            !DateTime.TryParse(closeDate, out var close))
            return new { status = "Fail", message = "Invalid date format. Use yyyy-MM-dd." };

        var stub = new Hr.Core.Entities.Position
        {
            IsOpen    = true,
            OpenDate  = open,
            CloseDate = close,
        };

        var result = ruleEngine.CheckApplicationPeriod(stub);
        return FormatResult(result);
    }

    [McpServerTool(Name = "GetOPMStandard"),
     Description(
         "Returns the OPM Qualification Standard for a given occupational series, including " +
         "the allowed GS grade range and the official standard URL. " +
         "Use this to look up requirements before drafting a job announcement.")]
    public object GetOPMStandard(
        [Description("OPM occupational series code, e.g. '2210' or '0201'")] string occupationalSeries)
    {
        var standard = standards.GetBySeries(occupationalSeries);
        if (standard is null)
            return new
            {
                found  = false,
                series = occupationalSeries,
                message = $"No OPM standard found for series '{occupationalSeries}'. " +
                          $"Visit https://www.opm.gov/policy-data-oversight/classification-qualifications/ to look it up.",
            };

        return new
        {
            found                    = true,
            series                   = standard.OccupationalSeries,
            seriesTitle              = standard.SeriesTitle,
            allowedGrades            = standard.AllowedGradeNumbers.Select(g => $"GS-{g:D2}").ToList(),
            qualificationStandardUrl = standard.QualificationStandardUrl,
        };
    }

    [McpServerTool(Name = "ListOPMSeries"),
     Description("Returns all OPM occupational series known to the compliance server with their titles and grade ranges.")]
    public object ListOPMSeries()
    {
        return standards.GetAll().Select(s => new
        {
            series      = s.OccupationalSeries,
            title       = s.SeriesTitle,
            gradeRange  = $"GS-{s.AllowedGradeNumbers.First():D2} to GS-{s.AllowedGradeNumbers.Last():D2}",
            standardUrl = s.QualificationStandardUrl,
        });
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static object FormatResult(ComplianceResult r) =>
        new { rule = r.RuleName, status = r.Status.ToString(), message = r.Message };
}
