# Part 1: Clean Architecture Foundation with HR Domain

**Series:** AI Agents & MCP with .NET 10 | **Part 1 of 5**  
**GitHub:** [workcontrolgit/DotnetAiAgentMcp](https://github.com/workcontrolgit/DotnetAiAgentMcp)

---

## Introduction

Before we wire up AI agents and MCP servers, we need solid ground to stand on. In this first post, we build the Clean Architecture foundation — a federal HR domain modeled after **USAJobs.gov**, the official US federal government job board.

Why USAJobs? Federal job announcements have a richer, more structured data model than commercial job boards. They require occupational series codes, GS pay grades, security clearance levels, duty locations, open/close application windows, and OPM-standard qualifications text. That richness gives our AI agent meaningful context when generating job postings in Parts 3 and 4.

By the end of this post you will have:

- A .NET 10 solution with 5 projects wired together correctly
- A domain model with entity and field names aligned to the [USAJobs API schema](https://developer.usajobs.gov/api-reference/)
- Application services the MCP tools will call in Part 3
- EF Core + SQL Server 2025 LocalDB with an initial migration
- 4 federal hiring organizations and 5 realistic USAJobs-format positions seeded and ready to query

---

## Why Clean Architecture for AI-Enabled Apps?

Traditional layered apps put the database at the bottom and business logic in the middle. When you add AI, you need to invert that thinking: the **domain model becomes the contract that the AI calls through** — whether via function calling, tool use, or MCP.

Clean Architecture enforces this by making the **Core** (domain + application) independent of any infrastructure, including databases *and* AI runtimes. That separation means:

- You can swap Ollama for Claude API without touching business logic
- MCP tools are thin wrappers over Application services — no domain logic leaks in
- Unit-testing use cases requires no database, no AI runtime, no HTTP server

Here is how the layers relate in this project:

```text
┌─────────────────────────────────────────────────────────────────┐
│      HrMcp.McpServer                 HrMcp.Agent               │
│      (MCP tools, HTTP)               (Console agent)           │
└────────────────────────┬────────────────────────────────────────┘
                         │  depends on
┌────────────────────────▼────────────────────────────────────────┐
│                    HrMcp.Application                            │
│       (PositionService, HiringOrganizationService)              │
└────────────────────────┬────────────────────────────────────────┘
                         │  depends on
┌────────────────────────▼────────────────────────────────────────┐
│                      HrMcp.Core                                 │
│       (Entities, Enums, Repository Interfaces)                  │
└────────────────────────▲────────────────────────────────────────┘
                         │  implements
┌────────────────────────┴────────────────────────────────────────┐
│            HrMcp.Infrastructure.Persistence                     │
│                 (EF Core, SQL Server)                           │
└─────────────────────────────────────────────────────────────────┘
```

**Key rule:** AI infrastructure (Ollama client, MCP SDK) lives only in the outermost projects. Core and Application have zero knowledge of AI.

---

## Understanding Federal Job Announcement Fields

Before writing code, it helps to understand what USAJobs requires. Every posting on [usajobs.gov](https://www.usajobs.gov) contains these fields — and our domain model maps to them directly using the **official USAJobs API field names** from [developer.usajobs.gov](https://developer.usajobs.gov/api-reference/):

| USAJobs API Field | Our Domain Property | Notes |
|---|---|---|
| `OrganizationName` | `HiringOrganization.OrganizationName` | The hiring office e.g. "Office of IT" |
| `DepartmentName` | `HiringOrganization.DepartmentName` | Cabinet-level parent e.g. "Dept of Defense" |
| `PositionTitle` | `Position.Title` | Official job title |
| `JobSummary` | `Position.Description` | Shown in search results |
| `MajorDuties` | `Position.Duties` | Full duties narrative |
| `Qualifications` | `Position.Qualifications` | OPM minimum quals text |
| `JobCategories[].Code` | `Position.OccupationalSeries` | 4-digit code e.g. "2210" |
| `PayPlan` + `LowGrade` | `Position.PayGradeMin` | e.g. "GS-09" |
| `PayPlan` + `HighGrade` | `Position.PayGradeMax` | e.g. "GS-11" |
| `PositionRemuneration[].MinimumRange` | `PositionRemuneration.MinimumRange` | Dollar amount |
| `PositionRemuneration[].MaximumRange` | `PositionRemuneration.MaximumRange` | Dollar amount |
| `PositionRemuneration[].RateIntervalCode` | `PositionRemuneration.RateIntervalCode` | `"PA"` = Per Annum |
| `appointmentType` | `Position.AppointmentType` | Permanent / Temporary / Term |
| `workSchedule` | `Position.WorkSchedule` | Full-time / Part-time |
| `positionOpenDate` | `Position.OpenDate` | |
| `positionCloseDate` | `Position.CloseDate` | Nullable |
| `WhoMayApply.Name` | `Position.WhoMayApply` | |
| `PositionLocation[].CityName` | `Position.DutyLocation` | |
| `teleworkEligible` | `Position.TeleworkEligible` | bool |
| `travelRequirement` | `Position.TravelRequired` | enum |
| `securityClearance` | `Position.SecurityClearance` | enum |
| `supervisoryStatus` | `Position.SupervisoryStatus` | bool |
| `relocationExpensesReimbursed` | `Position.RelocationAuthorized` | bool |
| `drugTestRequired` | `Position.DrugTestRequired` | bool |

---

## Prerequisites

| Tool | Version | Check |
|------|---------|-------|
| .NET SDK | 10.0 or later | `dotnet --version` |
| SQL Server 2025 LocalDB | Ships with VS 2026 | `sqllocaldb info` |
| EF Core CLI tools | Latest | `dotnet ef --version` |
| VS Code or Visual Studio | 2026+ | — |
| Git | Any | `git --version` |

Install EF Core CLI tools if missing:

```bash
dotnet tool install --global dotnet-ef
```

Install SQL Server 2025 LocalDB (ships with Visual Studio 2026 via the *Data Storage and Processing* workload):

```bash
# Windows only, via winget
winget install Microsoft.SQLServer.2025.LocalDB
```

> **Note:** Visual Studio 2026 initially installs an RC build of SQL Server 2025 LocalDB. Apply **Cumulative Update 3 (CU3)** if you need the new `REGEXP_*` functions or `VECTOR` data type. For this tutorial the base RC build is sufficient.

---

## Step 1 — Scaffold the Solution

```bash
mkdir DotnetAiAgentMcp && cd DotnetAiAgentMcp

dotnet new sln -n DotnetAiAgentMcp

dotnet new classlib -n HrMcp.Core                       -o src/HrMcp.Core
dotnet new classlib -n HrMcp.Application                -o src/HrMcp.Application
dotnet new classlib -n HrMcp.Infrastructure.Persistence -o src/HrMcp.Infrastructure.Persistence
dotnet new webapi   -n HrMcp.McpServer                  -o src/HrMcp.McpServer
dotnet new console  -n HrMcp.Agent                      -o src/HrMcp.Agent

dotnet sln add src/HrMcp.Core
dotnet sln add src/HrMcp.Application
dotnet sln add src/HrMcp.Infrastructure.Persistence
dotnet sln add src/HrMcp.McpServer
dotnet sln add src/HrMcp.Agent
```

Wire project references — dependencies always flow **inward** toward Core:

```bash
dotnet add src/HrMcp.Application reference src/HrMcp.Core
dotnet add src/HrMcp.Infrastructure.Persistence reference src/HrMcp.Core
dotnet add src/HrMcp.McpServer reference src/HrMcp.Application
dotnet add src/HrMcp.McpServer reference src/HrMcp.Infrastructure.Persistence
dotnet add src/HrMcp.Agent reference src/HrMcp.Application
dotnet add src/HrMcp.Agent reference src/HrMcp.Infrastructure.Persistence
```

```bash
rm src/HrMcp.Core/Class1.cs
rm src/HrMcp.Application/Class1.cs
rm src/HrMcp.Infrastructure.Persistence/Class1.cs

dotnet build DotnetAiAgentMcp.slnx   # 0 errors
```

> **Note:** .NET 10's `dotnet new sln` creates the newer `.slnx` format by default. All `dotnet build` commands in this series use `.slnx`.

---

## Step 2 — Domain Models (`HrMcp.Core`)

Zero NuGet dependencies. Entities, enums, and repository interfaces only.

```text
src/HrMcp.Core/
  Entities/
    HiringOrganization.cs
    Position.cs
    PositionRemuneration.cs
  Enums/
    AppointmentType.cs
    WorkSchedule.cs
    SecurityClearance.cs
    TravelRequirement.cs
  Interfaces/
    IHiringOrganizationRepository.cs
    IPositionRepository.cs
```

### `HiringOrganization.cs`

Maps to the USAJobs `OrganizationName` and `DepartmentName` fields. USAJobs distinguishes two levels:
- **`OrganizationName`** — the actual hiring office (e.g., "Space and Naval Warfare Systems Command")
- **`DepartmentName`** — the cabinet-level parent agency (e.g., "Department of the Navy")

```csharp
// src/HrMcp.Core/Entities/HiringOrganization.cs
namespace HrMcp.Core.Entities;

public class HiringOrganization
{
    public int Id { get; set; }

    // USAJobs: OrganizationName — the hiring office
    public string OrganizationName { get; set; } = string.Empty;

    // USAJobs: DepartmentName — cabinet-level parent agency
    public string DepartmentName { get; set; } = string.Empty;

    public string AgencyDescription { get; set; } = string.Empty;

    public ICollection<Position> Positions { get; set; } = [];
}
```

### Enums

```csharp
// src/HrMcp.Core/Enums/AppointmentType.cs
// USAJobs API: appointmentType
namespace HrMcp.Core.Enums;

public enum AppointmentType { Permanent, Temporary, Term }
```

```csharp
// src/HrMcp.Core/Enums/WorkSchedule.cs
// USAJobs API: workSchedule
namespace HrMcp.Core.Enums;

public enum WorkSchedule { FullTime, PartTime, Intermittent, MultipleSchedules }
```

```csharp
// src/HrMcp.Core/Enums/SecurityClearance.cs
// USAJobs API: securityClearance
namespace HrMcp.Core.Enums;

public enum SecurityClearance
{
    NotRequired,
    PublicTrust,
    Confidential,
    Secret,
    TopSecret,
    TopSecretSCI    // TS/SCI
}
```

```csharp
// src/HrMcp.Core/Enums/TravelRequirement.cs
// USAJobs API: travelRequirement
namespace HrMcp.Core.Enums;

public enum TravelRequirement
{
    NotRequired,
    Occasional,     // 25% or less
    Sometimes,      // up to 50%
    Frequent        // 75%+
}
```

### `Position.cs`

Every property maps to a documented USAJobs API field.

```csharp
// src/HrMcp.Core/Entities/Position.cs
using HrMcp.Core.Enums;

namespace HrMcp.Core.Entities;

public class Position
{
    public int Id { get; set; }

    // USAJobs: PositionTitle
    public string Title { get; set; } = string.Empty;

    // USAJobs: JobSummary — shown in search results
    public string Description { get; set; } = string.Empty;

    // USAJobs: MajorDuties — full narrative of what the employee does
    public string Duties { get; set; } = string.Empty;

    // USAJobs: Qualifications — OPM minimum qualifications + specialized experience
    public string Qualifications { get; set; } = string.Empty;

    public bool IsOpen { get; set; }

    // USAJobs: JobCategories[].Code — 4-digit OPM series e.g. "2210" = IT Management
    public string OccupationalSeries { get; set; } = string.Empty;

    // USAJobs: PayPlan + LowGrade / HighGrade e.g. "GS-09", "GS-11"
    public string PayGradeMin { get; set; } = string.Empty;
    public string PayGradeMax { get; set; } = string.Empty;

    // USAJobs: appointmentType
    public AppointmentType AppointmentType { get; set; } = AppointmentType.Permanent;

    // USAJobs: workSchedule
    public WorkSchedule WorkSchedule { get; set; } = WorkSchedule.FullTime;

    // USAJobs: positionOpenDate / positionCloseDate
    public DateTime OpenDate { get; set; } = DateTime.UtcNow;
    public DateTime? CloseDate { get; set; }

    // USAJobs: WhoMayApply.Name
    public string WhoMayApply { get; set; } = "Open to US Citizens";

    // USAJobs: PositionLocation[].CityName + CountrySubDivisionCode
    public string DutyLocation { get; set; } = string.Empty;

    // USAJobs: teleworkEligible (Y/N in API)
    public bool TeleworkEligible { get; set; } = false;

    // USAJobs: travelRequirement
    public TravelRequirement TravelRequired { get; set; } = TravelRequirement.NotRequired;

    // USAJobs: securityClearance
    public SecurityClearance SecurityClearance { get; set; } = SecurityClearance.NotRequired;

    // USAJobs: supervisoryStatus (Y/N in API)
    public bool SupervisoryStatus { get; set; } = false;

    // USAJobs: relocationExpensesReimbursed (Y/N in API)
    public bool RelocationAuthorized { get; set; } = false;

    // USAJobs: drugTestRequired (Y/N in API)
    public bool DrugTestRequired { get; set; } = false;

    // Navigation
    public int HiringOrganizationId { get; set; }
    public HiringOrganization HiringOrganization { get; set; } = null!;
    public PositionRemuneration PositionRemuneration { get; set; } = null!;
}
```

### `PositionRemuneration.cs`

Maps directly to the `PositionRemuneration` array object in the USAJobs API. `RateIntervalCode` uses the API's standard codes rather than a currency symbol — `"PA"` (Per Annum) is the default for all GS positions.

```csharp
// src/HrMcp.Core/Entities/PositionRemuneration.cs
namespace HrMcp.Core.Entities;

public class PositionRemuneration
{
    public int Id { get; set; }

    // USAJobs: MinimumRange — stored as string in API e.g. "68405"; we use decimal for queries
    public decimal MinimumRange { get; set; }

    // USAJobs: MaximumRange
    public decimal MaximumRange { get; set; }

    // USAJobs: RateIntervalCode — "PA" = Per Annum, "PH" = Per Hour, "PD" = Per Day
    public string RateIntervalCode { get; set; } = "PA";

    // USAJobs: Description — human-readable label e.g. "Per Year"
    public string Description { get; set; } = "Per Year";

    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;
}
```

### Repository Interfaces

```csharp
// src/HrMcp.Core/Interfaces/IPositionRepository.cs
using HrMcp.Core.Entities;

namespace HrMcp.Core.Interfaces;

public interface IPositionRepository
{
    Task<IEnumerable<Position>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<Position>> GetOpenPositionsAsync(CancellationToken ct = default);
    Task<Position?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<Position>> GetByOrganizationAsync(int organizationId, CancellationToken ct = default);
}
```

```csharp
// src/HrMcp.Core/Interfaces/IHiringOrganizationRepository.cs
using HrMcp.Core.Entities;

namespace HrMcp.Core.Interfaces;

public interface IHiringOrganizationRepository
{
    Task<IEnumerable<HiringOrganization>> GetAllAsync(CancellationToken ct = default);
    Task<HiringOrganization?> GetByIdAsync(int id, CancellationToken ct = default);
}
```

---

## Step 3 — Application Layer (`HrMcp.Application`)

```text
src/HrMcp.Application/
  Services/
    PositionService.cs
    HiringOrganizationService.cs
```

```csharp
// src/HrMcp.Application/Services/PositionService.cs
using HrMcp.Core.Entities;
using HrMcp.Core.Interfaces;

namespace HrMcp.Application.Services;

public class PositionService(IPositionRepository repo)
{
    public Task<IEnumerable<Position>> GetAllPositionsAsync(CancellationToken ct = default)
        => repo.GetAllAsync(ct);

    public Task<IEnumerable<Position>> GetOpenPositionsAsync(CancellationToken ct = default)
        => repo.GetOpenPositionsAsync(ct);

    public Task<Position?> GetPositionByIdAsync(int id, CancellationToken ct = default)
        => repo.GetByIdAsync(id, ct);

    public Task<IEnumerable<Position>> GetPositionsByOrganizationAsync(int organizationId, CancellationToken ct = default)
        => repo.GetByOrganizationAsync(organizationId, ct);
}
```

```csharp
// src/HrMcp.Application/Services/HiringOrganizationService.cs
using HrMcp.Core.Entities;
using HrMcp.Core.Interfaces;

namespace HrMcp.Application.Services;

public class HiringOrganizationService(IHiringOrganizationRepository repo)
{
    public Task<IEnumerable<HiringOrganization>> GetAllOrganizationsAsync(CancellationToken ct = default)
        => repo.GetAllAsync(ct);

    public Task<HiringOrganization?> GetOrganizationByIdAsync(int id, CancellationToken ct = default)
        => repo.GetByIdAsync(id, ct);
}
```

---

## Step 4 — Infrastructure Layer (`HrMcp.Infrastructure.Persistence`)

### 4.1 — Add NuGet Packages

```bash
dotnet add src/HrMcp.Infrastructure.Persistence package Microsoft.EntityFrameworkCore.SqlServer --version 9.*
dotnet add src/HrMcp.Infrastructure.Persistence package Microsoft.EntityFrameworkCore.Design --version 9.*
dotnet add src/HrMcp.McpServer package Microsoft.EntityFrameworkCore.SqlServer --version 9.*
dotnet add src/HrMcp.McpServer package Microsoft.EntityFrameworkCore.Design --version 9.*
```

```text
src/HrMcp.Infrastructure.Persistence/
  Repositories/
    HiringOrganizationRepository.cs
    PositionRepository.cs
  DependencyInjection.cs
  DbSeeder.cs
  HrDbContext.cs
```

### 4.2 — `HrDbContext.cs`

```csharp
// src/HrMcp.Infrastructure.Persistence/HrDbContext.cs
using HrMcp.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrMcp.Infrastructure.Persistence;

public class HrDbContext(DbContextOptions<HrDbContext> options) : DbContext(options)
{
    public DbSet<HiringOrganization>   HiringOrganizations   => Set<HiringOrganization>();
    public DbSet<Position>             Positions             => Set<Position>();
    public DbSet<PositionRemuneration> PositionRemunerations => Set<PositionRemuneration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PositionRemuneration>()
            .Property(r => r.MinimumRange).HasPrecision(18, 2);
        modelBuilder.Entity<PositionRemuneration>()
            .Property(r => r.MaximumRange).HasPrecision(18, 2);

        // 1-to-1: Position ↔ PositionRemuneration
        modelBuilder.Entity<PositionRemuneration>()
            .HasOne(r => r.Position)
            .WithOne(p => p.PositionRemuneration)
            .HasForeignKey<PositionRemuneration>(r => r.PositionId);
    }
}
```

### 4.3 — `PositionRepository.cs`

```csharp
// src/HrMcp.Infrastructure.Persistence/Repositories/PositionRepository.cs
using HrMcp.Core.Entities;
using HrMcp.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HrMcp.Infrastructure.Persistence.Repositories;

public class PositionRepository(HrDbContext db) : IPositionRepository
{
    private IQueryable<Position> BaseQuery =>
        db.Positions
          .Include(p => p.HiringOrganization)
          .Include(p => p.PositionRemuneration);

    public async Task<IEnumerable<Position>> GetAllAsync(CancellationToken ct = default)
        => await BaseQuery.ToListAsync(ct);

    public async Task<IEnumerable<Position>> GetOpenPositionsAsync(CancellationToken ct = default)
        => await BaseQuery.Where(p => p.IsOpen).ToListAsync(ct);

    public Task<Position?> GetByIdAsync(int id, CancellationToken ct = default)
        => BaseQuery.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IEnumerable<Position>> GetByOrganizationAsync(int organizationId, CancellationToken ct = default)
        => await BaseQuery.Where(p => p.HiringOrganizationId == organizationId).ToListAsync(ct);
}
```

### 4.4 — `HiringOrganizationRepository.cs`

```csharp
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
```

### 4.5 — `DbSeeder.cs`

Seeds 4 federal hiring organizations — each with both `OrganizationName` (the hiring office) and `DepartmentName` (the cabinet-level parent) — and 5 realistic USAJobs-format positions.

```csharp
// src/HrMcp.Infrastructure.Persistence/DbSeeder.cs
using HrMcp.Core.Entities;
using HrMcp.Core.Enums;

namespace HrMcp.Infrastructure.Persistence;

public static class DbSeeder
{
    public static void Seed(HrDbContext db)
    {
        if (db.HiringOrganizations.Any()) return;

        var oit = new HiringOrganization {
            OrganizationName  = "Office of Information Technology",
            DepartmentName    = "Department of Homeland Security",
            AgencyDescription = "IT infrastructure, cybersecurity, and enterprise systems"
        };
        var ohr = new HiringOrganization {
            OrganizationName  = "Office of Human Resources",
            DepartmentName    = "Department of Homeland Security",
            AgencyDescription = "Federal workforce management and talent acquisition"
        };
        var opd = new HiringOrganization {
            OrganizationName  = "Office of Policy Development",
            DepartmentName    = "Department of Homeland Security",
            AgencyDescription = "Federal policy analysis and regulatory affairs"
        };
        var fin = new HiringOrganization {
            OrganizationName  = "Office of the Chief Financial Officer",
            DepartmentName    = "Department of Homeland Security",
            AgencyDescription = "Federal financial management and budget oversight"
        };

        db.HiringOrganizations.AddRange(oit, ohr, opd, fin);
        db.SaveChanges();

        var today = DateTime.UtcNow;

        var positions = new List<Position>
        {
            new() {
                Title                = "IT Specialist (SYSADMIN)",
                Description          = "Manages and maintains enterprise IT systems and infrastructure.",
                Duties               = "Administers Windows and Linux servers. Manages Active Directory, DNS, and DHCP. Monitors system performance and resolves incidents. Coordinates patching and vulnerability remediation.",
                Qualifications       = "GS-11: One year of specialized experience equivalent to GS-09 managing enterprise server environments including Active Directory and network services.",
                IsOpen               = true,
                OccupationalSeries   = "2210",
                PayGradeMin          = "GS-09",  PayGradeMax = "GS-11",
                AppointmentType      = AppointmentType.Permanent,
                WorkSchedule         = WorkSchedule.FullTime,
                OpenDate             = today,             CloseDate = today.AddDays(14),
                WhoMayApply          = "Open to US Citizens",
                DutyLocation         = "Washington, DC",
                TeleworkEligible     = true,
                SecurityClearance    = SecurityClearance.Secret,
                TravelRequired       = TravelRequirement.Occasional,
                SupervisoryStatus    = false, RelocationAuthorized = false, DrugTestRequired = false,
                HiringOrganizationId = oit.Id,
                PositionRemuneration = new() { MinimumRange = 68_405, MaximumRange = 107_590 }
            },
            new() {
                Title                = "Supervisory IT Specialist (INFOSEC)",
                Description          = "Leads cybersecurity operations and oversees the agency information security program.",
                Duties               = "Directs incident response and FISMA compliance. Manages a team of 8 IT security specialists. Briefs senior leadership on cyber risk posture.",
                Qualifications       = "GS-14: One year of specialized experience equivalent to GS-13 leading an agency-wide information security program and supervising IT security personnel.",
                IsOpen               = false,
                OccupationalSeries   = "2210",
                PayGradeMin          = "GS-14",  PayGradeMax = "GS-14",
                AppointmentType      = AppointmentType.Permanent,
                WorkSchedule         = WorkSchedule.FullTime,
                OpenDate             = today.AddDays(-30), CloseDate = today.AddDays(-16),
                WhoMayApply          = "Open to current federal employees only",
                DutyLocation         = "Washington, DC",
                TeleworkEligible     = true,
                SecurityClearance    = SecurityClearance.TopSecret,
                TravelRequired       = TravelRequirement.Occasional,
                SupervisoryStatus    = true, RelocationAuthorized = true, DrugTestRequired = false,
                HiringOrganizationId = oit.Id,
                PositionRemuneration = new() { MinimumRange = 139_395, MaximumRange = 181_216 }
            },
            new() {
                Title                = "Human Resources Specialist (Recruitment)",
                Description          = "Manages full-cycle federal recruitment and staffing operations.",
                Duties               = "Develops job opportunity announcements on USAJobs. Rates and ranks applicants using OPM qualification standards. Advises hiring managers on merit promotion and competitive examining procedures.",
                Qualifications       = "GS-09: One year of specialized experience equivalent to GS-07 in federal staffing, classification, or employee relations.",
                IsOpen               = true,
                OccupationalSeries   = "0201",
                PayGradeMin          = "GS-07",  PayGradeMax = "GS-09",
                AppointmentType      = AppointmentType.Permanent,
                WorkSchedule         = WorkSchedule.FullTime,
                OpenDate             = today,             CloseDate = today.AddDays(10),
                WhoMayApply          = "Open to US Citizens",
                DutyLocation         = "Arlington, VA",
                TeleworkEligible     = true,
                SecurityClearance    = SecurityClearance.PublicTrust,
                TravelRequired       = TravelRequirement.NotRequired,
                SupervisoryStatus    = false, RelocationAuthorized = false, DrugTestRequired = false,
                HiringOrganizationId = ohr.Id,
                PositionRemuneration = new() { MinimumRange = 53_105, MaximumRange = 84_441 }
            },
            new() {
                Title                = "Management Analyst",
                Description          = "Conducts organizational studies and evaluates federal program effectiveness.",
                Duties               = "Analyzes agency workflow and recommends process improvements. Prepares management reports and briefing materials for senior leadership. Coordinates with program offices on performance measurement.",
                Qualifications       = "GS-12: One year of specialized experience equivalent to GS-11 conducting management or program analysis in a federal agency.",
                IsOpen               = true,
                OccupationalSeries   = "0343",
                PayGradeMin          = "GS-11",  PayGradeMax = "GS-12",
                AppointmentType      = AppointmentType.Permanent,
                WorkSchedule         = WorkSchedule.FullTime,
                OpenDate             = today.AddDays(-3),  CloseDate = today.AddDays(11),
                WhoMayApply          = "Open to US Citizens",
                DutyLocation         = "Remote (US)",
                TeleworkEligible     = true,
                SecurityClearance    = SecurityClearance.PublicTrust,
                TravelRequired       = TravelRequirement.Occasional,
                SupervisoryStatus    = false, RelocationAuthorized = false, DrugTestRequired = false,
                HiringOrganizationId = opd.Id,
                PositionRemuneration = new() { MinimumRange = 82_764, MaximumRange = 128_956 }
            },
            new() {
                Title                = "Financial Analyst",
                Description          = "Supports federal budget formulation and execution for the agency's appropriated funds.",
                Duties               = "Prepares budget justifications and spending plans. Monitors obligations and expenditures against appropriations. Coordinates with OMB on passback and apportionment requests.",
                Qualifications       = "GS-11: One year of specialized experience equivalent to GS-09 in federal budget or financial management.",
                IsOpen               = true,
                OccupationalSeries   = "0501",
                PayGradeMin          = "GS-09",  PayGradeMax = "GS-11",
                AppointmentType      = AppointmentType.Permanent,
                WorkSchedule         = WorkSchedule.FullTime,
                OpenDate             = today.AddDays(-1),  CloseDate = today.AddDays(13),
                WhoMayApply          = "Open to US Citizens",
                DutyLocation         = "Washington, DC",
                TeleworkEligible     = false,
                SecurityClearance    = SecurityClearance.PublicTrust,
                TravelRequired       = TravelRequirement.NotRequired,
                SupervisoryStatus    = false, RelocationAuthorized = false, DrugTestRequired = false,
                HiringOrganizationId = fin.Id,
                PositionRemuneration = new() { MinimumRange = 68_405, MaximumRange = 107_590 }
            },
        };

        db.Positions.AddRange(positions);
        db.SaveChanges();
    }
}
```

### 4.6 — `DependencyInjection.cs`

```csharp
// src/HrMcp.Infrastructure.Persistence/DependencyInjection.cs
using HrMcp.Core.Interfaces;
using HrMcp.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HrMcp.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<HrDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IHiringOrganizationRepository, HiringOrganizationRepository>();

        return services;
    }
}
```

---

## Step 5 — Wire Up `HrMcp.McpServer`

### 5.1 — `appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=HrMcpDb;Trusted_Connection=True;"
  },
  "Urls": "http://localhost:5100"
}
```

### 5.2 — `Program.cs`

```csharp
// src/HrMcp.McpServer/Program.cs
using HrMcp.Application.Services;
using HrMcp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPersistence(
    builder.Configuration.GetConnectionString("DefaultConnection")!);
builder.Services.AddScoped<PositionService>();
builder.Services.AddScoped<HiringOrganizationService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db);
}

