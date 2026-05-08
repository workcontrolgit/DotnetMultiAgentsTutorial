// Rules/OpmStandardsRepository.cs
namespace Hr.Compliance.Mcp.Rules;

/// <summary>
/// Static reference data for OPM Qualification Standards.
/// In production this could be backed by a database or OPM API call.
/// Covers the occupational series used in HrMcpDb seed data plus common federal series.
/// Source: https://www.opm.gov/policy-data-oversight/classification-qualifications/general-schedule-qualification-standards/
/// </summary>
public sealed class OpmStandardsRepository
{
    // GS grade numbers allowed for professional/administrative series
    private static readonly IReadOnlyList<int> Gs5To15 = [5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
    private static readonly IReadOnlyList<int> Gs5To12 = [5, 6, 7, 8, 9, 10, 11, 12];

    private static readonly Dictionary<string, OpmStandard> Standards = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ["2210"] = new(
            OccupationalSeries:         "2210",
            SeriesTitle:                "Information Technology Management",
            AllowedGradeNumbers:        Gs5To15,
            QualificationStandardUrl:   "https://www.opm.gov/policy-data-oversight/classification-qualifications/general-schedule-qualification-standards/2200/information-technology-it-management-series-2200-group-coverage-qualification-standard/",
            RequiredQualificationKeyword: "information technology"),

        ["0201"] = new(
            OccupationalSeries:         "0201",
            SeriesTitle:                "Human Resources Management",
            AllowedGradeNumbers:        Gs5To15,
            QualificationStandardUrl:   "https://www.opm.gov/policy-data-oversight/classification-qualifications/general-schedule-qualification-standards/0200/human-resources-management-series-0201/",
            RequiredQualificationKeyword: "human resources"),

        ["0343"] = new(
            OccupationalSeries:         "0343",
            SeriesTitle:                "Management and Program Analysis",
            AllowedGradeNumbers:        Gs5To15,
            QualificationStandardUrl:   "https://www.opm.gov/policy-data-oversight/classification-qualifications/general-schedule-qualification-standards/0300/management-and-program-analysis-series-0343/",
            RequiredQualificationKeyword: "management"),

        ["0501"] = new(
            OccupationalSeries:         "0501",
            SeriesTitle:                "Financial Administration and Program",
            AllowedGradeNumbers:        Gs5To15,
            QualificationStandardUrl:   "https://www.opm.gov/policy-data-oversight/classification-qualifications/general-schedule-qualification-standards/0500/financial-administration-and-program-series-0501/",
            RequiredQualificationKeyword: "financial"),

        ["0301"] = new(
            OccupationalSeries:         "0301",
            SeriesTitle:                "Miscellaneous Administration and Program",
            AllowedGradeNumbers:        Gs5To15,
            QualificationStandardUrl:   "https://www.opm.gov/policy-data-oversight/classification-qualifications/general-schedule-qualification-standards/0300/miscellaneous-administration-and-program-series-0301/",
            RequiredQualificationKeyword: "administration"),

        ["0110"] = new(
            OccupationalSeries:         "0110",
            SeriesTitle:                "Economist",
            AllowedGradeNumbers:        Gs5To15,
            QualificationStandardUrl:   "https://www.opm.gov/policy-data-oversight/classification-qualifications/general-schedule-qualification-standards/0100/economist-series-0110/",
            RequiredQualificationKeyword: "economics"),

        ["1102"] = new(
            OccupationalSeries:         "1102",
            SeriesTitle:                "Contracting",
            AllowedGradeNumbers:        Gs5To15,
            QualificationStandardUrl:   "https://www.opm.gov/policy-data-oversight/classification-qualifications/general-schedule-qualification-standards/1100/contracting-series-1102/",
            RequiredQualificationKeyword: "contracting"),

        ["0018"] = new(
            OccupationalSeries:         "0018",
            SeriesTitle:                "Safety and Occupational Health Management",
            AllowedGradeNumbers:        Gs5To12,
            QualificationStandardUrl:   "https://www.opm.gov/policy-data-oversight/classification-qualifications/general-schedule-qualification-standards/0000/safety-and-occupational-health-management-series-0018/",
            RequiredQualificationKeyword: "safety"),
    };

    public OpmStandard? GetBySeries(string occupationalSeries) =>
        Standards.TryGetValue(occupationalSeries.TrimStart('0').PadLeft(4, '0'), out var standard)
            ? standard
            : null;

    public IReadOnlyCollection<OpmStandard> GetAll() => Standards.Values;
}
