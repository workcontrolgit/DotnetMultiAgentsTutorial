// src/HrMcp.Application/Services/HiringOrganizationService.cs
using HrMcp.Core.Entities;
using HrMcp.Core.Interfaces;

namespace HrMcp.Application.Services;

public class HiringOrganizationService(IHiringOrganizationRepository repo)
{
    public Task<IEnumerable<HiringOrganization>> GetAllOrganizationsAsync(CancellationToken ct = default)
        => repo.GetAllAsync(ct);

    public Task<HiringOrganization?> GetOrganizationByIdAsync(int id, CancellationToken ct = default)
        => repo.GetByIdAsync(id, ct);
}