app.Run();
```

---

## Step 6 — EF Core Migration

```bash
dotnet ef migrations add InitialCreate \
  --project src/HrMcp.Infrastructure.Persistence \
  --startup-project src/HrMcp.McpServer \
  --output-dir Migrations

dotnet ef database update \
  --project src/HrMcp.Infrastructure.Persistence \
  --startup-project src/HrMcp.McpServer
```

---

## Step 7 — Verify

```bash
dotnet build DotnetAiAgentMcp.slnx   # 0 errors
dotnet run --project src/HrMcp.McpServer
```

On first run the server migrates and seeds. Check the database:

- `HiringOrganizations` table: 4 rows — each with `OrganizationName`, `DepartmentName`, `AgencyDescription`
- `Positions` table: 5 rows — 4 open, 1 closed; all with GS grades, series codes, clearance levels
- `PositionRemunerations` table: 5 rows — `MinimumRange`, `MaximumRange`, `RateIntervalCode = "PA"`

A sample record shows what a USAJobs-aligned announcement looks like in the database:

```text
Title:               IT Specialist (SYSADMIN)
OccupationalSeries:  2210
PayGradeMin:         GS-09
PayGradeMax:         GS-11
MinimumRange:        $68,405
MaximumRange:        $107,590
RateIntervalCode:    PA  (Per Annum)
DutyLocation:        Washington, DC
SecurityClearance:   Secret
TeleworkEligible:    true
WhoMayApply:         Open to US Citizens
OrganizationName:    Office of Information Technology
DepartmentName:      Department of Homeland Security
```

---

## Step 8 — Optional: Seed with Real USAJobs Data

The 5 positions above are hand-crafted to match the USAJobs schema. This step replaces them with real federal job postings pulled from the live API, then commits the result as a JSON file so readers cloning your repo don't need an API key.

### 8.1 — Register for an API Key

1. Go to [developer.usajobs.gov/APIRequest](https://developer.usajobs.gov/APIRequest/) and complete the short registration form
2. You will receive two credentials by email: your **email address** (used as `User-Agent`) and an **Authorization Key**

Authentication is header-only — no OAuth required:

```http
User-Agent:        your-email@example.com
Authorization-Key: your-key-here
```

### 8.2 — Create the Fetch Tool

This is a one-off utility that lives outside the solution. Create it in a `tools/` folder and **do not** add it to the `.sln`:

```bash
mkdir tools
dotnet new console -n UsaJobsFetcher -o tools/UsaJobsFetcher
```

**`tools/UsaJobsFetcher/UsaJobsFetcher.csproj`**

No extra packages — `System.Text.Json` ships with the runtime:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

**`tools/UsaJobsFetcher/UsaJobsFetcher.csproj`**

Add `UserSecretsId` and the configuration packages — credentials never touch the command line:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UserSecretsId>usajobs-fetcher</UserSecretsId>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="9.*" />
    <PackageReference Include="Microsoft.Extensions.Configuration.UserSecrets" Version="9.*" />
  </ItemGroup>
</Project>
```

