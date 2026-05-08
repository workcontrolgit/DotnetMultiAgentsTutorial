// src/HrMcp.Core/Entities/HiringOrganization.cs
namespace HrMcp.Core.Entities;

public class HiringOrganization
{
    public int Id { get; set; }

    // USAJobs: OrganizationName — the hiring office
    public string OrganizationName { get; set; } = string.Empty;

    // USAJobs: DepartmentName — cabinet-level parent agency
    public string DepartmentName { get; set; } = string.Empty;

    public string AgencyDescription { get; set; } = string.Empty;

    public ICollection<Position> Positions { get; set; } = [];
}
