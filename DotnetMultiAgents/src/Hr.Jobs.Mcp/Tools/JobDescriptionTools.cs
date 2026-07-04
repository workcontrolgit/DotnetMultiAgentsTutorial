// src/Hr.Jobs.Mcp/Tools/JobDescriptionTools.cs
using System.ComponentModel;
using System.Text;
using Hr.Application.Services;
using Hr.Core.Entities;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace Hr.Jobs.Mcp.Tools;

[McpServerToolType]
public sealed class JobDescriptionTools(PositionService positions, IChatClient chatClient)
{
    // Minimum character count for a position's Duties+Qualifications to be considered
    // rich enough that a same-grade sibling is not needed.
    private const int SparseThreshold = 300;

    [McpServerTool(Name = "WriteJobDescription"),
     Description("Generates a USAJobs-style job announcement for the specified position using AI. Returns a fully written narrative with Summary, Duties, Qualifications, and How to Apply sections.")]
    public async Task<string> WriteJobDescription(
        [Description("The numeric ID of the position to write a description for")] int positionId,
        CancellationToken ct = default)
    {
        var p = await positions.GetPositionByIdAsync(positionId, ct);
        if (p is null) return $"Position {positionId} not found.";

        var ladder = await BuildGradeLadderAsync(p, ct);
        var supplementalContext = BuildSupplementalContext(p);

        var systemPrompt = """
            You are a senior federal HR specialist with 15 years of experience writing USAJobs announcements.
            Your announcements are clear, compelling, and compliant with OPM writing standards.

            Rules you always follow:
            - Synthesize duties into 5-8 concise active-voice bullet points. Never copy raw text verbatim.
            - Qualifications must state a specific amount of specialized experience at the next lower grade level when the grade requires it.
            - Include education substitution only when the source material supports it or the grade range reasonably suggests it.
            - Mention security clearance, drug testing, travel, relocation, telework/remote eligibility, and supervisory expectations when applicable.
            - Use conditions of employment, required documents, and how-to-apply details when provided instead of inventing them.
            - How to Apply must mention USAJOBS.gov and include a note about veterans' preference unless the source explicitly conflicts.
            - Use second-person voice in the Duties section where natural.
            - Keep the Summary to 3-5 sentences covering mission, role impact, and why the opportunity matters.
            - Do not fabricate benefits, incentives, or requirements that are not grounded in the source context.
            """;

        var userPrompt = $"""
            Write a complete USAJobs-style job announcement for this position.

            Core position facts:
            Title:               {p.Title}
            Announcement Number: {ValueOrNone(p.AnnouncementNumber)}
            USAJobs Position ID: {ValueOrNone(p.UsaJobsId)}
            Department:          {p.HiringOrganization?.DepartmentName}
            Agency:              {p.HiringOrganization?.OrganizationName}
            Sub-Agency:          {ValueOrNone(p.SubAgencyName)}
            Occupational Series: {p.OccupationalSeries}
            Series Title:        {ValueOrNone(p.OccupationalSeriesTitle)}
            Pay Grade:           {p.PayGradeMin}-{p.PayGradeMax}
            Salary:              ${p.PositionRemuneration?.MinimumRange:N0} - ${p.PositionRemuneration?.MaximumRange:N0} per year
            Appointment Type:    {p.AppointmentType}
            Service Type:        {ValueOrNone(p.ServiceType)}
            Offering Type:       {ValueOrNone(p.PositionOfferingType)}
            Work Schedule:       {p.WorkSchedule}
            Open Date:           {p.OpenDate:yyyy-MM-dd}
            Close Date:          {p.CloseDate:yyyy-MM-dd}
            Hiring Path:         {ValueOrNone(p.HiringPath)}
            Who May Apply:       {p.WhoMayApply}
            Location:            {FormatLocation(p)}
            Telework:            {(p.TeleworkEligible ? "Eligible" : "Not eligible")}
            Remote:              {(p.RemoteEligible ? "Eligible" : "Not eligible")}
            Travel Required:     {p.TravelRequired}
            Security Clearance:  {p.SecurityClearance}
            Supervisory:         {(p.SupervisoryStatus ? "Yes" : "No")}
            Relocation:          {(p.RelocationAuthorized ? "Authorized" : "Not authorized")}
            Drug Test:           {(p.DrugTestRequired ? "Required" : "Not required")}
            Financial Disclosure:{(p.FinancialDisclosure ? "Required" : "Not required")}
            Position Sensitivity:{ValueOrNone(p.PositionSensitivityAndRisk)}
            Total Openings:      {ValueOrNone(p.TotalOpenings)}

            Source description (synthesize, do not copy):
            {p.Description}

            Source duties (synthesize into bullets, do not copy):
            {p.Duties}

            Source qualifications baseline:
            {p.Qualifications}

            Additional source context:
            {supplementalContext}

            {ladder}

            Format the announcement with exactly these markdown sections:
            ## Summary
            ## Duties
            ## Qualifications Required
            ## How to Apply
            """;

        var response = await chatClient.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userPrompt),
            ],
            cancellationToken: ct);

        return response.Text ?? $"Unable to generate description for position {positionId}.";
    }

    private static string BuildSupplementalContext(Position p)
    {
        var sb = new StringBuilder();

        AppendSection(sb, "Education", p.Education);
        AppendSection(sb, "Evaluations", p.Evaluations);
        AppendSection(sb, "Conditions of Employment", p.ConditionsOfEmployment);
        AppendSection(sb, "Required Documents", p.RequiredDocuments);
        AppendSection(sb, "How to Apply Source Notes", p.HowToApply);
        AppendSection(sb, "Next Steps", p.NextSteps);
        AppendSection(sb, "Additional Information", p.AdditionalInformation);
        AppendSection(sb, "Promotion Potential", p.PromotionPotential);
        AppendSection(sb, "Adjudication Type", p.AdjudicationType);
        AppendSection(sb, "Contact", FormatContact(p));
        AppendSection(sb, "Position URL", p.PositionUri);
        AppendSection(sb, "Apply URL", p.ApplyUri);

        if (!string.IsNullOrWhiteSpace(p.KeyRequirements))
        {
            sb.AppendLine("Key Requirements:");
            foreach (var line in SplitLines(p.KeyRequirements))
                sb.AppendLine($"- {line}");
        }

        return sb.Length == 0 ? "(none)" : sb.ToString().TrimEnd();
    }

    private static void AppendSection(StringBuilder sb, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        sb.AppendLine($"{label}:");
        sb.AppendLine(value.Trim());
        sb.AppendLine();
    }

    private static IEnumerable<string> SplitLines(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private async Task<string> BuildGradeLadderAsync(Position target, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(target.OccupationalSeries))
            return string.Empty;

        var targetGrade = ParseGradeNumber(target.PayGradeMin);
        if (targetGrade is null)
            return string.Empty;

        var siblings = (await positions.GetPositionsBySeriesAsync(target.OccupationalSeries, ct))
            .Where(p => p.Id != target.Id && !string.IsNullOrWhiteSpace(p.PayGradeMin))
            .ToList();

        if (siblings.Count == 0)
            return string.Empty;

        var byGrade = siblings
            .GroupBy(p => ParseGradeNumber(p.PayGradeMin))
            .Where(g => g.Key is not null)
            .ToDictionary(
                g => g.Key!.Value,
                g => g.OrderByDescending(Richness).First());

        var sections = new List<string>();

        var lowerKeys = byGrade.Keys.Where(g => g < targetGrade).ToList();
        var lowerGrade = lowerKeys.Count > 0 ? lowerKeys.Max() : 0;
        if (lowerGrade > 0 && byGrade.TryGetValue(lowerGrade, out var lower))
            sections.Add(FormatLadderEntry("NEXT LOWER GRADE (experience baseline for qualifications)", lower));

        var higherKeys = byGrade.Keys.Where(g => g > targetGrade).ToList();
        var higherGrade = higherKeys.Count > 0 ? higherKeys.Min() : 0;
        if (higherGrade > 0 && byGrade.TryGetValue(higherGrade, out var higher))
            sections.Add(FormatLadderEntry("NEXT HIGHER GRADE (scope ceiling; do not exceed)", higher));

        var isTargetSparse = Richness(target) < SparseThreshold;
        if (isTargetSparse && byGrade.TryGetValue(targetGrade.Value, out var peer))
            sections.Add(FormatLadderEntry("SAME GRADE PEER (supplement sparse duties)", peer));

        if (sections.Count == 0)
            return string.Empty;

        return $"""
            --- GRADE LADDER CONTEXT (for reference only; do not copy verbatim) ---
            The following real positions from the same occupational series ({target.OccupationalSeries})
            are provided to help write accurate grade-level qualifications and appropriately
            scope the duties. Use them as reference and synthesize them.

            {string.Join("\n\n", sections)}
            --- END GRADE LADDER CONTEXT ---
            """;
    }

    private static string FormatLadderEntry(string slot, Position p) =>
        $"""
        [{slot}]
        Title:              {p.Title}
        Series:             {p.OccupationalSeries} {ValueOrNone(p.OccupationalSeriesTitle)}
        Grade:              {p.PayGradeMin}-{p.PayGradeMax}
        Duties:             {Truncate(p.Duties, 600)}
        Qualifications:     {Truncate(p.Qualifications, 400)}
        Education:          {Truncate(p.Education, 200)}
        Conditions:         {Truncate(p.ConditionsOfEmployment, 200)}
        """;

    private static int Richness(Position p) =>
        (p.Duties?.Length ?? 0)
        + (p.Qualifications?.Length ?? 0)
        + (p.Education?.Length ?? 0)
        + (p.ConditionsOfEmployment?.Length ?? 0)
        + (p.HowToApply?.Length ?? 0);

    private static int? ParseGradeNumber(string? grade)
    {
        if (string.IsNullOrWhiteSpace(grade)) return null;
        var parts = grade.Split('-');
        return parts.Length == 2 && int.TryParse(parts[1], out var n) ? n : null;
    }

    private static string Truncate(string? text, int maxChars) =>
        string.IsNullOrWhiteSpace(text) ? "(none)" :
        text.Length <= maxChars ? text :
        text[..maxChars] + "...";

    private static string ValueOrNone(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "(none)" : value;

    private static string FormatLocation(Position p)
    {
        if (!string.IsNullOrWhiteSpace(p.DutyLocation) && !string.IsNullOrWhiteSpace(p.DutyLocationState))
            return $"{p.DutyLocation}, {p.DutyLocationState}";
        return ValueOrNone(p.DutyLocation);
    }

    private static string FormatContact(Position p)
    {
        var parts = new[]
        {
            p.ContactName,
            p.ContactPhone,
            p.ContactEmail,
            p.ContactAddress
        }.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

        return parts.Count == 0 ? string.Empty : string.Join(" | ", parts);
    }
}
