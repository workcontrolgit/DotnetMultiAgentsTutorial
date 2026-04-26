// src/HrMcp.Core/Interfaces/IHiringOrganizationRepository.cs
using HrMcp.Core.Entities;

namespace HrMcp.Core.Interfaces;

public interface IHiringOrganizationRepository
{
    Task<IEnumerable<HiringOrganization>> GetAllAsync(CancellationToken ct = default);
    Task<HiringOrganization?> GetByIdAsync(int id, CancellationToken ct = default);
}
