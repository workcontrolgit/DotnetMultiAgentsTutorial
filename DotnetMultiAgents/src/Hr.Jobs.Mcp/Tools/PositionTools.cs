// src/Hr.Jobs.Mcp/Tools/PositionTools.cs
using System.ComponentModel;
using Hr.Application.Services;
using ModelContextProtocol.Server;

namespace Hr.Jobs.Mcp.Tools;

[McpServerToolType]
public sealed class PositionTools(PositionService positions)
{
    [McpServerTool(Name = "GetOpenPositions"),
     Description("Returns all currently open federal job positions including title, pay grade, duty location, and security clearance requirements.")]
    public async Task<IEnumerable<object>> GetOpenPositions(CancellationToken ct = default)
    {
        var list = await positions.GetOpenPositionsAsync(ct);
        return list.Select(p => (object)new
        {
            p.Id,
            p.Title,
            p.Description,
            p.OccupationalSeries,
            p.PayGradeMin,
            p.PayGradeMax,
            MinimumRange = p.PositionRemuneration?.MinimumRange,
            MaximumRange = p.PositionRemuneration?.MaximumRange,
            RateIntervalCode = p.PositionRemuneration?.RateIntervalCode,
            p.DutyLocation,
            p.TeleworkEligible,
            SecurityClearance = p.SecurityClearance.ToString(),
            p.WhoMayApply,
            OrganizationName = p.HiringOrganization?.OrganizationName,
            DepartmentName = p.HiringOrganization?.DepartmentName
        });
    }

    [McpServerTool(Name = "GetPositionById"),
     Description("Returns full details for a specific position by its ID, including duties, qualifications, and pay information.")]
    public async Task<object?> GetPositionById(
        [Description("The numeric ID of the position to retrieve")] int positionId,
        CancellationToken ct = default)
    {
        var p = await positions.GetPositionByIdAsync(positionId, ct);
        if (p is null) return null;

        return new
        {
            p.Id,
            p.AnnouncementNumber,
            p.UsaJobsId,
            p.PositionUri,
            p.ApplyUri,
            p.Title,
            p.Description,
            p.Duties,
            p.Qualifications,
            p.Education,
            p.Evaluations,
            p.KeyRequirements,
            p.PromotionPotential,
            p.OccupationalSeries,
            p.OccupationalSeriesTitle,
            p.PayGradeMin,
            p.PayGradeMax,
            MinimumRange = p.PositionRemuneration?.MinimumRange,
            MaximumRange = p.PositionRemuneration?.MaximumRange,
            RateIntervalCode = p.PositionRemuneration?.RateIntervalCode,
            p.IsOpen,
            p.PositionOfferingType,
            p.DutyLocation,
            p.DutyLocationState,
            p.HiringPath,
            p.TeleworkEligible,
            p.RemoteEligible,
            SecurityClearance = p.SecurityClearance.ToString(),
            p.ServiceType,
            p.SubAgencyName,
            p.TotalOpenings,
            p.AdjudicationType,
            TravelRequired = p.TravelRequired.ToString(),
            AppointmentType = p.AppointmentType.ToString(),
            WorkSchedule = p.WorkSchedule.ToString(),
            p.WhoMayApply,
            p.FinancialDisclosure,
            p.SupervisoryStatus,
            p.RelocationAuthorized,
            p.DrugTestRequired,
            p.PositionSensitivityAndRisk,
            p.ContactName,
            p.ContactPhone,
            p.ContactEmail,
            p.ContactAddress,
            p.ConditionsOfEmployment,
            p.RequiredDocuments,
            p.HowToApply,
            p.NextSteps,
            p.AdditionalInformation,
            OpenDate = p.OpenDate.ToString("yyyy-MM-dd"),
            CloseDate = p.CloseDate?.ToString("yyyy-MM-dd"),
            OrganizationName = p.HiringOrganization?.OrganizationName,
            DepartmentName = p.HiringOrganization?.DepartmentName
        };
    }

    [McpServerTool(Name = "GetPositionsByOrganization"),
     Description("Returns all positions for a specific federal hiring organization. Use GetHiringOrganizations first to get valid organization IDs.")]
    public async Task<IEnumerable<object>> GetPositionsByOrganization(
        [Description("The numeric ID of the hiring organization")] int organizationId,
        CancellationToken ct = default)
    {
        var list = await positions.GetPositionsByOrganizationAsync(organizationId, ct);
        return list.Select(p => (object)new
        {
            p.Id,
            p.Title,
            p.Description,
            p.OccupationalSeries,
            p.PayGradeMin,
            p.PayGradeMax,
            p.IsOpen,
            p.DutyLocation,
            p.TeleworkEligible,
            SecurityClearance = p.SecurityClearance.ToString(),
            p.WhoMayApply
        });
    }
}
