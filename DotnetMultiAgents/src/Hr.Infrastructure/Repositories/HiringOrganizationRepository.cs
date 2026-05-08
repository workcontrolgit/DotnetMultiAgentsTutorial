// src/Hr.Infrastructure/Repositories/HiringOrganizationRepository.cs
using Hr.Core.Entities;
using Hr.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Hr.Infrastructure.Repositories;

public class HiringOrganizationRepository(HrDbContext db) : IHiringOrganizationRepository
{
    public async Task<IEnumerable<HiringOrganization>> GetAllAsync(CancellationToken ct = default)
        => await db.HiringOrganizations.Include(o => o.Positions).ToListAsync(ct);

    public Task<HiringOrganization?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.HiringOrganizations.Include(o => o.Positions).FirstOrDefaultAsync(o => o.Id == id, ct);
}
