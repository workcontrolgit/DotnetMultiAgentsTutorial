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
// Output: data/usajobs-seed.json — commit this file so readers don't need an API key.
//
// Strategy: fetch up to PagesPerSeries pages for each of the 8 OPM series that the
// Hr.Compliance.Mcp rule engine knows about. This yields rich, varied positions that
// exercise every code path in the tutorial (search, job description, compliance check).

using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

var config = new ConfigurationBuilder()
    .AddUserSecrets("usajobs-fetcher")
    .Build();

var email   = config["UsaJobs:Email"]   ?? throw new InvalidOperationException("UsaJobs:Email secret is not set.");
var authKey = config["UsaJobs:AuthKey"] ?? throw new InvalidOperationException("UsaJobs:AuthKey secret is not set.");

// ── tuning knobs ──────────────────────────────────────────────────────────────
const int ResultsPerPage = 25;   // USAJobs max is 500; keep low to stay polite
const int PagesPerSeries = 4;    // 4 pages × 25 = up to 100 per series
const int TotalCap       = 300;  // hard cap on total positions written

// The 8 OPM occupational series that OpmStandardsRepository knows about.
// Fetching per-series ensures every series has real positions in the seed data.
string[] targetSeries =
[
    "2210",  // Information Technology Management
    "0201",  // Human Resources Management
    "0343",  // Management and Program Analysis
    "0501",  // Financial Administration
    "0301",  // Miscellaneous Administration and Program
    "0110",  // Economist
    "1102",  // Contracting
    "0018",  // Safety and Occupational Health Management
];

// ── HTTP client with USAJobs auth ─────────────────────────────────────────────
using var http = new HttpClient();
http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", email);
http.DefaultRequestHeaders.Add("Authorization-Key", authKey);

var parseOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

// ── collect across all series ─────────────────────────────────────────────────
var orgs      = new Dictionary<string, SeedOrg>(StringComparer.OrdinalIgnoreCase);
var positions = new List<SeedPosition>();
var seenIds   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

        Console.Write($"  page {page}: {url}  → ");

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

        if (items.Count == 0) break; // no more pages

        var addedThisPage = 0;
        foreach (var item in items)
        {
            if (positions.Count >= TotalCap) break;

            var d = item.MatchedObjectDescriptor;
            if (d is null) continue;

            // Deduplicate by USAJobs PositionID (= ControlNumber in the API)
            var posId = d.PositionID ?? $"{d.PositionTitle}|{d.OrganizationName}";
            if (!seenIds.Add(posId)) continue;

            var det  = d.UserArea?.Details;
            var rem  = d.PositionRemuneration?.FirstOrDefault();
            var plan = d.JobGrade?.FirstOrDefault()?.Code ?? "GS";
            var lo   = det?.LowGrade;
            var hi   = det?.HighGrade;

            // Skip positions with no meaningful text — useless for the JD demo
            var duties = det?.MajorDuties is { Count: > 0 }
                ? string.Join(" ", det.MajorDuties)
                : "";
            var description = det?.JobSummary ?? "";
            if (string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(duties))
                continue;

            var isOpen = DateTime.TryParse(d.ApplicationCloseDate, out var close)
                         && close >= DateTime.UtcNow;

            var orgName  = d.OrganizationName ?? "Unknown Agency";
            var deptName = d.DepartmentName   ?? "Unknown Department";
            orgs.TryAdd(orgName, new SeedOrg(orgName, deptName, ""));

            positions.Add(new SeedPosition(
                Title:               d.PositionTitle ?? "",
                Description:         description,
                Duties:              duties,
                Qualifications:      det?.Requirements ?? "",
                IsOpen:              isOpen,
                OccupationalSeries:  d.JobCategory?.FirstOrDefault()?.Code ?? series,
                PayGradeMin:         lo is not null ? $"{plan}-{lo.PadLeft(2, '0')}" : "",
                PayGradeMax:         hi is not null ? $"{plan}-{hi.PadLeft(2, '0')}" : "",
                AppointmentType:     MapAppointment(d.PositionAppointmentType?.FirstOrDefault()?.Name),
                WorkSchedule:        MapSchedule(d.PositionSchedule?.FirstOrDefault()?.Name),
                OpenDate:            d.PositionStartDate    ?? DateTime.UtcNow.ToString("O"),
                CloseDate:           d.ApplicationCloseDate,
                WhoMayApply:         det?.WhoMayApply?.Name is { Length: > 0 } w ? w : "U.S. Citizens",
                DutyLocation:        d.PositionLocation?.FirstOrDefault()?.CityName ?? "",
                TeleworkEligible:    det?.TeleworkEligible ?? false,
                TravelRequired:      MapTravel(det?.TravelCode),
                SecurityClearance:   MapClearance(det?.SecurityClearance),
                SupervisoryStatus:   "Yes".Equals(det?.SupervisoryPosition, StringComparison.OrdinalIgnoreCase),
                RelocationAuthorized:"Yes".Equals(det?.Relocation,          StringComparison.OrdinalIgnoreCase),
                DrugTestRequired:    "Yes".Equals(det?.DrugTestRequired,     StringComparison.OrdinalIgnoreCase),
                OrganizationName:    orgName,
                MinimumRange:        decimal.TryParse(rem?.MinimumRange, out var mn) ? mn : 0,
                MaximumRange:        decimal.TryParse(rem?.MaximumRange, out var mx) ? mx : 0,
                RateIntervalCode:    rem?.RateIntervalCode ?? "PA"
            ));
            addedThisPage++;
        }

        Console.WriteLine($"  → kept {addedThisPage} (running total: {positions.Count})");

        // If the API returned fewer items than a full page, no point fetching the next page
        if (items.Count < ResultsPerPage) break;

        // Be polite to the public API
        await Task.Delay(300);
    }
}

