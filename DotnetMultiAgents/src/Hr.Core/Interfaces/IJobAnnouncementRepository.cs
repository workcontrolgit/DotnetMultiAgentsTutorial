// src/Hr.Core/Interfaces/IJobAnnouncementRepository.cs
using Hr.Core.Entities;
using Hr.Core.Enums;

namespace Hr.Core.Interfaces;

public interface IJobAnnouncementRepository
{
    Task<JobAnnouncement> SaveAsync(JobAnnouncement announcement, CancellationToken ct = default);
    Task<JobAnnouncement?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<JobAnnouncement>> GetByPositionAsync(int positionId, CancellationToken ct = default);
    Task<JobAnnouncement?> UpdateStatusAsync(int id, AnnouncementStatus status, string? complianceSummary, CancellationToken ct = default);
}