Store your credentials in the .NET secret store (never committed to source control):

```bash
dotnet user-secrets set "UsaJobs:Email"   "your@email.com"  --project tools/UsaJobsFetcher
dotnet user-secrets set "UsaJobs:AuthKey" "your-key-here"   --project tools/UsaJobsFetcher
```

**`tools/UsaJobsFetcher/Program.cs`**

Reads credentials from user secrets, calls the Search API, maps `MatchedObjectDescriptor` fields to the domain model schema, and writes `data/usajobs-seed.json` to the solution root:

```csharp
// tools/UsaJobsFetcher/Program.cs
// One-time tool: fetches real federal job postings from the USAJobs API
// and writes data/usajobs-seed.json to the solution root.
//
// Store credentials via .NET User Secrets (never commit them):
//   dotnet user-secrets set "UsaJobs:Email"   "your@email.com"  --project tools/UsaJobsFetcher
//   dotnet user-secrets set "UsaJobs:AuthKey" "your-key-here"   --project tools/UsaJobsFetcher
//
// Then run from the solution root:
//   dotnet run --project tools/UsaJobsFetcher
//
// Output: data/usajobs-seed.json — commit this file so readers don't need an API key.

using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

var config = new ConfigurationBuilder()
    .AddUserSecrets("usajobs-fetcher")
    .Build();

var email   = config["UsaJobs:Email"]   ?? throw new InvalidOperationException("UsaJobs:Email secret is not set.");
var authKey = config["UsaJobs:AuthKey"] ?? throw new InvalidOperationException("UsaJobs:AuthKey secret is not set.");

// ── call USAJobs Search API ───────────────────────────────────────────────────
using var http = new HttpClient();
http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", email);
http.DefaultRequestHeaders.Add("Authorization-Key", authKey);

// Change Organization= to broaden beyond DHS (remove it entirely for all agencies)
const string url =
    "https://data.usajobs.gov/api/search" +
    "?ResultsPerPage=25" +
    "&Organization=HS";   // HS = Department of Homeland Security

Console.WriteLine($"Fetching: {url}");
var responseJson = await http.GetStringAsync(url);

// ── parse ─────────────────────────────────────────────────────────────────────
var parseOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var root      = JsonSerializer.Deserialize<SearchRoot>(responseJson, parseOpts)!;
var items     = root.SearchResult.SearchResultItems;

Console.WriteLine($"Received {items.Count} positions");

// ── map to seed model ─────────────────────────────────────────────────────────
var orgs      = new Dictionary<string, SeedOrg>(StringComparer.OrdinalIgnoreCase);
var positions = new List<SeedPosition>();

foreach (var item in items)
{
    var d = item.MatchedObjectDescriptor;
    if (d is null) continue;

    var orgName  = d.OrganizationName ?? "Unknown Agency";
    var deptName = d.DepartmentName   ?? "Unknown Department";

    orgs.TryAdd(orgName, new SeedOrg(orgName, deptName, ""));

    var det  = d.UserArea?.Details;
    var rem  = d.PositionRemuneration?.FirstOrDefault();
    var plan = d.JobGrade?.FirstOrDefault()?.Code ?? "GS";
    var lo   = det?.LowGrade;
    var hi   = det?.HighGrade;

    var isOpen = DateTime.TryParse(d.ApplicationCloseDate, out var close)
                 && close >= DateTime.UtcNow;

    positions.Add(new SeedPosition(
        Title:               d.PositionTitle ?? "",
        Description:         det?.JobSummary ?? "",
        Duties:              det?.MajorDuties is { Count: > 0 }
                                 ? string.Join(" ", det.MajorDuties)
                                 : "",
        Qualifications:      det?.Requirements ?? "",
        IsOpen:              isOpen,
        OccupationalSeries:  d.JobCategory?.FirstOrDefault()?.Code ?? "",
        PayGradeMin:         lo is not null ? $"{plan}-{lo.PadLeft(2, '0')}" : "",
        PayGradeMax:         hi is not null ? $"{plan}-{hi.PadLeft(2, '0')}" : "",
        AppointmentType:     MapAppointment(d.PositionAppointmentType?.FirstOrDefault()?.Name),
        WorkSchedule:        MapSchedule(d.PositionSchedule?.FirstOrDefault()?.Name),
        OpenDate:            d.PositionStartDate    ?? DateTime.UtcNow.ToString("O"),
        CloseDate:           d.ApplicationCloseDate,
        WhoMayApply:         det?.WhoMayApply?.Name ?? "Open to US Citizens",
        DutyLocation:        d.PositionLocation?.FirstOrDefault()?.CityName ?? "",
        TeleworkEligible:    det?.TeleworkEligible ?? false,
        TravelRequired:      MapTravel(det?.TravelCode),
        SecurityClearance:   MapClearance(det?.SecurityClearance),
        SupervisoryStatus:   "Yes".Equals(det?.SupervisoryPosition, StringComparison.OrdinalIgnoreCase),
        RelocationAuthorized:"Yes".Equals(det?.Relocation,          StringComparison.OrdinalIgnoreCase),
        DrugTestRequired:    "Yes".Equals(det?.DrugTestRequired,     StringComparison.OrdinalIgnoreCase),
        OrganizationName:    orgName,
        MinimumRange:        decimal.TryParse(rem?.MinimumRange, out var mn) ? mn : 0,
        MaximumRange:        decimal.TryParse(rem?.MaximumRange, out var mx) ? mx : 0,
        RateIntervalCode:    rem?.RateIntervalCode ?? "PA"
    ));
}

// ── write data/usajobs-seed.json ──────────────────────────────────────────────
var outPath   = Path.Combine(Directory.GetCurrentDirectory(), "data", "usajobs-seed.json");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

var writeOpts = new JsonSerializerOptions
{
    WriteIndented          = true,
    PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

var seed = new SeedFile(orgs.Values.ToList(), positions);
await File.WriteAllTextAsync(outPath, JsonSerializer.Serialize(seed, writeOpts));

Console.WriteLine($"Wrote {positions.Count} positions from {orgs.Count} organizations");
Console.WriteLine($"Output: {outPath}");
return 0;

// ── string → enum label helpers ───────────────────────────────────────────────
static string MapAppointment(string? name) => name?.ToLower() switch
{
    { } s when s.Contains("perm") => "Permanent",
    { } s when s.Contains("term") => "Term",
    _                              => "Temporary"
};

static string MapSchedule(string? name) => name?.ToLower() switch
{
    { } s when s.Contains("full")         => "FullTime",
    { } s when s.Contains("part")         => "PartTime",
    { } s when s.Contains("intermittent") => "Intermittent",
    _                                      => "FullTime"
};

static string MapTravel(string? code) => code switch
{
    "1" => "NotRequired",
    "2" => "Occasional",
    "3" => "Sometimes",
    "4" => "Frequent",
    _   => "NotRequired"
};

static string MapClearance(string? s) => s?.ToLower() switch
{
    { } v when v.Contains("ts/sci") || v.Contains("top secret/sci") => "TopSecretSCI",
    { } v when v.Contains("top secret")                             => "TopSecret",
    { } v when v.Contains("secret")                                 => "Secret",
    { } v when v.Contains("confidential")                           => "Confidential",
    { } v when v.Contains("public trust")                           => "PublicTrust",
    _                                                                => "NotRequired"
};

// ── USAJobs API response types ────────────────────────────────────────────────
record SearchRoot(SearchResult SearchResult);
record SearchResult(int SearchResultCount, List<SearchResultItem> SearchResultItems);
record SearchResultItem(MatchedObjectDescriptor? MatchedObjectDescriptor);
record MatchedObjectDescriptor(
    string?                          PositionTitle,
    string?                          OrganizationName,
    string?                          DepartmentName,
    string?                          PositionStartDate,
    string?                          ApplicationCloseDate,
    List<PositionLocation>?          PositionLocation,
    List<JobCategory>?               JobCategory,
    List<JobGrade>?                  JobGrade,
    List<PositionSchedule>?          PositionSchedule,
    List<PositionAppointmentType>?   PositionAppointmentType,
    List<PositionRemuneration>?      PositionRemuneration,
    UserArea?                        UserArea);
record PositionLocation(string? CityName, string? CountrySubDivisionCode);
record JobCategory(string? Name, string? Code);
record JobGrade(string? Code);
record PositionSchedule(string? Name, string? Code);
record PositionAppointmentType(string? Name, string? Code);
record PositionRemuneration(string? MinimumRange, string? MaximumRange, string? RateIntervalCode);
record UserArea(UserAreaDetails? Details);
record UserAreaDetails(
    string?       JobSummary,
    WhoMayApply?  WhoMayApply,
    string?       LowGrade,
    string?       HighGrade,
    string?       Requirements,
    List<string>? MajorDuties,
    string?       Relocation,
    string?       DrugTestRequired,
    bool?         TeleworkEligible,
    string?       SupervisoryPosition,
    string?       SecurityClearance,
    string?       TravelCode);
record WhoMayApply(string? Name, string? Code);

// ── seed file model ───────────────────────────────────────────────────────────
record SeedFile(List<SeedOrg> Organizations, List<SeedPosition> Positions);
record SeedOrg(string OrganizationName, string DepartmentName, string AgencyDescription);
record SeedPosition(
    string   Title,
    string   Description,
    string   Duties,
    string   Qualifications,
    bool     IsOpen,
    string   OccupationalSeries,
    string   PayGradeMin,
    string   PayGradeMax,
    string   AppointmentType,
    string   WorkSchedule,
    string   OpenDate,
    string?  CloseDate,
    string   WhoMayApply,
    string   DutyLocation,
    bool     TeleworkEligible,
    string   TravelRequired,
    string   SecurityClearance,
    bool     SupervisoryStatus,
    bool     RelocationAuthorized,
    bool     DrugTestRequired,
    string   OrganizationName,
    decimal  MinimumRange,
    decimal  MaximumRange,
    string   RateIntervalCode);
```

