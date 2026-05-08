// src/Hr.Core/Interfaces/IHiringOrganizationRepository.cs
using Hr.Core.Entities;

namespace Hr.Core.Interfaces;

public interface IHiringOrganizationRepository
{
    Task<IEnumerable<HiringOrganization>> GetAllAsync(CancellationToken ct = default);
    Task<HiringOrganization?> GetByIdAsync(int id, CancellationToken ct = default);
}
