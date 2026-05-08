// Rules/OpmRuleEngine.cs
using Hr.Core.Entities;
using Hr.Core.Enums;

namespace Hr.Compliance.Mcp.Rules;

/// <summary>
/// Deterministic OPM compliance rule engine.
/// Each public method is one named rule that returns a <see cref="ComplianceResult"/>.
/// Rules contain zero LLM calls — all decisions are made in C# code.
/// </summary>
public sealed class OpmRuleEngine(OpmStandardsRepository standards)
{
    private const int MinAnnouncementBusinessDays = 5;

    // ── Public entry point ───────────────────────────────────────────────────

    /// <summary>Runs all rules against the position and returns a full report.</summary>
    public ComplianceReport RunAll(Position position)
    {
        var results = new List<ComplianceResult>
        {
            CheckRequiredFields(position),
            CheckPayGrade(position),
            CheckPayGradeAlignment(position),
            CheckApplicationPeriod(position),
            CheckQualificationsText(position),
            CheckSecurityClearanceDisclosure(position),
            CheckWhoMayApply(position),
        };

        var overall = results.Any(r => r.Status == ComplianceStatus.Fail)
            ? ComplianceStatus.Fail
            : results.Any(r => r.Status == ComplianceStatus.Warning)
                ? ComplianceStatus.Warning
                : ComplianceStatus.Pass;

        return new ComplianceReport(
            position.Id,
            position.Title,
            position.OccupationalSeries,
            overall,
            results);
    }

    // ── Individual rules (also callable as standalone MCP tools) ────────────

    /// <summary>
    /// Rule: All mandatory fields must be non-empty.
    /// </summary>
    public ComplianceResult CheckRequiredFields(Position p)
    {
        const string rule = "RequiredFields";

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(p.Title))              missing.Add("Title");
        if (string.IsNullOrWhiteSpace(p.OccupationalSeries)) missing.Add("OccupationalSeries");
        if (string.IsNullOrWhiteSpace(p.PayGradeMin))        missing.Add("PayGradeMin");
        if (string.IsNullOrWhiteSpace(p.PayGradeMax))        missing.Add("PayGradeMax");
        if (string.IsNullOrWhiteSpace(p.DutyLocation))       missing.Add("DutyLocation");
        if (string.IsNullOrWhiteSpace(p.WhoMayApply))        missing.Add("WhoMayApply");
        if (string.IsNullOrWhiteSpace(p.Duties))             missing.Add("Duties");
        if (string.IsNullOrWhiteSpace(p.Qualifications))     missing.Add("Qualifications");

