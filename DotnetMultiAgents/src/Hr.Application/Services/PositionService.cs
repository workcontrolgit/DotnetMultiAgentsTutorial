// src/Hr.Application/Services/PositionService.cs
using Hr.Core.Entities;
using Hr.Core.Interfaces;

namespace Hr.Application.Services;

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
