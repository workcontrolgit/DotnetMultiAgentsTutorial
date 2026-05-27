// src/Hr.Infrastructure/DbSeeder.cs
using System.Text.Json;
using Hr.Core.Entities;
using Hr.Core.Enums;

namespace Hr.Infrastructure;

public static class DbSeeder
{
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

    private static void SeedFromJson(HrDbContext db, string path)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<SeedFile>(File.ReadAllText(path), opts)!;

        var orgMap = new Dictionary<string, HiringOrganization>(StringComparer.OrdinalIgnoreCase);

        foreach (var o in file.Organizations)
        {
            var entity = new HiringOrganization
            {
                OrganizationName = o.OrganizationName,
                DepartmentName = o.DepartmentName,
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
                AnnouncementNumber = p.AnnouncementNumber ?? string.Empty,
                UsaJobsId = p.UsaJobsId ?? string.Empty,
                PositionUri = p.PositionUri ?? string.Empty,
                ApplyUri = p.ApplyUri ?? string.Empty,
                Title = p.Title,
                Description = p.Description,
                Duties = p.Duties,
                Qualifications = p.Qualifications,
                Education = p.Education ?? string.Empty,
                Evaluations = p.Evaluations ?? string.Empty,
                KeyRequirements = p.KeyRequirements ?? string.Empty,
                PromotionPotential = p.PromotionPotential ?? string.Empty,
                IsOpen = p.IsOpen,
                OccupationalSeries = p.OccupationalSeries,
                OccupationalSeriesTitle = p.OccupationalSeriesTitle ?? string.Empty,
                PayGradeMin = p.PayGradeMin,
                PayGradeMax = p.PayGradeMax,
                AppointmentType = Parse<AppointmentType>(p.AppointmentType, AppointmentType.Permanent),
                PositionOfferingType = p.PositionOfferingType ?? string.Empty,
                WorkSchedule = Parse<WorkSchedule>(p.WorkSchedule, WorkSchedule.FullTime),
                OpenDate = DateTime.TryParse(p.OpenDate, out var od) ? od : DateTime.UtcNow,
                CloseDate = DateTime.TryParse(p.CloseDate, out var cd) ? cd : null,
                WhoMayApply = p.WhoMayApply,
                HiringPath = p.HiringPath ?? string.Empty,
                DutyLocation = p.DutyLocation,
                DutyLocationState = p.DutyLocationState ?? string.Empty,
                TeleworkEligible = p.TeleworkEligible,
                TravelRequired = Parse<TravelRequirement>(p.TravelRequired, TravelRequirement.NotRequired),
                SecurityClearance = Parse<SecurityClearance>(p.SecurityClearance, SecurityClearance.NotRequired),
                ServiceType = p.ServiceType ?? string.Empty,
                SubAgencyName = p.SubAgencyName ?? string.Empty,
                TotalOpenings = p.TotalOpenings ?? string.Empty,
                AdjudicationType = p.AdjudicationType ?? string.Empty,
                RemoteEligible = p.RemoteEligible,
                FinancialDisclosure = p.FinancialDisclosure,
                SupervisoryStatus = p.SupervisoryStatus,
                RelocationAuthorized = p.RelocationAuthorized,
                DrugTestRequired = p.DrugTestRequired,
                PositionSensitivityAndRisk = p.PositionSensitivityAndRisk ?? string.Empty,
                ContactName = p.ContactName ?? string.Empty,
                ContactPhone = p.ContactPhone ?? string.Empty,
                ContactEmail = p.ContactEmail ?? string.Empty,
                ContactAddress = p.ContactAddress ?? string.Empty,
                ConditionsOfEmployment = p.ConditionsOfEmployment ?? string.Empty,
                RequiredDocuments = p.RequiredDocuments ?? string.Empty,
                HowToApply = p.HowToApply ?? string.Empty,
                NextSteps = p.NextSteps ?? string.Empty,
                AdditionalInformation = p.AdditionalInformation ?? string.Empty,
                HiringOrganizationId = org.Id,
                PositionRemuneration = new PositionRemuneration
                {
                    MinimumRange = p.MinimumRange,
                    MaximumRange = p.MaximumRange,
                    RateIntervalCode = p.RateIntervalCode,
                    Description = p.RateIntervalCode switch
                    {
                        "PA" => "Per Year",
                        "PH" => "Per Hour",
                        "PD" => "Per Day",
                        _ => p.RateIntervalCode
                    }
                }
            });
        }

        db.SaveChanges();
        Console.WriteLine($"[DbSeeder] Seeded {file.Positions.Count} positions from {path}");
    }

    private static T Parse<T>(string value, T fallback) where T : struct, Enum
        => Enum.TryParse<T>(value, ignoreCase: true, out var result) ? result : fallback;

    private record SeedFile(List<SeedOrg> Organizations, List<SeedPosition> Positions);
    private record SeedOrg(string OrganizationName, string DepartmentName, string AgencyDescription);
    private record SeedPosition(
        string Title,
        string Description,
        string Duties,
        string Qualifications,
        bool IsOpen,
        string OccupationalSeries,
        string PayGradeMin,
        string PayGradeMax,
        string AppointmentType,
        string WorkSchedule,
        string OpenDate,
        string? CloseDate,
        string WhoMayApply,
        string DutyLocation,
        bool TeleworkEligible,
        string TravelRequired,
        string SecurityClearance,
        bool SupervisoryStatus,
        bool RelocationAuthorized,
        bool DrugTestRequired,
        string OrganizationName,
        decimal MinimumRange,
        decimal MaximumRange,
        string RateIntervalCode,
        string? AnnouncementNumber = null,
        string? UsaJobsId = null,
        string? PositionUri = null,
        string? ApplyUri = null,
        string? Education = null,
        string? Evaluations = null,
        string? KeyRequirements = null,
        string? PromotionPotential = null,
        string? OccupationalSeriesTitle = null,
        string? PositionOfferingType = null,
        string? HiringPath = null,
        string? DutyLocationState = null,
        string? ServiceType = null,
        string? SubAgencyName = null,
        string? TotalOpenings = null,
        string? AdjudicationType = null,
        bool RemoteEligible = false,
        bool FinancialDisclosure = false,
        string? PositionSensitivityAndRisk = null,
        string? ContactName = null,
        string? ContactPhone = null,
        string? ContactEmail = null,
        string? ContactAddress = null,
        string? ConditionsOfEmployment = null,
        string? RequiredDocuments = null,
        string? HowToApply = null,
        string? NextSteps = null,
        string? AdditionalInformation = null);

    private static void SeedFromCode(HrDbContext db)
    {
        var oit = new HiringOrganization
        {
            OrganizationName = "Office of Information Technology",
            DepartmentName = "Department of Homeland Security",
            AgencyDescription = "IT infrastructure, cybersecurity, and enterprise systems"
        };
        var ohr = new HiringOrganization
        {
            OrganizationName = "Office of Human Resources",
            DepartmentName = "Department of Homeland Security",
            AgencyDescription = "Federal workforce management and talent acquisition"
        };
        var opd = new HiringOrganization
        {
            OrganizationName = "Office of Policy Development",
            DepartmentName = "Department of Homeland Security",
            AgencyDescription = "Federal policy analysis and regulatory affairs"
        };
        var fin = new HiringOrganization
        {
            OrganizationName = "Office of the Chief Financial Officer",
            DepartmentName = "Department of Homeland Security",
            AgencyDescription = "Federal financial management and budget oversight"
        };

        db.HiringOrganizations.AddRange(oit, ohr, opd, fin);
        db.SaveChanges();

        var today = DateTime.UtcNow;

        var positions = new List<Position>
        {
            new()
            {
                AnnouncementNumber = "DHS-OIT-2026-001",
                Title = "IT Specialist (SYSADMIN)",
                Description = "Manages and maintains enterprise IT systems and infrastructure.",
                Duties = "Administers Windows and Linux servers. Manages Active Directory, DNS, and DHCP. Monitors system performance and resolves incidents. Coordinates patching and vulnerability remediation.",
                Qualifications = "GS-11: One year of specialized experience equivalent to GS-09 managing enterprise server environments including Active Directory and network services.",
                Education = "No substitution of education for specialized experience at this grade level.",
                Evaluations = "Applicants will be evaluated on technical competence, problem solving, and systems administration experience.",
                KeyRequirements = "U.S. citizenship; background investigation; may require after-hours support.",
                PromotionPotential = "GS-12",
                IsOpen = true,
                OccupationalSeries = "2210",
                OccupationalSeriesTitle = "Information Technology Management",
                PayGradeMin = "GS-09",
                PayGradeMax = "GS-11",
                AppointmentType = AppointmentType.Permanent,
                PositionOfferingType = "Competitive Service",
                WorkSchedule = WorkSchedule.FullTime,
                OpenDate = today,
                CloseDate = today.AddDays(14),
                WhoMayApply = "Open to US Citizens",
                HiringPath = "public",
                DutyLocation = "Washington",
                DutyLocationState = "DC",
                TeleworkEligible = true,
                TravelRequired = TravelRequirement.Occasional,
                SecurityClearance = SecurityClearance.Secret,
                ServiceType = "Competitive",
                SubAgencyName = "Enterprise Operations",
                TotalOpenings = "2",
                AdjudicationType = "Suitability/Fitness",
                RemoteEligible = false,
                FinancialDisclosure = false,
                SupervisoryStatus = false,
                RelocationAuthorized = false,
                DrugTestRequired = false,
                PositionSensitivityAndRisk = "Noncritical-Sensitive / Moderate Risk",
                ConditionsOfEmployment = "Must maintain privileged-access eligibility.",
                RequiredDocuments = "Resume; SF-50 if applicable.",
                HowToApply = "Apply through USAJobs before the closing date.",
                NextSteps = "Qualified applicants may be referred to the hiring manager.",
                AdditionalInformation = "This position supports enterprise infrastructure modernization.",
                HiringOrganizationId = oit.Id,
                PositionRemuneration = new() { MinimumRange = 68_405, MaximumRange = 107_590 }
            },
            new()
            {
                AnnouncementNumber = "DHS-OIT-2026-002",
                Title = "Supervisory IT Specialist (INFOSEC)",
                Description = "Leads cybersecurity operations and oversees the agency information security program.",
                Duties = "Directs incident response and FISMA compliance. Manages a team of 8 IT security specialists. Briefs senior leadership on cyber risk posture.",
                Qualifications = "GS-14: One year of specialized experience equivalent to GS-13 leading an agency-wide information security program and supervising IT security personnel.",
                Education = "Education may not be substituted for specialized experience.",
                Evaluations = "Applicants will be evaluated on leadership, cyber operations, and program management experience.",
                KeyRequirements = "U.S. citizenship; Top Secret eligibility; supervisory probation may be required.",
                PromotionPotential = "GS-14",
                IsOpen = false,
                OccupationalSeries = "2210",
                OccupationalSeriesTitle = "Information Technology Management",
                PayGradeMin = "GS-14",
                PayGradeMax = "GS-14",
                AppointmentType = AppointmentType.Permanent,
                PositionOfferingType = "Competitive Service",
                WorkSchedule = WorkSchedule.FullTime,
                OpenDate = today.AddDays(-30),
                CloseDate = today.AddDays(-16),
                WhoMayApply = "Open to current federal employees only",
                HiringPath = "fed-competitive",
                DutyLocation = "Washington",
                DutyLocationState = "DC",
                TeleworkEligible = true,
                TravelRequired = TravelRequirement.Occasional,
                SecurityClearance = SecurityClearance.TopSecret,
                ServiceType = "Competitive",
                SubAgencyName = "Cybersecurity Division",
                TotalOpenings = "1",
                AdjudicationType = "Credentialing, Suitability/Fitness",
                RemoteEligible = false,
                FinancialDisclosure = true,
                SupervisoryStatus = true,
                RelocationAuthorized = true,
                DrugTestRequired = false,
                PositionSensitivityAndRisk = "Critical-Sensitive / High Risk",
                ConditionsOfEmployment = "Must sign supervisory and privileged-user agreements.",
                RequiredDocuments = "Resume; SF-50; performance appraisal.",
                HowToApply = "Apply through USAJobs before closing.",
                NextSteps = "Best-qualified applicants will be interviewed.",
                AdditionalInformation = "Supervises a multidisciplinary cyber operations team.",
                HiringOrganizationId = oit.Id,
                PositionRemuneration = new() { MinimumRange = 139_395, MaximumRange = 181_216 }
            },
            new()
            {
                AnnouncementNumber = "DHS-OHR-2026-003",
                Title = "Human Resources Specialist (Recruitment)",
                Description = "Manages full-cycle federal recruitment and staffing operations.",
                Duties = "Develops job opportunity announcements on USAJobs. Rates and ranks applicants using OPM qualification standards. Advises hiring managers on merit promotion and competitive examining procedures.",
                Qualifications = "GS-09: One year of specialized experience equivalent to GS-07 in federal staffing, classification, or employee relations.",
                Education = "Graduate education may be qualifying as described in the announcement.",
                Evaluations = "Applicants will be evaluated on staffing policy knowledge and customer service.",
                KeyRequirements = "U.S. citizenship; background investigation.",
                PromotionPotential = "GS-11",
                IsOpen = true,
                OccupationalSeries = "0201",
                OccupationalSeriesTitle = "Human Resources Management",
                PayGradeMin = "GS-07",
                PayGradeMax = "GS-09",
                AppointmentType = AppointmentType.Permanent,
                PositionOfferingType = "Competitive Service",
                WorkSchedule = WorkSchedule.FullTime,
                OpenDate = today,
                CloseDate = today.AddDays(10),
                WhoMayApply = "Open to US Citizens",
                HiringPath = "public",
                DutyLocation = "Arlington",
                DutyLocationState = "VA",
                TeleworkEligible = true,
                TravelRequired = TravelRequirement.NotRequired,
                SecurityClearance = SecurityClearance.PublicTrust,
                ServiceType = "Competitive",
                SubAgencyName = "Talent Acquisition",
                TotalOpenings = "1",
                AdjudicationType = "Suitability/Fitness",
                RemoteEligible = false,
                FinancialDisclosure = false,
                SupervisoryStatus = false,
                RelocationAuthorized = false,
                DrugTestRequired = false,
                PositionSensitivityAndRisk = "Moderate Risk",
                ConditionsOfEmployment = "May require probationary period.",
                RequiredDocuments = "Resume; transcripts if qualifying via education.",
                HowToApply = "Submit application package in USAJobs.",
                NextSteps = "Applications will be reviewed after the closing date.",
                AdditionalInformation = "Supports merit-staffing operations across DHS components.",
                HiringOrganizationId = ohr.Id,
                PositionRemuneration = new() { MinimumRange = 53_105, MaximumRange = 84_441 }
            },
            new()
            {
                AnnouncementNumber = "DHS-OPD-2026-004",
                Title = "Management Analyst",
                Description = "Conducts organizational studies and evaluates federal program effectiveness.",
                Duties = "Analyzes agency workflow and recommends process improvements. Prepares management reports and briefing materials for senior leadership. Coordinates with program offices on performance measurement.",
                Qualifications = "GS-12: One year of specialized experience equivalent to GS-11 conducting management or program analysis in a federal agency.",
                Education = "Education may not be substituted at this grade level.",
                Evaluations = "Applicants will be evaluated on analysis, writing, and stakeholder engagement.",
                KeyRequirements = "U.S. citizenship; background investigation.",
                PromotionPotential = "GS-13",
                IsOpen = true,
                OccupationalSeries = "0343",
                OccupationalSeriesTitle = "Management and Program Analysis",
                PayGradeMin = "GS-11",
                PayGradeMax = "GS-12",
                AppointmentType = AppointmentType.Permanent,
                PositionOfferingType = "Competitive Service",
                WorkSchedule = WorkSchedule.FullTime,
                OpenDate = today.AddDays(-3),
                CloseDate = today.AddDays(11),
                WhoMayApply = "Open to US Citizens",
                HiringPath = "public",
                DutyLocation = "Remote",
                DutyLocationState = "US",
                TeleworkEligible = true,
                TravelRequired = TravelRequirement.Occasional,
                SecurityClearance = SecurityClearance.PublicTrust,
                ServiceType = "Competitive",
                SubAgencyName = "Program Evaluation",
                TotalOpenings = "1",
                AdjudicationType = "Suitability/Fitness",
                RemoteEligible = true,
                FinancialDisclosure = false,
                SupervisoryStatus = false,
                RelocationAuthorized = false,
                DrugTestRequired = false,
                PositionSensitivityAndRisk = "Moderate Risk",
                ConditionsOfEmployment = "Occasional travel for site assessments.",
                RequiredDocuments = "Resume; supporting documents as requested.",
                HowToApply = "Apply online through USAJobs.",
                NextSteps = "Selected applicants may be asked for a writing sample.",
                AdditionalInformation = "Supports cross-component improvement initiatives.",
                HiringOrganizationId = opd.Id,
                PositionRemuneration = new() { MinimumRange = 82_764, MaximumRange = 128_956 }
            },
            new()
            {
                AnnouncementNumber = "DHS-OCFO-2026-005",
                Title = "Financial Analyst",
                Description = "Supports federal budget formulation and execution for the agency's appropriated funds.",
                Duties = "Prepares budget justifications and spending plans. Monitors obligations and expenditures against appropriations. Coordinates with OMB on passback and apportionment requests.",
                Qualifications = "GS-11: One year of specialized experience equivalent to GS-09 in federal budget or financial management.",
                Education = "Graduate education may qualify at lower grade combinations.",
                Evaluations = "Applicants will be evaluated on budget formulation and analytical ability.",
                KeyRequirements = "U.S. citizenship; background investigation.",
                PromotionPotential = "GS-12",
                IsOpen = true,
                OccupationalSeries = "0501",
                OccupationalSeriesTitle = "Financial Administration and Program",
                PayGradeMin = "GS-09",
                PayGradeMax = "GS-11",
                AppointmentType = AppointmentType.Permanent,
                PositionOfferingType = "Competitive Service",
                WorkSchedule = WorkSchedule.FullTime,
                OpenDate = today.AddDays(-1),
                CloseDate = today.AddDays(13),
                WhoMayApply = "Open to US Citizens",
                HiringPath = "public",
                DutyLocation = "Washington",
                DutyLocationState = "DC",
                TeleworkEligible = false,
                TravelRequired = TravelRequirement.NotRequired,
                SecurityClearance = SecurityClearance.PublicTrust,
                ServiceType = "Competitive",
                SubAgencyName = "Budget Division",
                TotalOpenings = "1",
                AdjudicationType = "Suitability/Fitness",
                RemoteEligible = false,
                FinancialDisclosure = false,
                SupervisoryStatus = false,
                RelocationAuthorized = false,
                DrugTestRequired = false,
                PositionSensitivityAndRisk = "Moderate Risk",
                ConditionsOfEmployment = "May require overtime during budget season.",
                RequiredDocuments = "Resume; transcripts if qualifying via education.",
                HowToApply = "Apply online through USAJobs.",
                NextSteps = "Referrals will be made to the selecting official.",
                AdditionalInformation = "Supports agency-wide budget execution and reporting.",
                HiringOrganizationId = fin.Id,
                PositionRemuneration = new() { MinimumRange = 68_405, MaximumRange = 107_590 }
            },
        };

        db.Positions.AddRange(positions);
        db.SaveChanges();
    }
}
