// src/Hr.Application/Services/HiringOrganizationService.cs
using Hr.Core.Entities;
using Hr.Core.Interfaces;

namespace Hr.Application.Services;

public class HiringOrganizationService(IHiringOrganizationRepository repo)
{
    public Task<IEnumerable<HiringOrganization>> GetAllOrganizationsAsync(CancellationToken ct = default)
        => repo.GetAllAsync(ct);

    public Task<HiringOrganization?> GetOrganizationByIdAsync(int id, CancellationToken ct = default)
        => repo.GetByIdAsync(id, ct);
}