### 8.3 — Run and Commit

Run from the **solution root** — `Directory.GetCurrentDirectory()` writes `data/usajobs-seed.json` relative to where you invoke `dotnet run`:

```bash
dotnet run --project tools/UsaJobsFetcher
```

Sample output:

```text
Fetching: https://data.usajobs.gov/api/search?ResultsPerPage=25&Organization=HS
Received 25 positions
Wrote 25 positions from 8 organizations
Output: C:\...\DotnetAiAgentMcp\data\usajobs-seed.json
```

Commit the seed file — anyone cloning the repo gets real data without an API key:

```bash
git add data/usajobs-seed.json
git commit -m "seed: add real USAJobs positions from DHS"
```

### 8.4 — Update `DbSeeder.cs` to Load from JSON

Extend `DbSeeder` to accept an optional path. When the file exists it uses it; otherwise it falls back to the hand-crafted data:

```csharp
// src/HrMcp.Infrastructure.Persistence/DbSeeder.cs
using System.Text.Json;
using HrMcp.Core.Entities;
using HrMcp.Core.Enums;

namespace HrMcp.Infrastructure.Persistence;

public static class DbSeeder
{
    // ← added optional jsonSeedPath parameter
    public static void Seed(HrDbContext db, string? jsonSeedPath = null)
    {
        if (db.HiringOrganizations.Any()) return;

        if (jsonSeedPath is not null && File.Exists(jsonSeedPath))
        {
            SeedFromJson(db, jsonSeedPath);
            return;
        }

        SeedFromCode(db);
    }

    // ── JSON path ─────────────────────────────────────────────────────────────
    private static void SeedFromJson(HrDbContext db, string path)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<SeedFile>(File.ReadAllText(path), opts)!;

        var orgMap = new Dictionary<string, HiringOrganization>(StringComparer.OrdinalIgnoreCase);

        foreach (var o in file.Organizations)
        {
            var entity = new HiringOrganization
            {
                OrganizationName  = o.OrganizationName,
                DepartmentName    = o.DepartmentName,
                AgencyDescription = o.AgencyDescription
            };
            db.HiringOrganizations.Add(entity);
            orgMap[o.OrganizationName] = entity;
        }

        db.SaveChanges();

        foreach (var p in file.Positions)
        {
            if (!orgMap.TryGetValue(p.OrganizationName, out var org)) continue;

            db.Positions.Add(new Position
            {
                Title                = p.Title,
                Description          = p.Description,
                Duties               = p.Duties,
                Qualifications       = p.Qualifications,
                IsOpen               = p.IsOpen,
                OccupationalSeries   = p.OccupationalSeries,
                PayGradeMin          = p.PayGradeMin,
                PayGradeMax          = p.PayGradeMax,
                AppointmentType      = Parse<AppointmentType>(p.AppointmentType,    AppointmentType.Permanent),
                WorkSchedule         = Parse<WorkSchedule>(p.WorkSchedule,          WorkSchedule.FullTime),
                OpenDate             = DateTime.TryParse(p.OpenDate,  out var od) ? od : DateTime.UtcNow,
                CloseDate            = DateTime.TryParse(p.CloseDate, out var cd) ? cd : null,
                WhoMayApply          = p.WhoMayApply,
                DutyLocation         = p.DutyLocation,
                TeleworkEligible     = p.TeleworkEligible,
                TravelRequired       = Parse<TravelRequirement>(p.TravelRequired,   TravelRequirement.NotRequired),
                SecurityClearance    = Parse<SecurityClearance>(p.SecurityClearance,SecurityClearance.NotRequired),
                SupervisoryStatus    = p.SupervisoryStatus,
                RelocationAuthorized = p.RelocationAuthorized,
                DrugTestRequired     = p.DrugTestRequired,
                HiringOrganizationId = org.Id,
                PositionRemuneration = new PositionRemuneration
                {
                    MinimumRange     = p.MinimumRange,
                    MaximumRange     = p.MaximumRange,
                    RateIntervalCode = p.RateIntervalCode,
                    Description      = p.RateIntervalCode switch
                    {
                        "PA" => "Per Year",
                        "PH" => "Per Hour",
                        "PD" => "Per Day",
                        _    => p.RateIntervalCode
                    }
                }
            });
        }

        db.SaveChanges();
        Console.WriteLine($"[DbSeeder] Seeded {file.Positions.Count} positions from {path}");
    }

    private static T Parse<T>(string value, T fallback) where T : struct, Enum
        => Enum.TryParse<T>(value, ignoreCase: true, out var result) ? result : fallback;

    // ── seed file DTOs ────────────────────────────────────────────────────────
    private record SeedFile(List<SeedOrg> Organizations, List<SeedPosition> Positions);
    private record SeedOrg(string OrganizationName, string DepartmentName, string AgencyDescription);
    private record SeedPosition(
        string   Title,        string   Description,   string   Duties,
        string   Qualifications, bool   IsOpen,        string   OccupationalSeries,
        string   PayGradeMin,  string   PayGradeMax,   string   AppointmentType,
        string   WorkSchedule, string   OpenDate,      string?  CloseDate,
        string   WhoMayApply,  string   DutyLocation,  bool     TeleworkEligible,
        string   TravelRequired, string SecurityClearance, bool SupervisoryStatus,
        bool     RelocationAuthorized, bool DrugTestRequired, string OrganizationName,
        decimal  MinimumRange, decimal  MaximumRange,  string   RateIntervalCode);

    // ── hardcoded fallback ────────────────────────────────────────────────────
    // Rename the original Seed() body to SeedFromCode() — no changes to the content.
    private static void SeedFromCode(HrDbContext db)
    {
        var oit = new HiringOrganization {
            OrganizationName  = "Office of Information Technology",
            DepartmentName    = "Department of Homeland Security",
            AgencyDescription = "IT infrastructure, cybersecurity, and enterprise systems"
        };
        var ohr = new HiringOrganization {
            OrganizationName  = "Office of Human Resources",
            DepartmentName    = "Department of Homeland Security",
            AgencyDescription = "Federal workforce management and talent acquisition"
        };
        var opd = new HiringOrganization {
            OrganizationName  = "Office of Policy Development",
            DepartmentName    = "Department of Homeland Security",
            AgencyDescription = "Federal policy analysis and regulatory affairs"
        };
        var fin = new HiringOrganization {
            OrganizationName  = "Office of the Chief Financial Officer",
            DepartmentName    = "Department of Homeland Security",
            AgencyDescription = "Federal financial management and budget oversight"
        };

        db.HiringOrganizations.AddRange(oit, ohr, opd, fin);
        db.SaveChanges();

        var today = DateTime.UtcNow;

        var positions = new List<Position>
        {
            new() {
                Title = "IT Specialist (SYSADMIN)",
                Description = "Manages and maintains enterprise IT systems and infrastructure.",
                Duties = "Administers Windows and Linux servers. Manages Active Directory, DNS, and DHCP. Monitors system performance and resolves incidents. Coordinates patching and vulnerability remediation.",
                Qualifications = "GS-11: One year of specialized experience equivalent to GS-09 managing enterprise server environments including Active Directory and network services.",
                IsOpen = true, OccupationalSeries = "2210", PayGradeMin = "GS-09", PayGradeMax = "GS-11",
                AppointmentType = AppointmentType.Permanent, WorkSchedule = WorkSchedule.FullTime,
                OpenDate = today, CloseDate = today.AddDays(14), WhoMayApply = "Open to US Citizens",
                DutyLocation = "Washington, DC", TeleworkEligible = true,
                SecurityClearance = SecurityClearance.Secret, TravelRequired = TravelRequirement.Occasional,
                SupervisoryStatus = false, RelocationAuthorized = false, DrugTestRequired = false,
                HiringOrganizationId = oit.Id,
                PositionRemuneration = new() { MinimumRange = 68_405, MaximumRange = 107_590 }
            },
            new() {
                Title = "Supervisory IT Specialist (INFOSEC)",
                Description = "Leads cybersecurity operations and oversees the agency information security program.",
                Duties = "Directs incident response and FISMA compliance. Manages a team of 8 IT security specialists. Briefs senior leadership on cyber risk posture.",
                Qualifications = "GS-14: One year of specialized experience equivalent to GS-13 leading an agency-wide information security program and supervising IT security personnel.",
                IsOpen = false, OccupationalSeries = "2210", PayGradeMin = "GS-14", PayGradeMax = "GS-14",
                AppointmentType = AppointmentType.Permanent, WorkSchedule = WorkSchedule.FullTime,
                OpenDate = today.AddDays(-30), CloseDate = today.AddDays(-16),
                WhoMayApply = "Open to current federal employees only",
                DutyLocation = "Washington, DC", TeleworkEligible = true,
                SecurityClearance = SecurityClearance.TopSecret, TravelRequired = TravelRequirement.Occasional,
                SupervisoryStatus = true, RelocationAuthorized = true, DrugTestRequired = false,
                HiringOrganizationId = oit.Id,
                PositionRemuneration = new() { MinimumRange = 139_395, MaximumRange = 181_216 }
            },
            new() {
                Title = "Human Resources Specialist (Recruitment)",
                Description = "Manages full-cycle federal recruitment and staffing operations.",
                Duties = "Develops job opportunity announcements on USAJobs. Rates and ranks applicants using OPM qualification standards. Advises hiring managers on merit promotion and competitive examining procedures.",
                Qualifications = "GS-09: One year of specialized experience equivalent to GS-07 in federal staffing, classification, or employee relations.",
                IsOpen = true, OccupationalSeries = "0201", PayGradeMin = "GS-07", PayGradeMax = "GS-09",
                AppointmentType = AppointmentType.Permanent, WorkSchedule = WorkSchedule.FullTime,
                OpenDate = today, CloseDate = today.AddDays(10), WhoMayApply = "Open to US Citizens",
                DutyLocation = "Arlington, VA", TeleworkEligible = true,
                SecurityClearance = SecurityClearance.PublicTrust, TravelRequired = TravelRequirement.NotRequired,
                SupervisoryStatus = false, RelocationAuthorized = false, DrugTestRequired = false,
                HiringOrganizationId = ohr.Id,
                PositionRemuneration = new() { MinimumRange = 53_105, MaximumRange = 84_441 }
            },
            new() {
                Title = "Management Analyst",
                Description = "Conducts organizational studies and evaluates federal program effectiveness.",
                Duties = "Analyzes agency workflow and recommends process improvements. Prepares management reports and briefing materials for senior leadership. Coordinates with program offices on performance measurement.",
                Qualifications = "GS-12: One year of specialized experience equivalent to GS-11 conducting management or program analysis in a federal agency.",
                IsOpen = true, OccupationalSeries = "0343", PayGradeMin = "GS-11", PayGradeMax = "GS-12",
                AppointmentType = AppointmentType.Permanent, WorkSchedule = WorkSchedule.FullTime,
                OpenDate = today.AddDays(-3), CloseDate = today.AddDays(11), WhoMayApply = "Open to US Citizens",
                DutyLocation = "Remote (US)", TeleworkEligible = true,
                SecurityClearance = SecurityClearance.PublicTrust, TravelRequired = TravelRequirement.Occasional,
                SupervisoryStatus = false, RelocationAuthorized = false, DrugTestRequired = false,
                HiringOrganizationId = opd.Id,
                PositionRemuneration = new() { MinimumRange = 82_764, MaximumRange = 128_956 }
            },
            new() {
                Title = "Financial Analyst",
                Description = "Supports federal budget formulation and execution for the agency's appropriated funds.",
                Duties = "Prepares budget justifications and spending plans. Monitors obligations and expenditures against appropriations. Coordinates with OMB on passback and apportionment requests.",
                Qualifications = "GS-11: One year of specialized experience equivalent to GS-09 in federal budget or financial management.",
                IsOpen = true, OccupationalSeries = "0501", PayGradeMin = "GS-09", PayGradeMax = "GS-11",
                AppointmentType = AppointmentType.Permanent, WorkSchedule = WorkSchedule.FullTime,
                OpenDate = today.AddDays(-1), CloseDate = today.AddDays(13), WhoMayApply = "Open to US Citizens",
                DutyLocation = "Washington, DC", TeleworkEligible = false,
                SecurityClearance = SecurityClearance.PublicTrust, TravelRequired = TravelRequirement.NotRequired,
                SupervisoryStatus = false, RelocationAuthorized = false, DrugTestRequired = false,
                HiringOrganizationId = fin.Id,
                PositionRemuneration = new() { MinimumRange = 68_405, MaximumRange = 107_590 }
            },
        };

        db.Positions.AddRange(positions);
        db.SaveChanges();
    }
}
```

