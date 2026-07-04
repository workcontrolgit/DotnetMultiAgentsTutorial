# Part 4 — The Compliance MCP Server: Deterministic Rules, Zero LLM

*[Building Multi-Agent Systems with .NET 10 Blog Series](preface-why-one-agent-is-not-enough.md)*

---

Part 3 built `Hr.Jobs.Mcp` — nine tools that agents use to search positions, generate job descriptions, and list hiring organizations. Tools that create content are only useful if that content meets regulatory standards. This part adds the second server, `Hr.Compliance.Mcp` — a rule engine that checks OPM compliance in deterministic C# with zero LLM calls.

The most important architectural decision in the system is not which model to use or how to structure the agents. It is deciding where the LLM should not be involved at all.

The OPM compliance server (`Hr.Compliance.Mcp`) has zero LLM calls. Every compliance decision — pass, warning, fail — is made by deterministic C# code. The language model appears only in the orchestrator layer, where the OPMCompliance specialist agent reads the compliance results and explains them to the user in plain language.

This post explains why that separation matters, how the rule engine is built, and how to test every rule with MCP Inspector before writing orchestrator code.

---

## Why Deterministic Rules, Not LLM Judgment

Consider the alternative: you ask an LLM "does this position's pay grade comply with OPM standards?" The model might say "the grade range appears reasonable for an IT Management position." It might also say "GS-16 seems high but could be justified by the role's scope." Both answers sound plausible. Neither is correct — there is no GS-16, and the model has no reliable knowledge of current OPM qualification standards.

OPM compliance is binary. Either the grade is in the allowed range for the series or it is not. Either the announcement period is at least 5 business days or it is not. Either the qualifications text references the advertised grade level or it does not. There is no "could be justified" — these are regulatory requirements.

The rule of thumb for this type of decision: **if a lawyer or auditor could evaluate it from a checklist, make it deterministic C# code.** Reserve the LLM for tasks that require language understanding, synthesis, or judgment — explaining what is wrong and how to fix it.

---

## The OpmRuleEngine: 7 Rules, Zero Dependencies

`OpmRuleEngine` has one dependency: `OpmStandardsRepository`. No `IChatClient`. No HTTP client. No EF Core — the compliance server looks up positions from the shared database via its own repository, but the rule engine itself only receives a `Position` object and evaluates it.

```csharp
// src/Hr.Compliance.Mcp/Rules/OpmRuleEngine.cs
public sealed class OpmRuleEngine(OpmStandardsRepository standards)
{
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

        var overall = results.Any(r => r.Status == ComplianceStatus.Fail) ? ComplianceStatus.Fail
            : results.Any(r => r.Status == ComplianceStatus.Warning)      ? ComplianceStatus.Warning
            : ComplianceStatus.Pass;

        return new ComplianceReport(position.Id, position.Title,
            position.OccupationalSeries, overall, results);
    }
}
```

The overall status is the worst result across all rules. One failure makes the report a failure.

---

## The 7 Rules Explained

**Rule 1 — RequiredFields**

Eight mandatory fields must be non-empty: Title, OccupationalSeries, PayGradeMin, PayGradeMax, DutyLocation, WhoMayApply, Duties, Qualifications. If any are missing, the position cannot be meaningfully evaluated by the other rules.

```csharp
public ComplianceResult CheckRequiredFields(Position p)
{
    var missing = new List<string>();
    if (string.IsNullOrWhiteSpace(p.Title))              missing.Add("Title");
    if (string.IsNullOrWhiteSpace(p.OccupationalSeries)) missing.Add("OccupationalSeries");
    // ... 6 more fields

    return missing.Count == 0
        ? ComplianceResult.Pass("RequiredFields")
        : ComplianceResult.Fail("RequiredFields",
            $"Missing required fields: {string.Join(", ", missing)}.");
}
```

**Rule 2 — PayGradeRange**

Both grades must parse as `PLAN-NN` format (e.g., `GS-12`) and `PayGradeMin` must be ≤ `PayGradeMax`.

**Rule 3 — PayGradeAlignment** (the occupational series check)

