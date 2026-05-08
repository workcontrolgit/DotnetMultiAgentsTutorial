// src/Hr.Core/Entities/Position.cs
using Hr.Core.Enums;

namespace Hr.Core.Entities;

public class Position
{
    public int Id { get; set; }

    // USAJobs: PositionTitle
    public string Title { get; set; } = string.Empty;

    // USAJobs: JobSummary — shown in search results
    public string Description { get; set; } = string.Empty;

    // USAJobs: MajorDuties — full narrative of what the employee does
    public string Duties { get; set; } = string.Empty;

    // USAJobs: Qualifications — OPM minimum qualifications + specialized experience
    public string Qualifications { get; set; } = string.Empty;

    public bool IsOpen { get; set; }

    // USAJobs: JobCategories[].Code — 4-digit OPM series e.g. "2210" = IT Management
    public string OccupationalSeries { get; set; } = string.Empty;

    // USAJobs: PayPlan + LowGrade / HighGrade e.g. "GS-09", "GS-11"
    public string PayGradeMin { get; set; } = string.Empty;
    public string PayGradeMax { get; set; } = string.Empty;

    // USAJobs: appointmentType
    public AppointmentType AppointmentType { get; set; } = AppointmentType.Permanent;

    // USAJobs: workSchedule
    public WorkSchedule WorkSchedule { get; set; } = WorkSchedule.FullTime;

    // USAJobs: positionOpenDate / positionCloseDate
    public DateTime OpenDate { get; set; } = DateTime.UtcNow;
    public DateTime? CloseDate { get; set; }

    // USAJobs: WhoMayApply.Name
    public string WhoMayApply { get; set; } = "Open to US Citizens";

    // USAJobs: PositionLocation[].CityName + CountrySubDivisionCode
    public string DutyLocation { get; set; } = string.Empty;

    // USAJobs: teleworkEligible (Y/N in API)
    public bool TeleworkEligible { get; set; } = false;

    // USAJobs: travelRequirement
    public TravelRequirement TravelRequired { get; set; } = TravelRequirement.NotRequired;

    // USAJobs: securityClearance
    public SecurityClearance SecurityClearance { get; set; } = SecurityClearance.NotRequired;

    // USAJobs: supervisoryStatus (Y/N in API)
    public bool SupervisoryStatus { get; set; } = false;

    // USAJobs: relocationExpensesReimbursed (Y/N in API)
    public bool RelocationAuthorized { get; set; } = false;

    // USAJobs: drugTestRequired (Y/N in API)
    public bool DrugTestRequired { get; set; } = false;

    // Navigation
    public int HiringOrganizationId { get; set; }
    public HiringOrganization HiringOrganization { get; set; } = null!;
    public PositionRemuneration PositionRemuneration { get; set; } = null!;
}
