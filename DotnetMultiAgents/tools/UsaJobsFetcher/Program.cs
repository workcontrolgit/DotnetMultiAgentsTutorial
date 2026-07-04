// tools/UsaJobsFetcher/Program.cs
// One-time tool: fetches real federal job postings from the USAJobs API
// and writes data/usajobs-seed.json to the solution root.
//
// Store credentials via .NET User Secrets (never commit them):
//   dotnet user-secrets set "UsaJobs:Email"   "your@email.com"  --project tools/UsaJobsFetcher
//   dotnet user-secrets set "UsaJobs:AuthKey" "your-key-here"   --project tools/UsaJobsFetcher
//
// Then run from the solution root:
//   dotnet run --project tools/UsaJobsFetcher
//
// Output: data/usajobs-seed.json - commit this file so readers do not need an API key.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .AddUserSecrets("usajobs-fetcher")
    .Build();

var email = config["UsaJobs:Email"] ?? throw new InvalidOperationException("UsaJobs:Email secret is not set.");
var authKey = config["UsaJobs:AuthKey"] ?? throw new InvalidOperationException("UsaJobs:AuthKey secret is not set.");

const int ResultsPerPage = 25;
const int PagesPerSeries = 4;
const int TotalCap = 300;

string[] targetSeries =
[
    "2210",
    "0201",
    "0343",
    "0501",
    "0301",
    "0110",
    "1102",
    "0018",
];

using var http = new HttpClient();
http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", email);
http.DefaultRequestHeaders.Add("Authorization-Key", authKey);

var parseOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

var orgs = new Dictionary<string, SeedOrg>(StringComparer.OrdinalIgnoreCase);
var positions = new List<SeedPosition>();
var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

