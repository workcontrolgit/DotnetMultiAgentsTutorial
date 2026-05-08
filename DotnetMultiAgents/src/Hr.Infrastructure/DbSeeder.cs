// src/Hr.Infrastructure/DbSeeder.cs
using System.Text.Json;
using Hr.Core.Entities;
using Hr.Core.Enums;

namespace Hr.Infrastructure;

public static class DbSeeder
{
    // ← added optional jsonSeedPath parameter
    public static void Seed(HrDbContext db, string? jsonSeedPath = null)
    {
        if (db.HiringOrganizations.Any()) return;

        if (jsonSeedPath is not null && File.Exists(jsonSeedPath))
        {
            SeedFromJson(db, jsonSeedPath);
            return;
        }

        SeedFromCode(db);
    }

    // ── JSON path ─────────────────────────────────────────────────────────────
    private static void SeedFromJson(HrDbContext db, string path)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<SeedFile>(File.ReadAllText(path), opts)!;

        var orgMap = new Dictionary<string, HiringOrganization>(StringComparer.OrdinalIgnoreCase);

        foreach (var o in file.Organizations)
        {
            var entity = new HiringOrganization
            {
                OrganizationName  = o.OrganizationName,
                DepartmentName    = o.DepartmentName,
                AgencyDescription = o.AgencyDescription
            };
            db.HiringOrganizations.Add(entity);
            orgMap[o.OrganizationName] = entity;
        }

        db.SaveChanges();

        foreach (var p in file.Positions)
        {
            if (!orgMap.TryGetValue(p.OrganizationName, out var org)) continue;

            db.Positions.Add(new Position
            {
                Title                = p.Title,
                Description          = p.Description,
                Duties               = p.Duties,
                Qualifications       = p.Qualifications,
                IsOpen               = p.IsOpen,
                OccupationalSeries   = p.OccupationalSeries,
                PayGradeMin          = p.PayGradeMin,
                PayGradeMax          = p.PayGradeMax,
                AppointmentType      = Parse<AppointmentType>(p.AppointmentType,    AppointmentType.Permanent),
                WorkSchedule         = Parse<WorkSchedule>(p.WorkSchedule,          WorkSchedule.FullTime),
                OpenDate             = DateTime.TryParse(p.OpenDate,  out var od) ? od : DateTime.UtcNow,
                CloseDate            = DateTime.TryParse(p.CloseDate, out var cd) ? cd : null,
                WhoMayApply          = p.WhoMayApply,
                DutyLocation         = p.DutyLocation,
                TeleworkEligible     = p.TeleworkEligible,
                TravelRequired       = Parse<TravelRequirement>(p.TravelRequired,   TravelRequirement.NotRequired),
                SecurityClearance    = Parse<SecurityClearance>(p.SecurityClearance,SecurityClearance.NotRequired),
                SupervisoryStatus    = p.SupervisoryStatus,
                RelocationAuthorized = p.RelocationAuthorized,
                DrugTestRequired     = p.DrugTestRequired,
                HiringOrganizationId = org.Id,
                PositionRemuneration = new PositionRemuneration
                {
                    MinimumRange     = p.MinimumRange,
                    MaximumRange     = p.MaximumRange,
                    RateIntervalCode = p.RateIntervalCode,
                    Description      = p.RateIntervalCode switch
                    {
                        "PA" => "Per Year",
                        "PH" => "Per Hour",
                        "PD" => "Per Day",
                        _    => p.RateIntervalCode
                    }
                }
            });
        }

