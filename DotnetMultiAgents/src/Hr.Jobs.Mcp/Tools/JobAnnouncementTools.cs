// src/Hr.Jobs.Mcp/Tools/JobAnnouncementTools.cs
using System.ComponentModel;
using System.Text;
using Hr.Application.Services;
using Hr.Core.Enums;
using ModelContextProtocol.Server;

namespace Hr.Jobs.Mcp.Tools;

[McpServerToolType]
public sealed class JobAnnouncementTools(JobAnnouncementService announcements)
{
    [McpServerTool(Name = "SaveJobAnnouncement"),
     Description("Persists a generated job announcement draft to the database. Call this after WriteJobDescription returns the draft text. Returns the new announcement ID and its initial Draft status.")]
    public async Task<string> SaveJobAnnouncement(
        [Description("The numeric ID of the position this announcement is for")] int positionId,
        [Description("The full markdown announcement text returned by WriteJobDescription")] string draftText,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(draftText))
            return "Error: draftText is empty. Generate the announcement with WriteJobDescription first.";

        var saved = await announcements.SaveDraftAsync(positionId, draftText, ct);
        return $"Announcement saved. ID: {saved.Id} | Position: {positionId} | Status: {saved.Status} | Generated: {saved.GeneratedAt:yyyy-MM-dd HH:mm} UTC";
    }

    [McpServerTool(Name = "GetJobAnnouncement"),
     Description("Retrieves a saved job announcement by its ID, including its current status and compliance summary if available.")]
    public async Task<string> GetJobAnnouncement(
        [Description("The announcement ID returned by SaveJobAnnouncement")] int announcementId,
        CancellationToken ct = default)
    {
        var a = await announcements.GetByIdAsync(announcementId, ct);
        if (a is null) return $"Announcement {announcementId} not found.";

        var sb = new StringBuilder();
        sb.AppendLine($"Announcement ID:   {a.Id}");
        sb.AppendLine($"Position:          {a.Position?.Title ?? $"ID {a.PositionId}"}");
        sb.AppendLine($"Status:            {a.Status}");
        sb.AppendLine($"Generated:         {a.GeneratedAt:yyyy-MM-dd HH:mm} UTC");
        if (a.ComplianceCheckedAt.HasValue)
            sb.AppendLine($"Compliance check:  {a.ComplianceCheckedAt:yyyy-MM-dd HH:mm} UTC");
        if (!string.IsNullOrWhiteSpace(a.ComplianceSummary))
            sb.AppendLine($"Compliance notes:  {a.ComplianceSummary}");
        sb.AppendLine();
        sb.Append(a.DraftText);

        return sb.ToString();
    }

    [McpServerTool(Name = "ListJobAnnouncements"),
     Description("Lists all saved job announcement drafts for a position, newest first. Shows ID, status, and generated date — does not return full draft text.")]
    public async Task<string> ListJobAnnouncements(
        [Description("The numeric ID of the position")] int positionId,
        CancellationToken ct = default)
    {
        var list = (await announcements.GetByPositionAsync(positionId, ct)).ToList();
        if (list.Count == 0) return $"No announcements found for position {positionId}.";

        var sb = new StringBuilder();
        sb.AppendLine($"Announcements for position {positionId} ({list.Count} total):");
        foreach (var a in list)
        {
            sb.Append($"  ID {a.Id,4} | {a.Status,-17} | {a.GeneratedAt:yyyy-MM-dd HH:mm}");
            if (a.ComplianceCheckedAt.HasValue)
                sb.Append($" | checked {a.ComplianceCheckedAt:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    [McpServerTool(Name = "UpdateAnnouncementStatus"),
     Description("Updates the compliance status of a saved announcement. Call this after RunFullComplianceCheck completes. Valid statuses: CompliancePassed, ComplianceFailed, Published.")]
    public async Task<string> UpdateAnnouncementStatus(
        [Description("The announcement ID to update")] int announcementId,
        [Description("New status: CompliancePassed, ComplianceFailed, or Published")] string status,
        [Description("Plain-language summary of the compliance outcome to store alongside the draft")] string? complianceSummary = null,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<AnnouncementStatus>(status, ignoreCase: true, out var parsed))
            return $"Invalid status '{status}'. Valid values: CompliancePassed, ComplianceFailed, Published.";

        if (parsed == AnnouncementStatus.Draft)
            return "Cannot set status back to Draft. Use CompliancePassed, ComplianceFailed, or Published.";

        var updated = await announcements.UpdateStatusAsync(announcementId, parsed, complianceSummary, ct);
        if (updated is null) return $"Announcement {announcementId} not found.";

        return $"Announcement {announcementId} updated to {updated.Status}." +
               (updated.ComplianceCheckedAt.HasValue
                   ? $" Compliance recorded at {updated.ComplianceCheckedAt:yyyy-MM-dd HH:mm} UTC."
                   : string.Empty);
    }
}