foreach (var series in targetSeries)
{
    if (positions.Count >= TotalCap) break;

    Console.WriteLine($"\nFetching series {series}...");

    for (var page = 1; page <= PagesPerSeries; page++)
    {
        if (positions.Count >= TotalCap) break;

        var url =
            $"https://data.usajobs.gov/api/search" +
            $"?JobCategoryCode={series}" +
            $"&ResultsPerPage={ResultsPerPage}" +
            $"&Page={page}";

        Console.Write($"  page {page}: {url} -> ");

        string responseJson;
        try
        {
            responseJson = await http.GetStringAsync(url);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            break;
        }

        SearchRoot? root;
        try
        {
            root = JsonSerializer.Deserialize<SearchRoot>(responseJson, parseOpts);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"parse error: {ex.Message}");
            break;
        }

        var items = root?.SearchResult?.SearchResultItems ?? [];
        Console.WriteLine($"{items.Count} items");

        if (items.Count == 0) break;

        var addedThisPage = 0;
        foreach (var item in items)
        {
            if (positions.Count >= TotalCap) break;

            var d = item.MatchedObjectDescriptor;
            if (d is null) continue;

            var posId = d.PositionID ?? item.MatchedObjectId ?? $"{d.PositionTitle}|{d.OrganizationName}";
            if (!seenIds.Add(posId)) continue;

            var det = d.UserArea?.Details;
            var rem = d.PositionRemuneration?.FirstOrDefault();
            var plan = d.JobGrade?.FirstOrDefault()?.Code ?? "GS";
            var lo = det?.LowGrade;
            var hi = det?.HighGrade;

            var duties = det?.MajorDuties is { Count: > 0 }
                ? string.Join(" ", det.MajorDuties)
                : string.Empty;
            var description = det?.JobSummary ?? string.Empty;
            if (string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(duties))
                continue;

            var isOpen = DateTime.TryParse(d.ApplicationCloseDate, out var close)
                && close >= DateTime.UtcNow;

            var orgName = d.OrganizationName ?? "Unknown Agency";
            var deptName = d.DepartmentName ?? "Unknown Department";
            orgs.TryAdd(orgName, new SeedOrg(orgName, deptName, string.Empty));

            positions.Add(new SeedPosition(
                AnnouncementNumber: d.PositionID ?? string.Empty,
                UsaJobsId: item.MatchedObjectId ?? string.Empty,
                PositionUri: d.PositionURI ?? string.Empty,
                ApplyUri: d.ApplyURI?.FirstOrDefault() ?? string.Empty,
                Title: d.PositionTitle ?? string.Empty,
                Description: description,
                Duties: duties,
                Qualifications: det?.Requirements ?? string.Empty,
                Education: det?.Education ?? string.Empty,
                Evaluations: det?.Evaluations ?? string.Empty,
                KeyRequirements: JoinLines(det?.KeyRequirements),
                PromotionPotential: det?.PromotionPotential ?? string.Empty,
                IsOpen: isOpen,
                OccupationalSeries: d.JobCategory?.FirstOrDefault()?.Code ?? series,
                OccupationalSeriesTitle: d.JobCategory?.FirstOrDefault()?.Name ?? string.Empty,
                PayGradeMin: lo is not null ? $"{plan}-{lo.PadLeft(2, '0')}" : string.Empty,
                PayGradeMax: hi is not null ? $"{plan}-{hi.PadLeft(2, '0')}" : string.Empty,
                AppointmentType: MapAppointment(d.PositionAppointmentType?.FirstOrDefault()?.Name),
                PositionOfferingType: d.PositionOfferingType ?? string.Empty,
                WorkSchedule: MapSchedule(d.PositionSchedule?.FirstOrDefault()?.Name),
                OpenDate: d.PositionStartDate ?? DateTime.UtcNow.ToString("O"),
                CloseDate: d.ApplicationCloseDate,
                WhoMayApply: det?.WhoMayApply?.Name is { Length: > 0 } w ? w : "U.S. Citizens",
                HiringPath: JoinCsv(det?.HiringPath?.Select(p => p.Code ?? p.Name)),
                DutyLocation: d.PositionLocation?.FirstOrDefault()?.CityName ?? string.Empty,
                DutyLocationState: d.PositionLocation?.FirstOrDefault()?.CountrySubDivisionCode ?? string.Empty,
                TeleworkEligible: det?.TeleworkEligible ?? false,
                TravelRequired: MapTravel(det?.TravelCode),
                SecurityClearance: MapClearance(det?.SecurityClearance),
                ServiceType: det?.ServiceType ?? string.Empty,
                SubAgencyName: det?.SubAgencyName ?? string.Empty,
                TotalOpenings: det?.TotalOpenings ?? string.Empty,
                AdjudicationType: JoinCsv(det?.AdjudicationType),
                RemoteEligible: det?.RemoteIndicator ?? false,
                FinancialDisclosure: det?.FinancialDisclosure ?? false,
                SupervisoryStatus: IsYes(det?.SupervisoryPosition),
                RelocationAuthorized: IsYes(det?.Relocation),
                DrugTestRequired: IsYes(det?.DrugTestRequired),
                PositionSensitivityAndRisk: det?.PositionSensitivityAndRisk ?? string.Empty,
                ContactName: det?.AgencyContact?.Name ?? string.Empty,
                ContactPhone: det?.AgencyContact?.Phone ?? string.Empty,
                ContactEmail: det?.AgencyContact?.Email ?? string.Empty,
                ContactAddress: det?.AgencyContact?.Address ?? string.Empty,
                ConditionsOfEmployment: det?.ConditionsOfEmployment ?? string.Empty,
                RequiredDocuments: det?.RequiredDocuments ?? string.Empty,
                HowToApply: det?.HowToApply ?? string.Empty,
                NextSteps: det?.WhatToExpectNext ?? string.Empty,
                AdditionalInformation: det?.OtherInformation ?? string.Empty,
                OrganizationName: orgName,
                MinimumRange: decimal.TryParse(rem?.MinimumRange, out var mn) ? mn : 0,
                MaximumRange: decimal.TryParse(rem?.MaximumRange, out var mx) ? mx : 0,
                RateIntervalCode: rem?.RateIntervalCode ?? "PA"
            ));
            addedThisPage++;
        }

        Console.WriteLine($"  -> kept {addedThisPage} (running total: {positions.Count})");

        if (items.Count < ResultsPerPage) break;
        await Task.Delay(300);
    }
}

var outPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "usajobs-seed.json");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

var writeOpts = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

var seed = new SeedFile(orgs.Values.ToList(), positions);
await File.WriteAllTextAsync(outPath, JsonSerializer.Serialize(seed, writeOpts));

Console.WriteLine($"\nWrote {positions.Count} positions from {orgs.Count} organizations");
Console.WriteLine("Series breakdown:");
foreach (var s in targetSeries)
{
    var count = positions.Count(p => p.OccupationalSeries == s);
    Console.WriteLine($"  {s}: {count} positions");
}
Console.WriteLine($"Output: {outPath}");
return 0;

static string MapAppointment(string? name) => name?.ToLowerInvariant() switch
{
    { } s when s.Contains("perm") => "Permanent",
    { } s when s.Contains("term") => "Term",
    _ => "Temporary"
};

static string MapSchedule(string? name) => name?.ToLowerInvariant() switch
{
    { } s when s.Contains("full") => "FullTime",
    { } s when s.Contains("part") => "PartTime",
    { } s when s.Contains("intermittent") => "Intermittent",
    _ => "FullTime"
};

static string MapTravel(string? code) => code switch
{
    "1" => "NotRequired",
    "2" => "Occasional",
    "3" => "Sometimes",
    "4" => "Frequent",
    _ => "NotRequired"
};

static string MapClearance(string? s) => s?.ToLowerInvariant() switch
{
    { } v when v.Contains("ts/sci") || v.Contains("top secret/sci") => "TopSecretSCI",
    { } v when v.Contains("top secret") => "TopSecret",
    { } v when v.Contains("secret") => "Secret",
    { } v when v.Contains("confidential") => "Confidential",
    { } v when v.Contains("public trust") => "PublicTrust",
    _ => "NotRequired"
};

