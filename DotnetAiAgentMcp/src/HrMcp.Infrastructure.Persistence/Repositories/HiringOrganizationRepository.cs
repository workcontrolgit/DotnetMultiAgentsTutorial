// src/HrMcp.Infrastructure.Persistence/Repositories/HiringOrganizationRepository.cs
using HrMcp.Core.Entities;
using HrMcp.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HrMcp.Infrastructure.Persistence.Repositories;

public class HiringOrganizationRepository(HrDbContext db) : IHiringOrganizationRepository
{
    public async Task<IEnumerable<HiringOrganization>> GetAllAsync(CancellationToken ct = default)
        => await db.HiringOrganizations.Include(o => o.Positions).ToListAsync(ct);

    public Task<HiringOrganization?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.HiringOrganizations.Include(o => o.Positions).FirstOrDefaultAsync(o => o.Id == id, ct);
}
