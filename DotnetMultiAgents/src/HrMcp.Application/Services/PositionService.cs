// src/HrMcp.Application/Services/PositionService.cs
using HrMcp.Core.Entities;
using HrMcp.Core.Interfaces;

namespace HrMcp.Application.Services;

public class PositionService(IPositionRepository repo)
{
    public Task<IEnumerable<Position>> GetAllPositionsAsync(CancellationToken ct = default)
        => repo.GetAllAsync(ct);

    public Task<IEnumerable<Position>> GetOpenPositionsAsync(CancellationToken ct = default)
        => repo.GetOpenPositionsAsync(ct);

    public Task<Position?> GetPositionByIdAsync(int id, CancellationToken ct = default)
        => repo.GetByIdAsync(id, ct);

    public Task<IEnumerable<Position>> GetPositionsByOrganizationAsync(int organizationId, CancellationToken ct = default)
        => repo.GetByOrganizationAsync(organizationId, ct);
}