// ── write data/usajobs-seed.json ──────────────────────────────────────────────
var outPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "usajobs-seed.json");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

var writeOpts = new JsonSerializerOptions
{
    WriteIndented          = true,
    PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

var seed = new SeedFile(orgs.Values.ToList(), positions);
await File.WriteAllTextAsync(outPath, JsonSerializer.Serialize(seed, writeOpts));

Console.WriteLine($"\nWrote {positions.Count} positions from {orgs.Count} organizations");
Console.WriteLine($"Series breakdown:");
foreach (var s in targetSeries)
{
    var count = positions.Count(p => p.OccupationalSeries == s);
    Console.WriteLine($"  {s}: {count} positions");
}
Console.WriteLine($"Output: {outPath}");
return 0;

// ── string → enum label helpers ───────────────────────────────────────────────
static string MapAppointment(string? name) => name?.ToLower() switch
{
    { } s when s.Contains("perm") => "Permanent",
    { } s when s.Contains("term") => "Term",
    _                              => "Temporary"
};

static string MapSchedule(string? name) => name?.ToLower() switch
{
    { } s when s.Contains("full")         => "FullTime",
    { } s when s.Contains("part")         => "PartTime",
    { } s when s.Contains("intermittent") => "Intermittent",
    _                                      => "FullTime"
};

static string MapTravel(string? code) => code switch
{
    "1" => "NotRequired",
    "2" => "Occasional",
    "3" => "Sometimes",
    "4" => "Frequent",
    _   => "NotRequired"
};

static string MapClearance(string? s) => s?.ToLower() switch
{
    { } v when v.Contains("ts/sci") || v.Contains("top secret/sci") => "TopSecretSCI",
    { } v when v.Contains("top secret")                             => "TopSecret",
    { } v when v.Contains("secret")                                 => "Secret",
    { } v when v.Contains("confidential")                           => "Confidential",
    { } v when v.Contains("public trust")                           => "PublicTrust",
    _                                                                => "NotRequired"
};

// ── USAJobs API response types ────────────────────────────────────────────────
record SearchRoot(SearchResult SearchResult);
record SearchResult(int SearchResultCount, List<SearchResultItem> SearchResultItems);
record SearchResultItem(MatchedObjectDescriptor? MatchedObjectDescriptor);
record MatchedObjectDescriptor(
    string?                          PositionID,
    string?                          PositionTitle,
    string?                          OrganizationName,
    string?                          DepartmentName,
    string?                          PositionStartDate,
    string?                          ApplicationCloseDate,
    List<PositionLocation>?          PositionLocation,
    List<JobCategory>?               JobCategory,
    List<JobGrade>?                  JobGrade,
    List<PositionSchedule>?          PositionSchedule,
    List<PositionAppointmentType>?   PositionAppointmentType,
    List<PositionRemuneration>?      PositionRemuneration,
    UserArea?                        UserArea);
record PositionLocation(string? CityName, string? CountrySubDivisionCode);
record JobCategory(string? Name, string? Code);
record JobGrade(string? Code);
record PositionSchedule(string? Name, string? Code);
record PositionAppointmentType(string? Name, string? Code);
record PositionRemuneration(string? MinimumRange, string? MaximumRange, string? RateIntervalCode);
record UserArea(UserAreaDetails? Details);
record UserAreaDetails(
    string?       JobSummary,
    WhoMayApply?  WhoMayApply,
    string?       LowGrade,
    string?       HighGrade,
    string?       Requirements,
    List<string>? MajorDuties,
    string?       Relocation,
    string?       DrugTestRequired,
    bool?         TeleworkEligible,
    string?       SupervisoryPosition,
    string?       SecurityClearance,
    string?       TravelCode);
record WhoMayApply(string? Name, string? Code);

// ── seed file model ───────────────────────────────────────────────────────────
record SeedFile(List<SeedOrg> Organizations, List<SeedPosition> Positions);
record SeedOrg(string OrganizationName, string DepartmentName, string AgencyDescription);
record SeedPosition(
    string   Title,
    string   Description,
    string   Duties,
    string   Qualifications,
    bool     IsOpen,
    string   OccupationalSeries,
    string   PayGradeMin,
    string   PayGradeMax,
    string   AppointmentType,
    string   WorkSchedule,
    string   OpenDate,
    string?  CloseDate,
    string   WhoMayApply,
    string   DutyLocation,
    bool     TeleworkEligible,
    string   TravelRequired,
    string   SecurityClearance,
    bool     SupervisoryStatus,
    bool     RelocationAuthorized,
    bool     DrugTestRequired,
    string   OrganizationName,
    decimal  MinimumRange,
    decimal  MaximumRange,
    string   RateIntervalCode);
