// src/Hr.Infrastructure/Repositories/JobAnnouncementRepository.cs
using Hr.Core.Entities;
using Hr.Core.Enums;
using Hr.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Hr.Infrastructure.Repositories;

public class JobAnnouncementRepository(HrDbContext db) : IJobAnnouncementRepository
{
    public async Task<JobAnnouncement> SaveAsync(JobAnnouncement announcement, CancellationToken ct = default)
    {
        db.JobAnnouncements.Add(announcement);
        await db.SaveChangesAsync(ct);
        return announcement;
    }

    public Task<JobAnnouncement?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.JobAnnouncements
             .Include(a => a.Position)
             .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IEnumerable<JobAnnouncement>> GetByPositionAsync(int positionId, CancellationToken ct = default)
        => await db.JobAnnouncements
                   .Where(a => a.PositionId == positionId)
                   .OrderByDescending(a => a.GeneratedAt)
                   .ToListAsync(ct);

    public async Task<JobAnnouncement?> UpdateStatusAsync(
        int id, AnnouncementStatus status, string? complianceSummary, CancellationToken ct = default)
    {
        var announcement = await db.JobAnnouncements.FindAsync([id], ct);
        if (announcement is null) return null;

        announcement.Status             = status;
        announcement.ComplianceSummary  = complianceSummary;
        announcement.ComplianceCheckedAt = status is AnnouncementStatus.CompliancePassed
                                        or AnnouncementStatus.ComplianceFailed
            ? DateTime.UtcNow
            : announcement.ComplianceCheckedAt;

        await db.SaveChangesAsync(ct);
        return announcement;
    }
}