        db.SaveChanges();
        Console.WriteLine($"[DbSeeder] Seeded {file.Positions.Count} positions from {path}");
    }

    private static T Parse<T>(string value, T fallback) where T : struct, Enum
        => Enum.TryParse<T>(value, ignoreCase: true, out var result) ? result : fallback;

    // ── seed file DTOs ────────────────────────────────────────────────────────
    private record SeedFile(List<SeedOrg> Organizations, List<SeedPosition> Positions);
    private record SeedOrg(string OrganizationName, string DepartmentName, string AgencyDescription);
    private record SeedPosition(
        string   Title,        string   Description,   string   Duties,
        string   Qualifications, bool   IsOpen,        string   OccupationalSeries,
        string   PayGradeMin,  string   PayGradeMax,   string   AppointmentType,
        string   WorkSchedule, string   OpenDate,      string?  CloseDate,
        string   WhoMayApply,  string   DutyLocation,  bool     TeleworkEligible,
        string   TravelRequired, string SecurityClearance, bool SupervisoryStatus,
        bool     RelocationAuthorized, bool DrugTestRequired, string OrganizationName,
        decimal  MinimumRange, decimal  MaximumRange,  string   RateIntervalCode);

    // ── hardcoded fallback ────────────────────────────────────────────────────
    private static void SeedFromCode(HrDbContext db)
    {
        var oit = new HiringOrganization {
            OrganizationName  = "Office of Information Technology",
            DepartmentName    = "Department of Homeland Security",
            AgencyDescription = "IT infrastructure, cybersecurity, and enterprise systems"
        };
        var ohr = new HiringOrganization {
            OrganizationName  = "Office of Human Resources",
            DepartmentName    = "Department of Homeland Security",
            AgencyDescription = "Federal workforce management and talent acquisition"
        };
        var opd = new HiringOrganization {
            OrganizationName  = "Office of Policy Development",
            DepartmentName    = "Department of Homeland Security",
            AgencyDescription = "Federal policy analysis and regulatory affairs"
        };
        var fin = new HiringOrganization {
            OrganizationName  = "Office of the Chief Financial Officer",
            DepartmentName    = "Department of Homeland Security",
            AgencyDescription = "Federal financial management and budget oversight"
        };

        db.HiringOrganizations.AddRange(oit, ohr, opd, fin);
        db.SaveChanges();

        var today = DateTime.UtcNow;

        var positions = new List<Position>
        {
            new() {
                Title = "IT Specialist (SYSADMIN)",
                Description = "Manages and maintains enterprise IT systems and infrastructure.",
                Duties = "Administers Windows and Linux servers. Manages Active Directory, DNS, and DHCP. Monitors system performance and resolves incidents. Coordinates patching and vulnerability remediation.",
                Qualifications = "GS-11: One year of specialized experience equivalent to GS-09 managing enterprise server environments including Active Directory and network services.",
                IsOpen = true, OccupationalSeries = "2210", PayGradeMin = "GS-09", PayGradeMax = "GS-11",
                AppointmentType = AppointmentType.Permanent, WorkSchedule = WorkSchedule.FullTime,
                OpenDate = today, CloseDate = today.AddDays(14), WhoMayApply = "Open to US Citizens",
                DutyLocation = "Washington, DC", TeleworkEligible = true,
                SecurityClearance = SecurityClearance.Secret, TravelRequired = TravelRequirement.Occasional,
                SupervisoryStatus = false, RelocationAuthorized = false, DrugTestRequired = false,
                HiringOrganizationId = oit.Id,
                PositionRemuneration = new() { MinimumRange = 68_405, MaximumRange = 107_590 }
            },
            new() {
                Title = "Supervisory IT Specialist (INFOSEC)",
                Description = "Leads cybersecurity operations and oversees the agency information security program.",
                Duties = "Directs incident response and FISMA compliance. Manages a team of 8 IT security specialists. Briefs senior leadership on cyber risk posture.",
                Qualifications = "GS-14: One year of specialized experience equivalent to GS-13 leading an agency-wide information security program and supervising IT security personnel.",
                IsOpen = false, OccupationalSeries = "2210", PayGradeMin = "GS-14", PayGradeMax = "GS-14",
                AppointmentType = AppointmentType.Permanent, WorkSchedule = WorkSchedule.FullTime,
                OpenDate = today.AddDays(-30), CloseDate = today.AddDays(-16),
                WhoMayApply = "Open to current federal employees only",
                DutyLocation = "Washington, DC", TeleworkEligible = true,
                SecurityClearance = SecurityClearance.TopSecret, TravelRequired = TravelRequirement.Occasional,
                SupervisoryStatus = true, RelocationAuthorized = true, DrugTestRequired = false,
                HiringOrganizationId = oit.Id,
                PositionRemuneration = new() { MinimumRange = 139_395, MaximumRange = 181_216 }
            },
            new() {
                Title = "Human Resources Specialist (Recruitment)",
                Description = "Manages full-cycle federal recruitment and staffing operations.",
                Duties = "Develops job opportunity announcements on USAJobs. Rates and ranks applicants using OPM qualification standards. Advises hiring managers on merit promotion and competitive examining procedures.",
                Qualifications = "GS-09: One year of specialized experience equivalent to GS-07 in federal staffing, classification, or employee relations.",
                IsOpen = true, OccupationalSeries = "0201", PayGradeMin = "GS-07", PayGradeMax = "GS-09",
                AppointmentType = AppointmentType.Permanent, WorkSchedule = WorkSchedule.FullTime,
                OpenDate = today, CloseDate = today.AddDays(10), WhoMayApply = "Open to US Citizens",
                DutyLocation = "Arlington, VA", TeleworkEligible = true,
                SecurityClearance = SecurityClearance.PublicTrust, TravelRequired = TravelRequirement.NotRequired,
                SupervisoryStatus = false, RelocationAuthorized = false, DrugTestRequired = false,
                HiringOrganizationId = ohr.Id,
                PositionRemuneration = new() { MinimumRange = 53_105, MaximumRange = 84_441 }
            },
            new() {
                Title = "Management Analyst",
                Description = "Conducts organizational studies and evaluates federal program effectiveness.",
                Duties = "Analyzes agency workflow and recommends process improvements. Prepares management reports and briefing materials for senior leadership. Coordinates with program offices on performance measurement.",
                Qualifications = "GS-12: One year of specialized experience equivalent to GS-11 conducting management or program analysis in a federal agency.",
                IsOpen = true, OccupationalSeries = "0343", PayGradeMin = "GS-11", PayGradeMax = "GS-12",
                AppointmentType = AppointmentType.Permanent, WorkSchedule = WorkSchedule.FullTime,
                OpenDate = today.AddDays(-3), CloseDate = today.AddDays(11), WhoMayApply = "Open to US Citizens",
                DutyLocation = "Remote (US)", TeleworkEligible = true,
                SecurityClearance = SecurityClearance.PublicTrust, TravelRequired = TravelRequirement.Occasional,
                SupervisoryStatus = false, RelocationAuthorized = false, DrugTestRequired = false,
                HiringOrganizationId = opd.Id,
                PositionRemuneration = new() { MinimumRange = 82_764, MaximumRange = 128_956 }
            },
            new() {
                Title = "Financial Analyst",
                Description = "Supports federal budget formulation and execution for the agency's appropriated funds.",
                Duties = "Prepares budget justifications and spending plans. Monitors obligations and expenditures against appropriations. Coordinates with OMB on passback and apportionment requests.",
                Qualifications = "GS-11: One year of specialized experience equivalent to GS-09 in federal budget or financial management.",
                IsOpen = true, OccupationalSeries = "0501", PayGradeMin = "GS-09", PayGradeMax = "GS-11",
                AppointmentType = AppointmentType.Permanent, WorkSchedule = WorkSchedule.FullTime,
                OpenDate = today.AddDays(-1), CloseDate = today.AddDays(13), WhoMayApply = "Open to US Citizens",
                DutyLocation = "Washington, DC", TeleworkEligible = false,
                SecurityClearance = SecurityClearance.PublicTrust, TravelRequired = TravelRequirement.NotRequired,
                SupervisoryStatus = false, RelocationAuthorized = false, DrugTestRequired = false,
                HiringOrganizationId = fin.Id,
                PositionRemuneration = new() { MinimumRange = 68_405, MaximumRange = 107_590 }
            },
        };

        db.Positions.AddRange(positions);
        db.SaveChanges();
    }
}
