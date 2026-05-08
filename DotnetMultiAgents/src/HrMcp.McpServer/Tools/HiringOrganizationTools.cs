// src/HrMcp.McpServer/Tools/HiringOrganizationTools.cs
using System.ComponentModel;
using HrMcp.Application.Services;
using ModelContextProtocol.Server;

namespace HrMcp.McpServer.Tools;

[McpServerToolType]
public sealed class HiringOrganizationTools(HiringOrganizationService organizations)
{
    [McpServerTool(Name = "GetHiringOrganizations"),
     Description("Returns all federal hiring organizations in the database with their department affiliations, IDs, and open position count.")]
    public async Task<IEnumerable<object>> GetHiringOrganizations(CancellationToken ct = default)
    {
        var list = await organizations.GetAllOrganizationsAsync(ct);
        return list.Select(o => (object)new
        {
            o.Id,
            o.OrganizationName,
            o.DepartmentName,
            o.AgencyDescription,
            OpenPositionCount = o.Positions.Count(p => p.IsOpen)
        });
    }
}
