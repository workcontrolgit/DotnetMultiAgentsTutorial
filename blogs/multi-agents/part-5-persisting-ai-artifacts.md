# Part 5 — Persisting AI Artifacts: The JobAnnouncement Lifecycle

*Part 5 of: Building Multi-Agent Systems with .NET 10*

---

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · **Part 5** · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 4 — Compliance MCP: Deterministic Rules, Zero LLM](part-4-compliance-mcp-deterministic-rules.md) &nbsp;|&nbsp; [Part 6 — The Selector Pattern →](part-6-selector-pattern.md)

*Medium: [← Part 4](MEDIUM_URL_PART_4) | [Part 6 →](MEDIUM_URL_PART_6)*

---

`WriteJobDescription` returns a string. When the conversation ends, the string disappears.

This is the most overlooked problem in agent application design. You invest token budget generating a high-quality job announcement, the compliance check passes, the HR specialist approves it — and then the session closes and nothing was saved. The next conversation starts from zero.

AI-generated artifacts need a lifecycle: they must be created, stored, status-tracked, and retrievable. This post walks through the full implementation — entity, repository, service, MCP tools, EF migration — and shows why the clean architecture from Part 2 makes this addition straightforward.

---

## The Design Decision

Three options for persisting a draft job announcement were considered:

**Option A — Field on Position.** Add `DraftJobDescription` and `DraftGeneratedAt` columns to the `Positions` table. Simplest, but mixes source data (what the position is) with the generated artifact (what was written about it). Only one draft per position, no history.

**Option B — Separate entity.** A `JobAnnouncement` has its own table, its own status enum, and a foreign key to `Position`. A position can have many drafts over time. Status transitions are explicit. History is auditable.

**Option C — Cross-server records.** The compliance server writes its own result record; the HR server writes the announcement. Two tables, two migrations, orchestrator coordination required.

Option B is the right balance for a tutorial: realistic enough to teach the pattern, simple enough to implement in one post.

---

## The AnnouncementStatus Enum

The lifecycle has four states:

```csharp
// src/Hr.Core/Enums/AnnouncementStatus.cs
public enum AnnouncementStatus
{
    Draft,             // generated, not yet checked
    CompliancePassed,  // all 7 OPM rules passed
    ComplianceFailed,  // one or more rules failed
    Published          // approved and posted on USAJobs
}
```

The state machine is linear with one branch:

```
Draft
  ├── CompliancePassed → Published
  └── ComplianceFailed → (revise → Draft → ...)
```

You cannot set status back to `Draft` via `UpdateAnnouncementStatus`. Revision means generating a new draft — a new row in the table.

---

## The JobAnnouncement Entity

```csharp
// src/Hr.Core/Entities/JobAnnouncement.cs
public class JobAnnouncement
{
    public int Id { get; set; }
    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;
    public string DraftText { get; set; } = string.Empty;
    public AnnouncementStatus Status { get; set; } = AnnouncementStatus.Draft;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ComplianceCheckedAt { get; set; }
    public string? ComplianceSummary { get; set; }
}
```

`ComplianceSummary` stores a plain-language explanation of the compliance outcome written by the OPMCompliance agent — the human-readable version of what `RunFullComplianceCheck` returned.

---

## The Repository

```csharp
// src/Hr.Infrastructure/Repositories/JobAnnouncementRepository.cs
public class JobAnnouncementRepository(HrDbContext db) : IJobAnnouncementRepository
{
    public async Task<JobAnnouncement> SaveAsync(JobAnnouncement announcement, CancellationToken ct = default)
    {
        db.JobAnnouncements.Add(announcement);
        await db.SaveChangesAsync(ct);
        return announcement; // Id is populated by EF after SaveChanges
    }

    public async Task<JobAnnouncement?> UpdateStatusAsync(
        int id, AnnouncementStatus status, string? complianceSummary, CancellationToken ct = default)
    {
        var announcement = await db.JobAnnouncements.FindAsync([id], ct);
        if (announcement is null) return null;

        announcement.Status            = status;
        announcement.ComplianceSummary = complianceSummary;
        announcement.ComplianceCheckedAt = status is AnnouncementStatus.CompliancePassed
                                        or AnnouncementStatus.ComplianceFailed
            ? DateTime.UtcNow : announcement.ComplianceCheckedAt;

        await db.SaveChangesAsync(ct);
        return announcement;
    }
}
```