        return missing.Count == 0
            ? ComplianceResult.Pass(rule)
            : ComplianceResult.Fail(rule,
                $"Missing required fields: {string.Join(", ", missing)}.");
    }

    /// <summary>
    /// Rule: PayGradeMin must be ≤ PayGradeMax and both must be valid GS grades.
    /// </summary>
    public ComplianceResult CheckPayGrade(Position p)
    {
        const string rule = "PayGradeRange";

        if (!TryParseGsGrade(p.PayGradeMin, out var min))
            return ComplianceResult.Fail(rule,
                $"PayGradeMin '{p.PayGradeMin}' is not a valid GS grade (expected format: GS-NN).");

        if (!TryParseGsGrade(p.PayGradeMax, out var max))
            return ComplianceResult.Fail(rule,
                $"PayGradeMax '{p.PayGradeMax}' is not a valid GS grade (expected format: GS-NN).");

        if (min > max)
            return ComplianceResult.Fail(rule,
                $"PayGradeMin ({p.PayGradeMin}) must be ≤ PayGradeMax ({p.PayGradeMax}).");

        return ComplianceResult.Pass(rule);
    }

    /// <summary>
    /// Rule: Pay grade range must fall within the OPM-allowed grades for the occupational series.
    /// </summary>
    public ComplianceResult CheckPayGradeAlignment(Position p)
    {
        const string rule = "PayGradeAlignment";

        var standard = standards.GetBySeries(p.OccupationalSeries);
        if (standard is null)
            return ComplianceResult.Warn(rule,
                $"No OPM standard found for series '{p.OccupationalSeries}'. Cannot validate pay grade alignment.");

        if (!TryParseGsGrade(p.PayGradeMin, out var min) ||
            !TryParseGsGrade(p.PayGradeMax, out var max))
            return ComplianceResult.Warn(rule, "Pay grade format invalid; alignment check skipped.");

        var invalidGrades = new List<string>();
        if (!standard.AllowedGradeNumbers.Contains(min))
            invalidGrades.Add(p.PayGradeMin);
        if (!standard.AllowedGradeNumbers.Contains(max) && max != min)
            invalidGrades.Add(p.PayGradeMax);

        if (invalidGrades.Count > 0)
            return ComplianceResult.Fail(rule,
                $"Grade(s) {string.Join(", ", invalidGrades)} are outside the allowed range for series " +
                $"{p.OccupationalSeries} ({standard.SeriesTitle}). " +
                $"Allowed: GS-{standard.AllowedGradeNumbers.First():D2} to GS-{standard.AllowedGradeNumbers.Last():D2}. " +
                $"Standard: {standard.QualificationStandardUrl}");

        return ComplianceResult.Pass(rule);
    }

    /// <summary>
    /// Rule: Open positions must have an announcement period of at least 5 business days.
    /// </summary>
    public ComplianceResult CheckApplicationPeriod(Position p)
    {
        const string rule = "ApplicationPeriod";

        if (!p.IsOpen || p.CloseDate is null)
            return ComplianceResult.Pass(rule); // closed or no close date — not applicable

        var businessDays = CountBusinessDays(p.OpenDate, p.CloseDate.Value);
        if (businessDays < MinAnnouncementBusinessDays)
            return ComplianceResult.Fail(rule,
                $"Open announcement must be posted for at least {MinAnnouncementBusinessDays} business days. " +
                $"Current period: {businessDays} business day(s) " +
                $"({p.OpenDate:yyyy-MM-dd} to {p.CloseDate.Value:yyyy-MM-dd}).");

        return ComplianceResult.Pass(rule);
    }

    /// <summary>
    /// Rule: Qualifications text must reference the pay grade level explicitly (e.g. "GS-09").
    /// </summary>
    public ComplianceResult CheckQualificationsText(Position p)
    {
        const string rule = "QualificationsGradeReference";

        if (string.IsNullOrWhiteSpace(p.Qualifications))
            return ComplianceResult.Fail(rule, "Qualifications field is empty.");

        // At least one of the advertised grades should appear in the text
        var gradesMentioned = new[] { p.PayGradeMin, p.PayGradeMax }
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Any(g => p.Qualifications.Contains(g, StringComparison.OrdinalIgnoreCase));

        if (!gradesMentioned)
            return ComplianceResult.Warn(rule,
                $"Qualifications text does not explicitly reference the advertised grade(s) " +
                $"({p.PayGradeMin}–{p.PayGradeMax}). OPM standard qualifications text " +
                $"must specify the grade level for which experience is required.");

        return ComplianceResult.Pass(rule);
    }

    /// <summary>
    /// Rule: If a security clearance is required, it must be stated in Duties or Qualifications.
    /// </summary>
    public ComplianceResult CheckSecurityClearanceDisclosure(Position p)
    {
        const string rule = "SecurityClearanceDisclosure";

        if (p.SecurityClearance == SecurityClearance.NotRequired)
            return ComplianceResult.Pass(rule);

        var clearanceName = p.SecurityClearance.ToString();
        var searchText    = $"{p.Duties} {p.Qualifications}";

        // Accept common variations: "Top Secret", "Secret", "Public Trust" etc.
        var mentioned = searchText.Contains(clearanceName, StringComparison.OrdinalIgnoreCase)
            || searchText.Contains("clearance",       StringComparison.OrdinalIgnoreCase)
            || searchText.Contains("public trust",    StringComparison.OrdinalIgnoreCase)
            || searchText.Contains("secret",          StringComparison.OrdinalIgnoreCase);

        return mentioned
            ? ComplianceResult.Pass(rule)
            : ComplianceResult.Fail(rule,
                $"Position requires {clearanceName} clearance but this is not mentioned " +
                $"in Duties or Qualifications. Security requirements must be disclosed.");
    }

    /// <summary>
    /// Rule: WhoMayApply must be one of the recognized federal applicant pool categories.
    /// </summary>
    public ComplianceResult CheckWhoMayApply(Position p)
    {
        const string rule = "WhoMayApply";

        if (string.IsNullOrWhiteSpace(p.WhoMayApply))
            return ComplianceResult.Fail(rule, "WhoMayApply field is empty.");

        // Recognized federal categories (case-insensitive substring match)
        string[] recognized =
        [
            "us citizens",
            "united states citizens",
            "current federal employees",
            "federal employees",
            "status candidates",
            "merit promotion",
            "all sources",
            "open to the public",
            "veterans",
        ];

        var matched = recognized.Any(k =>
            p.WhoMayApply.Contains(k, StringComparison.OrdinalIgnoreCase));

        return matched
            ? ComplianceResult.Pass(rule)
            : ComplianceResult.Warn(rule,
                $"WhoMayApply value '{p.WhoMayApply}' does not match a recognized federal applicant " +
                $"pool category. Verify it meets USAJobs announcement standards.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool TryParseGsGrade(string? grade, out int number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(grade)) return false;
        var parts = grade.Split('-');
        return parts.Length == 2 && int.TryParse(parts[1], out number);
    }

    private static int CountBusinessDays(DateTime start, DateTime end)
    {
        var days = 0;
        var current = start.Date;
        while (current <= end.Date)
        {
            if (current.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                days++;
            current = current.AddDays(1);
        }
        return days;
    }
}
