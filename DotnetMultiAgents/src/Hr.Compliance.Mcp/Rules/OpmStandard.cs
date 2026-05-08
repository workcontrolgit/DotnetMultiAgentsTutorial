// Rules/OpmStandard.cs
namespace Hr.Compliance.Mcp.Rules;

/// <summary>
/// Reference data for one OPM occupational series.
/// Sourced from: https://www.opm.gov/policy-data-oversight/classification-qualifications/
/// </summary>
public record OpmStandard(
    string OccupationalSeries,
    string SeriesTitle,
    IReadOnlyList<int> AllowedGradeNumbers,
    string QualificationStandardUrl,
    string RequiredQualificationKeyword);