static bool IsYes(string? value)
    => "Yes".Equals(value, StringComparison.OrdinalIgnoreCase)
       || "Y".Equals(value, StringComparison.OrdinalIgnoreCase)
       || "True".Equals(value, StringComparison.OrdinalIgnoreCase);

static string JoinLines(IEnumerable<string>? values)
    => values is null ? string.Empty : string.Join(Environment.NewLine, values.Where(v => !string.IsNullOrWhiteSpace(v)));

static string JoinCsv(IEnumerable<string?>? values)
    => values is null ? string.Empty : string.Join(", ", values.Where(v => !string.IsNullOrWhiteSpace(v)));

record SearchRoot(SearchResult SearchResult);
record SearchResult(int SearchResultCount, List<SearchResultItem> SearchResultItems);
record SearchResultItem(string? MatchedObjectId, MatchedObjectDescriptor? MatchedObjectDescriptor);
record MatchedObjectDescriptor(
    string? PositionID,
    string? PositionURI,
    List<string>? ApplyURI,
    string? PositionTitle,
    string? OrganizationName,
    string? DepartmentName,
    string? PositionStartDate,
    string? ApplicationCloseDate,
    string? PositionOfferingType,
    List<PositionLocation>? PositionLocation,
    List<JobCategory>? JobCategory,
    List<JobGrade>? JobGrade,
    List<PositionSchedule>? PositionSchedule,
    List<PositionAppointmentType>? PositionAppointmentType,
    List<PositionRemuneration>? PositionRemuneration,
    UserArea? UserArea);
record PositionLocation(string? CityName, string? CountrySubDivisionCode);
record JobCategory(string? Name, string? Code);
record JobGrade(string? Code);
record PositionSchedule(string? Name, string? Code);
record PositionAppointmentType(string? Name, string? Code);
record PositionRemuneration(string? MinimumRange, string? MaximumRange, string? RateIntervalCode);
record UserArea(UserAreaDetails? Details);
record UserAreaDetails(
    string? JobSummary,
    WhoMayApply? WhoMayApply,
    string? LowGrade,
    string? HighGrade,
    string? Requirements,
    string? Education,
    string? Evaluations,
    List<string>? KeyRequirements,
    string? PromotionPotential,
    List<string>? MajorDuties,
    List<HiringPath>? HiringPath,
    string? Relocation,
    string? DrugTestRequired,
    bool? TeleworkEligible,
    string? SupervisoryPosition,
    string? SecurityClearance,
    string? TravelCode,
    string? ServiceType,
    string? SubAgencyName,
    string? TotalOpenings,
    List<string>? AdjudicationType,
    bool? RemoteIndicator,
    bool? FinancialDisclosure,
    string? PositionSensitivityAndRisk,
    string? ConditionsOfEmployment,
    string? RequiredDocuments,
    string? HowToApply,
    string? WhatToExpectNext,
    string? OtherInformation,
    AgencyContact? AgencyContact);
record WhoMayApply(string? Name, string? Code);
record HiringPath(string? Name, string? Code);
record AgencyContact(string? Name, string? Phone, string? Email, string? Address);

record SeedFile(List<SeedOrg> Organizations, List<SeedPosition> Positions);
record SeedOrg(string OrganizationName, string DepartmentName, string AgencyDescription);
record SeedPosition(
    string AnnouncementNumber,
    string UsaJobsId,
    string PositionUri,
    string ApplyUri,
    string Title,
    string Description,
    string Duties,
    string Qualifications,
    string Education,
    string Evaluations,
    string KeyRequirements,
    string PromotionPotential,
    bool IsOpen,
    string OccupationalSeries,
    string OccupationalSeriesTitle,
    string PayGradeMin,
    string PayGradeMax,
    string AppointmentType,
    string PositionOfferingType,
    string WorkSchedule,
    string OpenDate,
    string? CloseDate,
    string WhoMayApply,
    string HiringPath,
    string DutyLocation,
    string DutyLocationState,
    bool TeleworkEligible,
    string TravelRequired,
    string SecurityClearance,
    string ServiceType,
    string SubAgencyName,
    string TotalOpenings,
    string AdjudicationType,
    bool RemoteEligible,
    bool FinancialDisclosure,
    bool SupervisoryStatus,
    bool RelocationAuthorized,
    bool DrugTestRequired,
    string PositionSensitivityAndRisk,
    string ContactName,
    string ContactPhone,
    string ContactEmail,
    string ContactAddress,
    string ConditionsOfEmployment,
    string RequiredDocuments,
    string HowToApply,
    string NextSteps,
    string AdditionalInformation,
    string OrganizationName,
    decimal MinimumRange,
    decimal MaximumRange,
    string RateIntervalCode);
