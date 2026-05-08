// src/Hr.Core/Interfaces/IPositionRepository.cs
using Hr.Core.Entities;

namespace Hr.Core.Interfaces;

public interface IPositionRepository
{
    Task<IEnumerable<Position>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<Position>> GetOpenPositionsAsync(CancellationToken ct = default);
    Task<Position?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<Position>> GetByOrganizationAsync(int organizationId, CancellationToken ct = default);
}
