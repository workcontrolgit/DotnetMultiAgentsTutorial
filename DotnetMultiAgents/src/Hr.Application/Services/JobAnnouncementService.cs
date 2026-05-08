// src/Hr.Application/Services/JobAnnouncementService.cs
using Hr.Core.Entities;
using Hr.Core.Enums;
using Hr.Core.Interfaces;

namespace Hr.Application.Services;

public class JobAnnouncementService(IJobAnnouncementRepository repo)
{
    public Task<JobAnnouncement> SaveDraftAsync(int positionId, string draftText, CancellationToken ct = default)
        => repo.SaveAsync(new JobAnnouncement
        {
            PositionId  = positionId,
            DraftText   = draftText,
            Status      = AnnouncementStatus.Draft,
            GeneratedAt = DateTime.UtcNow,
        }, ct);

    public Task<JobAnnouncement?> GetByIdAsync(int id, CancellationToken ct = default)
        => repo.GetByIdAsync(id, ct);

    public Task<IEnumerable<JobAnnouncement>> GetByPositionAsync(int positionId, CancellationToken ct = default)
        => repo.GetByPositionAsync(positionId, ct);

    public Task<JobAnnouncement?> UpdateStatusAsync(
        int id, AnnouncementStatus status, string? complianceSummary, CancellationToken ct = default)
        => repo.UpdateStatusAsync(id, status, complianceSummary, ct);
}
