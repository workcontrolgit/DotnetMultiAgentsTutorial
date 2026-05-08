// src/Hr.Infrastructure/Repositories/PositionRepository.cs
using Hr.Core.Entities;
using Hr.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Hr.Infrastructure.Repositories;

public class PositionRepository(HrDbContext db) : IPositionRepository
{
    private IQueryable<Position> BaseQuery =>
        db.Positions
          .Include(p => p.HiringOrganization)
          .Include(p => p.PositionRemuneration);

    public async Task<IEnumerable<Position>> GetAllAsync(CancellationToken ct = default)
        => await BaseQuery.ToListAsync(ct);

    public async Task<IEnumerable<Position>> GetOpenPositionsAsync(CancellationToken ct = default)
        => await BaseQuery.Where(p => p.IsOpen).ToListAsync(ct);

    public Task<Position?> GetByIdAsync(int id, CancellationToken ct = default)
        => BaseQuery.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IEnumerable<Position>> GetByOrganizationAsync(int organizationId, CancellationToken ct = default)
        => await BaseQuery.Where(p => p.HiringOrganizationId == organizationId).ToListAsync(ct);

    public async Task<IEnumerable<Position>> GetBySeriesAsync(string occupationalSeries, CancellationToken ct = default)
        => await BaseQuery.Where(p => p.OccupationalSeries == occupationalSeries).ToListAsync(ct);
}