`ComplianceCheckedAt` is only set when the status transitions to `CompliancePassed` or `ComplianceFailed`, not when it moves to `Published`. This preserves the original check timestamp through the publish step.

---

## The EF Core Migration

After adding `JobAnnouncements` to `HrDbContext` and implementing the repository, generate the migration:

```bash
dotnet ef migrations add AddJobAnnouncement \
  --project src/Hr.Infrastructure \
  --startup-project src/Hr.Jobs.Mcp
```

The generated migration creates the table with the correct constraints:

```csharp
migrationBuilder.CreateTable(
    name: "JobAnnouncements",
    columns: table => new
    {
        Id                  = table.Column<int>(nullable: false)
                                   .Annotation("SqlServer:Identity", "1, 1"),
        PositionId          = table.Column<int>(nullable: false),
        DraftText           = table.Column<string>(nullable: false),
        Status              = table.Column<int>(nullable: false),
        GeneratedAt         = table.Column<DateTime>(nullable: false),
        ComplianceCheckedAt = table.Column<DateTime>(nullable: true),
        ComplianceSummary   = table.Column<string>(nullable: true)
    },
    constraints: table =>
    {
        table.PrimaryKey("PK_JobAnnouncements", x => x.Id);
        table.ForeignKey(
            name: "FK_JobAnnouncements_Positions_PositionId",
            column: x => x.PositionId,
            principalTable: "Positions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    });
```

Apply the migration:

```bash
dotnet ef database update \
  --project src/Hr.Infrastructure \
  --startup-project src/Hr.Jobs.Mcp
```

The `JobAnnouncements` table is now live. Existing data is untouched.

---

## The Four MCP Tools

`JobAnnouncementTools` exposes four operations that span the lifecycle:

**SaveJobAnnouncement** — called by the JobDescription agent immediately after `WriteJobDescription` returns:

```csharp
[McpServerTool(Name = "SaveJobAnnouncement"),
 Description("Persists a generated job announcement draft. Call after WriteJobDescription. Returns the new announcement ID.")]
public async Task<string> SaveJobAnnouncement(int positionId, string draftText, ...)
{
    var saved = await announcements.SaveDraftAsync(positionId, draftText, ct);
    return $"Announcement saved. ID: {saved.Id} | Status: {saved.Status} | Generated: {saved.GeneratedAt:yyyy-MM-dd HH:mm} UTC";
}
```

**GetJobAnnouncement** — retrieves a draft by ID including full text and compliance notes:

```csharp
[McpServerTool(Name = "GetJobAnnouncement"),
 Description("Retrieves a saved announcement by ID, including status and compliance summary.")]
public async Task<string> GetJobAnnouncement(int announcementId, ...)
```

**ListJobAnnouncements** — shows all drafts for a position, newest first, without returning full text:

```csharp
[McpServerTool(Name = "ListJobAnnouncements"),
 Description("Lists all drafts for a position. Shows ID, status, and date. Does not return full text.")]
public async Task<string> ListJobAnnouncements(int positionId, ...)
```

**UpdateAnnouncementStatus** — called by the OPMCompliance agent after `RunFullComplianceCheck`:

```csharp
[McpServerTool(Name = "UpdateAnnouncementStatus"),
 Description("Updates compliance status of a saved announcement. Valid statuses: CompliancePassed, ComplianceFailed, Published.")]
public async Task<string> UpdateAnnouncementStatus(
    int announcementId, string status, string? complianceSummary = null, ...)
{
    if (!Enum.TryParse<AnnouncementStatus>(status, ignoreCase: true, out var parsed))
        return $"Invalid status '{status}'. Valid values: CompliancePassed, ComplianceFailed, Published.";

    if (parsed == AnnouncementStatus.Draft)
        return "Cannot set status back to Draft. Generate a new announcement instead.";

    var updated = await announcements.UpdateStatusAsync(announcementId, parsed, complianceSummary, ct);
    return $"Announcement {announcementId} updated to {updated!.Status}.";
}
```

The guard against setting status back to `Draft` is in the tool, not just the service. The LLM will sometimes try — this message corrects it.

---

## End-to-End Workflow

With both MCP servers running, test the complete lifecycle through MCP Inspector:

**Step 1** — call `WriteJobDescription(positionId: 5)` → get draft markdown text

**Step 2** — call `SaveJobAnnouncement(positionId: 5, draftText: "...")` → get `ID: 3 | Status: Draft`

**Step 3** — call `RunFullComplianceCheck(positionId: 5)` on the compliance server → get report

**Step 4** — call `UpdateAnnouncementStatus(announcementId: 3, status: "CompliancePassed", complianceSummary: "All 7 rules passed. Pay grade GS-12 is within the allowed range for series 2210.")` → get confirmation

**Step 5** — call `GetJobAnnouncement(announcementId: 3)` → get the full draft with compliance notes attached

**Step 6** — call `UpdateAnnouncementStatus(announcementId: 3, status: "Published")` → move to final state

The design principle at work: **the agent writes the artifact; the database owns the truth.** The LLM session can end at any step. The announcement is safe in `HrMcpDb` and will be there when the next conversation asks for it.

---

The persistence layer is in place and all 14 tools work end to end. The next problem is quality: a single agent holding all 14 tools writes worse job descriptions than one focused on writing alone. Part 6 introduces the Selector pattern — a router that classifies each user turn and delegates it to one specialist with a scoped tool set and a focused system prompt.

---

**Series:** [Preface](preface-why-one-agent-is-not-enough.md) · [Part 1](part-1-dotnet-agent-framework.md) · [Part 2](part-2-clean-architecture-for-ai.md) · [Part 3](part-3-hr-data-mcp-server.md) · [Part 4](part-4-compliance-mcp-deterministic-rules.md) · **Part 5** · [Part 6](part-6-selector-pattern.md) · [Part 7](part-7-claude-desktop-multi-agent.md) · [Part 8](part-8-pipe-pattern.md) · [Part 9](part-9-group-chat-pattern.md) · [Part 10](part-10-evaluator-optimizer-pattern.md)

← [Part 4 — Compliance MCP: Deterministic Rules, Zero LLM](part-4-compliance-mcp-deterministic-rules.md) &nbsp;|&nbsp; [Part 6 — The Selector Pattern →](part-6-selector-pattern.md)

*Medium: [← Part 4](MEDIUM_URL_PART_4) | [Part 6 →](MEDIUM_URL_PART_6)*

---

## References

### NuGet Packages

- [Microsoft.EntityFrameworkCore](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore) — `JobAnnouncement` entity, `HrDbContext`, migrations
- [Microsoft.EntityFrameworkCore.SqlServer](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer) — SQL Server LocalDB provider
- [Microsoft.EntityFrameworkCore.Tools](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Tools) — `dotnet ef migrations add` and `dotnet ef database update`

### Microsoft Documentation

- [EF Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/) — Adding, applying, and rolling back schema migrations
- [EF Core Relationships](https://learn.microsoft.com/en-us/ef/core/modeling/relationships) — FK from `JobAnnouncement` to `Position`, cascade delete

### GitHub

- [DotnetMultiAgentsTutorial](https://github.com/workcontrolgit/DotnetMultiAgentsTutorial) — Full source for all patterns in this series