This is the most technically interesting rule. It calls `OpmStandardsRepository.GetBySeries()` to retrieve the OPM qualification standard for the position's series, then checks that both grades fall within the allowed range.

```csharp
public ComplianceResult CheckPayGradeAlignment(Position p)
{
    var standard = standards.GetBySeries(p.OccupationalSeries);
    if (standard is null)
        return ComplianceResult.Warn("PayGradeAlignment",
            $"No OPM standard found for series '{p.OccupationalSeries}'.");

    var invalidGrades = new List<string>();
    if (!standard.AllowedGradeNumbers.Contains(min)) invalidGrades.Add(p.PayGradeMin);
    if (!standard.AllowedGradeNumbers.Contains(max)) invalidGrades.Add(p.PayGradeMax);

    if (invalidGrades.Count > 0)
        return ComplianceResult.Fail("PayGradeAlignment",
            $"Grade(s) {string.Join(", ", invalidGrades)} are outside the allowed range " +
            $"for series {p.OccupationalSeries} ({standard.SeriesTitle}). " +
            $"Allowed: GS-{standard.AllowedGradeNumbers.First():D2} " +
            $"to GS-{standard.AllowedGradeNumbers.Last():D2}. " +
            $"Standard: {standard.QualificationStandardUrl}");

    return ComplianceResult.Pass("PayGradeAlignment");
}
```

An IT Specialist (series 2210) posted at GS-16 fails because the series allows only GS-05 through GS-15. The failure message includes the OPM standard URL — the HR specialist can click it to read the exact requirement.

**Rule 4 — ApplicationPeriod**

Open positions must have at least 5 business days between open date and close date (excluding weekends). The `CountBusinessDays` helper iterates calendar days, skipping Saturday and Sunday.

**Rule 5 — QualificationsGradeReference**

The qualifications text must explicitly mention the advertised grade level (e.g., "GS-12"). OPM qualification standards are written grade-by-grade; if the text does not reference a specific grade, it cannot be OPM-compliant by definition.

**Rule 6 — SecurityClearanceDisclosure**

If a clearance is required (anything other than `NotRequired`), the duties or qualifications text must mention it. A position that requires Secret clearance but does not disclose this in the announcement creates legal exposure.

**Rule 7 — WhoMayApply**

The applicant pool must match one of the recognized federal categories: "US Citizens", "Current Federal Employees", "Status Candidates", "Merit Promotion", "All Sources", "Open to the Public", or "Veterans". A substring match is used so "Open to all US Citizens" passes even though it is not an exact match.

---

## OpmStandardsRepository: Static Reference Data for 8 Series

The repository is a dictionary of `OpmStandard` value objects, one per series:

```csharp
private static readonly Dictionary<string, OpmStandard> Standards = new(
    StringComparer.OrdinalIgnoreCase)
{
    ["2210"] = new(
        OccupationalSeries:           "2210",
        SeriesTitle:                  "Information Technology Management",
        AllowedGradeNumbers:          Gs5To15,
        QualificationStandardUrl:     "https://www.opm.gov/.../information-technology-.../",
        RequiredQualificationKeyword: "information technology"),

    ["0018"] = new(
        OccupationalSeries:           "0018",
        SeriesTitle:                  "Safety and Occupational Health Management",
        AllowedGradeNumbers:          Gs5To12,   // narrower range
        QualificationStandardUrl:     "https://www.opm.gov/.../safety-.../",
        RequiredQualificationKeyword: "safety"),
    // ... 6 more series
};
```

Seven of the eight series allow GS-05 through GS-15 (`Gs5To15`). Safety (0018) allows only GS-05 through GS-12. The `RequiredQualificationKeyword` is stored as reference data and returned by the `GetOPMStandard` tool — a future Rule 8 could validate that the keyword appears in the qualifications text.

The `GetBySeries` lookup normalizes the series code:

```csharp
public OpmStandard? GetBySeries(string occupationalSeries) =>
    Standards.TryGetValue(
        occupationalSeries.TrimStart('0').PadLeft(4, '0'), out var standard)
        ? standard : null;
```

