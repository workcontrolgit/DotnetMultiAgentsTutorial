// src/Hr.Jobs.Mcp/Tools/JobDescriptionTools.cs
using System.ComponentModel;
using Hr.Application.Services;
using Hr.Core.Entities;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace Hr.Jobs.Mcp.Tools;

[McpServerToolType]
public sealed class JobDescriptionTools(PositionService positions, IChatClient chatClient)
{
    // Minimum character count for a position's Duties+Qualifications to be considered
    // "rich enough" that a same-grade sibling is not needed.
    private const int SparseThreshold = 300;

    [McpServerTool(Name = "WriteJobDescription"),
     Description("Generates a USAJobs-style job announcement for the specified position using AI. Returns a fully written narrative with Summary, Duties, Qualifications, and How to Apply sections.")]
    public async Task<string> WriteJobDescription(
        [Description("The numeric ID of the position to write a description for")] int positionId,
        CancellationToken ct = default)
    {
        var p = await positions.GetPositionByIdAsync(positionId, ct);
        if (p is null) return $"Position {positionId} not found.";

        // Build a grade-ladder context from sibling positions in the same series.
        // At most 3 positions are selected — one per slot — regardless of how many
        // records exist for that series in the database.
        var ladder = await BuildGradeLadderAsync(p, ct);

        var systemPrompt = """
            You are a senior federal HR specialist with 15 years of experience writing USAJobs announcements.
            Your announcements are known for being clear, compelling, and compliant with OPM writing standards.

            Rules you always follow:
            - SYNTHESIZE duties into 5–8 concise active-voice bullet points. Never copy raw text verbatim.
            - Qualifications must state a specific number of years of specialized experience at the next lower grade level (e.g., "one year of specialized experience equivalent to GS-11").
            - Add education alternatives for GS-5 through GS-9 positions (e.g., bachelor's degree substitution).
            - Security clearance and drug test requirements must appear in Qualifications if applicable.
            - How to Apply must mention USAJOBS.gov and include a note about veterans' preference.
            - Use second-person ("You will...") in the Duties section for approachability.
            - Keep the Summary to 3–5 sentences: mission context, role impact, and what makes this opportunity compelling.
            """;

        var userPrompt = $"""
            Write a complete USAJobs-style job announcement for this position:

            Title:              {p.Title}
            Department:         {p.HiringOrganization?.DepartmentName}
            Agency:             {p.HiringOrganization?.OrganizationName}
            Occupational Series:{p.OccupationalSeries}
            Pay Grade:          {p.PayGradeMin}–{p.PayGradeMax}
            Salary:             ${p.PositionRemuneration?.MinimumRange:N0} – ${p.PositionRemuneration?.MaximumRange:N0} per year
            Location:           {p.DutyLocation}
            Telework:           {(p.TeleworkEligible ? "Eligible" : "Not eligible")}
            Who May Apply:      {p.WhoMayApply}
            Security Clearance: {p.SecurityClearance}

            Source description (synthesize, do not copy):
            {p.Description}

            Source duties (synthesize into bullets, do not copy):
            {p.Duties}

            Source qualifications (use as a baseline; add grade-level experience statement):
            {p.Qualifications}

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

    // ── Grade-ladder helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Fetches all positions in the same occupational series, then selects at most
    /// 3 representatives (next-lower grade, next-higher grade, same-grade sibling)
    /// to use as grade-ladder context in the prompt.
    ///
    /// Selection rule: among all positions at a given grade level, pick the one with
    /// the longest combined Duties+Qualifications text — the richest record wins.
    /// </summary>
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

        // Group by grade number; within each grade pick the richest record
        var byGrade = siblings
            .GroupBy(p => ParseGradeNumber(p.PayGradeMin))
            .Where(g => g.Key is not null)
            .ToDictionary(
                g => g.Key!.Value,
                g => g.OrderByDescending(p => Richness(p)).First());

        var sections = new List<string>();

        // Slot 1: next lower grade — used for the "specialized experience equiv. to GS-N" statement
        var lowerKeys = byGrade.Keys.Where(g => g < targetGrade).ToList();
        var lowerGrade = lowerKeys.Count > 0 ? lowerKeys.Max() : 0;
        if (lowerGrade > 0 && byGrade.TryGetValue(lowerGrade, out var lower))
            sections.Add(FormatLadderEntry("NEXT LOWER GRADE (experience baseline for qualifications)", lower));

        // Slot 2: next higher grade — used to avoid overstating scope
        var higherKeys = byGrade.Keys.Where(g => g > targetGrade).ToList();
        var higherGrade = higherKeys.Count > 0 ? higherKeys.Min() : 0;
        if (higherGrade > 0 && byGrade.TryGetValue(higherGrade, out var higher))
            sections.Add(FormatLadderEntry("NEXT HIGHER GRADE (scope ceiling — do not exceed)", higher));

        // Slot 3: same-grade sibling — only when target's own text is sparse
        var isTargetSparse = Richness(target) < SparseThreshold;
        if (isTargetSparse && byGrade.TryGetValue(targetGrade.Value, out var peer))
            sections.Add(FormatLadderEntry("SAME GRADE PEER (supplement sparse duties)", peer));

        if (sections.Count == 0)
            return string.Empty;

        return $"""
            --- GRADE LADDER CONTEXT (for reference only — do not copy verbatim) ---
            The following real positions from the same occupational series ({target.OccupationalSeries})
            are provided to help you write accurate grade-level qualifications and appropriately
            scope the duties. Use them as reference; synthesize, do not quote.

            {string.Join("\n\n", sections)}
            --- END GRADE LADDER CONTEXT ---
            """;
    }

    private static string FormatLadderEntry(string slot, Position p) =>
        $"""
        [{slot}]
        Title:          {p.Title}
        Grade:          {p.PayGradeMin}–{p.PayGradeMax}
        Duties:         {Truncate(p.Duties, 600)}
        Qualifications: {Truncate(p.Qualifications, 400)}
        """;

    /// <summary>Richness score: total character count of Duties + Qualifications.</summary>
    private static int Richness(Position p) =>
        (p.Duties?.Length ?? 0) + (p.Qualifications?.Length ?? 0);

    /// <summary>Parses the numeric part from a grade string like "GS-12" or "GS-09".</summary>
    private static int? ParseGradeNumber(string? grade)
    {
        if (string.IsNullOrWhiteSpace(grade)) return null;
        var parts = grade.Split('-');
        return parts.Length == 2 && int.TryParse(parts[1], out var n) ? n : null;
    }

    private static string Truncate(string? text, int maxChars) =>
        string.IsNullOrWhiteSpace(text) ? "(none)" :
        text.Length <= maxChars ? text :
        text[..maxChars] + "…";
}