Update the call site in `Program.cs` — pass the seed path so `DbSeeder` knows where to look:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
    db.Database.Migrate();

    // Looks for data/usajobs-seed.json in the working directory (solution root when using dotnet run)
    var seedPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "usajobs-seed.json");
    DbSeeder.Seed(db, seedPath);
}
```

On the next `dotnet run` you will see:

```text
[DbSeeder] Seeded 25 positions from data/usajobs-seed.json
```

If the file is absent the server falls back to the 5 hand-crafted positions — so the app always starts cleanly regardless of whether the JSON file has been generated.

---

## What We Built

| Project | Purpose | Dependencies |
|---------|---------|-------------|
| `HrMcp.Core` | Domain entities, enums, repository interfaces | None |
| `HrMcp.Application` | Use cases (PositionService, HiringOrganizationService) | Core |
| `HrMcp.Infrastructure.Persistence` | EF Core implementation | Core, EF Core |
| `HrMcp.McpServer` | Web host (MCP tools added in Part 3) | Application, Infrastructure |
| `HrMcp.Agent` | Console agent (built in Part 4) | Application, Infrastructure |

The AI knows nothing about this yet. In Part 3, we expose these positions as MCP tools so that Claude Desktop and any MCP-compatible client can query, filter, and generate USAJobs-format announcements from this data.

---

## Next Up

**[Part 2: Introduction to Model Context Protocol →](part-2-intro-to-mcp.md)**

We step back from code to build the mental model for MCP: what problem it solves, how its architecture maps to familiar .NET patterns, and why it matters for .NET developers building AI-enabled applications.

---

## Sources

- [USAJobs.gov Developer API Reference](https://developer.usajobs.gov/api-reference/)
- [USAJobs Historical Data Fields — Abigail Haddad](https://abigailhaddad.github.io/usajobs_historical/)
- [How to understand the job announcement overview — USAJobs Help](https://help.usajobs.gov/how-to/job-announcement/overview)
- [What is a series or grade? — USAJobs Help](https://help.usajobs.gov/faq/pay/series-and-grade)
- [Fixing SQL Server 2025 LocalDB in Visual Studio 2026 — ErikEJ](https://erikej.github.io/sqlserver/localdb/2026/03/13/localdb-sqlserver-2025.html)