`"201"`, `"0201"`, and `" 0201 "` all resolve to the HR Management series.

---

## ComplianceResult: A Value Object Pipeline

Each rule returns a `ComplianceResult` — an immutable value object with three factory methods:

```csharp
public record ComplianceResult(string RuleName, ComplianceStatus Status, string Message)
{
    public static ComplianceResult Pass(string rule) =>
        new(rule, ComplianceStatus.Pass, "Passed.");

    public static ComplianceResult Warn(string rule, string message) =>
        new(rule, ComplianceStatus.Warning, message);

    public static ComplianceResult Fail(string rule, string message) =>
        new(rule, ComplianceStatus.Fail, message);
}
```

`ComplianceReport` aggregates the results and computes the overall status:

```csharp
public record ComplianceReport(
    int PositionId, string PositionTitle, string OccupationalSeries,
    ComplianceStatus OverallStatus, IReadOnlyList<ComplianceResult> Results);
```

The MCP tool serializes this to a formatted string so the LLM specialist agent can read and explain it:

```
OPM Compliance Report
Position: IT Specialist (ID: 42)
Series: 2210 | Overall: FAIL

RequiredFields         PASS    Passed.
PayGradeRange          PASS    Passed.
PayGradeAlignment      FAIL    Grade GS-16 is outside the allowed range for series 2210
                               (Information Technology Management). Allowed: GS-05 to GS-15.
                               Standard: https://www.opm.gov/...
ApplicationPeriod      PASS    Passed.
QualificationsGrade    WARN    Qualifications text does not reference the advertised grade(s)
                               (GS-16–GS-16).
SecurityClearance      PASS    Passed.
WhoMayApply            PASS    Passed.
```

---

## Testing with MCP Inspector

Start the compliance server:

```bash
dotnet run --project src/Hr.Compliance.Mcp
```

Open MCP Inspector:

```bash
npx @modelcontextprotocol/inspector http://localhost:5200/compliance
```

**1. List known series**

Call `ListOPMSeries` with no parameters. You get all 8 series with their allowed grade ranges and OPM standard URLs.

**2. Check a specific series**

Call `GetOPMStandard` with `series: "2210"`. You get the IT Management standard including allowed grades GS-05 to GS-15 and the qualification URL.

**3. Full compliance check**

Call `RunFullComplianceCheck` with `positionId: 1`. You get the full 7-rule report formatted as above. A position seeded from real USAJobs data will typically pass most rules but may warn on QualificationsGradeReference if the fetched qualifications text does not explicitly mention the grade.

**4. Validate a pay grade independently**

Call `ValidatePayGrade` with `series: "2210"`, `minGrade: "GS-07"`, `maxGrade: "GS-09"`. You get a targeted grade alignment check without fetching a position from the database.

Testing at this level confirms the rule logic is correct before the compliance agent ever sees a result.

---

## What Comes Next

Both MCP servers are running and tested. But `WriteJobDescription` returns a string that disappears when the conversation ends. Part 5 introduces the `JobAnnouncement` entity and lifecycle — Draft, CompliancePassed, ComplianceFailed, Published — so every generated draft persists across sessions with a full audit trail.

---

← [Part 3 — Building the HR Data MCP Server](part-3-hr-data-mcp-server.md) &nbsp;|&nbsp; [Part 5 — Persisting AI Artifacts →](part-5-persisting-ai-artifacts.md)

---

## References

### NuGet Packages

- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) — MCP server SDK; this server has zero `IChatClient` dependency by design

### Microsoft Documentation

- [.NET MCP SDK — Getting Started](https://learn.microsoft.com/en-us/dotnet/ai/model-context-protocol) — `[McpServerTool]` attribute and server hosting
- [OPM Classification Standards](https://www.opm.gov/policy-data-oversight/classification-qualifications/classifying-general-schedule-positions/) — Authoritative source for the 7 compliance rules

### GitHub

- [DotnetMultiAgentsTutorial](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial) — Full source for all patterns in this series
- [modelcontextprotocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk) — Official C# MCP SDK
