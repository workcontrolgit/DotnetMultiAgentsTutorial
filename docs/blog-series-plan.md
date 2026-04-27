# Blog Series Plan: AI Agents & MCP with .NET 10

**Audience:** .NET developers experienced with C#/.NET, new to AI agents and MCP  
**Detail level:** Full code samples (copy-paste ready)  
**Output path:** `blogs/series-1-ai-agent-mcp/`  
**Code path:** `src/`

---

## Series Overview

| # | Post | File | Key Deliverable |
|---|------|------|-----------------|
| 1 | Clean Architecture Foundation with HR Domain | `part-1-clean-architecture-hr-domain.md` | Runnable .NET 10 solution with HR data |
| 2 | Introduction to Model Context Protocol | `part-2-intro-to-mcp.md` | Mental model for MCP (no code) |
| 3 | Building an MCP Server in .NET 10 | `part-3-mcp-server-dotnet.md` | Working MCP server with HR tools |
| 4 | AI Agent with Microsoft.Extensions.AI + Ollama | `part-4-ai-agent-extensions-ai.md` | Console agent using local LLM + MCP tools |
| 5 | Claude Desktop Integration & End-to-End Demo | `part-5-claude-desktop-integration.md` | Claude Desktop calling HR MCP tools |
| 6 | Securing the MCP Server with OIDC | `part-6-mcp-security-oidc.md` | JWT/OIDC-protected MCP endpoint with authenticated agent calls |

---

---

# Part 1 — Clean Architecture Foundation with HR Domain

**File:** `blogs/series-1-ai-agent-mcp/part-1-clean-architecture-hr-domain.md`

## Goals
- Explain *why* Clean Architecture works well for AI-enabled backends
- Scaffold a .NET 10 solution with 5 projects
- Define the HR domain: `Position`, `HiringOrganization`, `PositionRemuneration` (USAJobs-aligned)
- Implement EF Core with SQL Server LocalDB
- Seed realistic federal HR data

## Sections & Steps

### Section 1 — Why Clean Architecture for AI Apps
- Explain the inversion: domain becomes the AI's contract
- Diagram showing layers (Core → Application → Infrastructure ← McpServer/Agent)
- Key rule: AI infrastructure (Ollama, MCP SDK) lives in outermost layers only

### Section 2 — Prerequisites
List exactly:
- .NET 10 SDK (`dotnet --version` ≥ 10.0)
- SQL Server LocalDB (ships with Visual Studio, or install standalone)
- VS Code or Visual Studio 2022+
- Git

### Section 3 — Scaffold the Solution

**Step 3.1 — Create solution and projects**
```bash
mkdir DotnetAiAgentMcp && cd DotnetAiAgentMcp
dotnet new sln -n DotnetAiAgentMcp
dotnet new classlib -n HrMcp.Core                    -o src/HrMcp.Core
dotnet new classlib -n HrMcp.Application             -o src/HrMcp.Application
dotnet new classlib -n HrMcp.Infrastructure.Persistence -o src/HrMcp.Infrastructure.Persistence
dotnet new webapi   -n HrMcp.McpServer               -o src/HrMcp.McpServer
dotnet new console  -n HrMcp.Agent                   -o src/HrMcp.Agent
dotnet sln add src/HrMcp.Core src/HrMcp.Application src/HrMcp.Infrastructure.Persistence src/HrMcp.McpServer src/HrMcp.Agent
```

**Step 3.2 — Wire project references (dependency flows inward)**
```bash
dotnet add src/HrMcp.Application                  reference src/HrMcp.Core
dotnet add src/HrMcp.Infrastructure.Persistence   reference src/HrMcp.Core
dotnet add src/HrMcp.McpServer                    reference src/HrMcp.Application
dotnet add src/HrMcp.McpServer                    reference src/HrMcp.Infrastructure.Persistence
dotnet add src/HrMcp.Agent                        reference src/HrMcp.Application
dotnet add src/HrMcp.Agent                        reference src/HrMcp.Infrastructure.Persistence
```

**Step 3.3 — Delete boilerplate**
```bash
rm src/HrMcp.Core/Class1.cs
rm src/HrMcp.Application/Class1.cs
rm src/HrMcp.Infrastructure.Persistence/Class1.cs
```

### Section 4 — Domain Models (`HrMcp.Core`)

Target job board: **USAJobs.gov** (US federal government). Domain fields align with the official USAJobs API (`MatchedObjectDescriptor` + Historic JOA schema).

**Step 4.1 — Folder layout**
```
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

**Step 4.2 — `HiringOrganization.cs`** (maps to USAJobs `OrganizationName` / `DepartmentName`)
```csharp
namespace HrMcp.Core.Entities;

public class HiringOrganization
{
    public int Id { get; set; }

    // USAJobs: OrganizationName — the hiring office e.g. "Space and Naval Warfare Systems Command"
    public string OrganizationName { get; set; } = string.Empty;

    // USAJobs: DepartmentName — cabinet-level parent e.g. "Department of Homeland Security"
    public string DepartmentName { get; set; } = string.Empty;

    public string AgencyDescription { get; set; } = string.Empty;

    public ICollection<Position> Positions { get; set; } = [];
}
```

**Step 4.3 — Enums** (new — USAJobs API value sets)

`AppointmentType.cs`
```csharp
namespace HrMcp.Core.Enums;
public enum AppointmentType { Permanent, Temporary, Term }
```

`WorkSchedule.cs`
```csharp
namespace HrMcp.Core.Enums;
public enum WorkSchedule { FullTime, PartTime, Intermittent, MultipleSchedules }
```

`SecurityClearance.cs`
```csharp
namespace HrMcp.Core.Enums;
public enum SecurityClearance { NotRequired, PublicTrust, Confidential, Secret, TopSecret, TopSecretSCI }
```

`TravelRequirement.cs`
```csharp
namespace HrMcp.Core.Enums;
public enum TravelRequirement { NotRequired, Occasional, Sometimes, Frequent }
```

**Step 4.4 — `Position.cs`** (USAJobs-aligned)

| USAJobs API Field | Property | Notes |
|---|---|---|
| `PositionTitle` | `Title` | |
| `JobSummary` | `Description` | shown in search results |
| `MajorDuties` | `Duties` | full duties narrative |
| `Qualifications` | `Qualifications` | OPM min quals text |
| `JobCategories[].Code` | `OccupationalSeries` | 4-digit e.g. "2210" |
| `PayPlan`+`LowGrade` | `PayGradeMin` | e.g. "GS-09" |
| `PayPlan`+`HighGrade` | `PayGradeMax` | e.g. "GS-11" |
| `appointmentType` | `AppointmentType` | enum |
| `workSchedule` | `WorkSchedule` | enum |
| `positionOpenDate` | `OpenDate` | |
| `positionCloseDate` | `CloseDate` | nullable |
| `WhoMayApply.Name` | `WhoMayApply` | |
| `PositionLocation[].CityName` | `DutyLocation` | |
| `teleworkEligible` | `TeleworkEligible` | bool |
| `travelRequirement` | `TravelRequired` | enum |
| `securityClearance` | `SecurityClearance` | enum |
| `supervisoryStatus` | `SupervisoryStatus` | bool |
| `relocationExpensesReimbursed` | `RelocationAuthorized` | bool |
| `drugTestRequired` | `DrugTestRequired` | bool |

```csharp
using HrMcp.Core.Enums;

namespace HrMcp.Core.Entities;

public class Position
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Duties { get; set; } = string.Empty;
    public string Qualifications { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public string OccupationalSeries { get; set; } = string.Empty;
    public string PayGradeMin { get; set; } = string.Empty;
    public string PayGradeMax { get; set; } = string.Empty;
    public AppointmentType AppointmentType { get; set; } = AppointmentType.Permanent;
    public WorkSchedule WorkSchedule { get; set; } = WorkSchedule.FullTime;
    public DateTime OpenDate { get; set; } = DateTime.UtcNow;
    public DateTime? CloseDate { get; set; }
    public string WhoMayApply { get; set; } = "Open to US Citizens";
    public string DutyLocation { get; set; } = string.Empty;
    public bool TeleworkEligible { get; set; } = false;
    public TravelRequirement TravelRequired { get; set; } = TravelRequirement.NotRequired;
    public SecurityClearance SecurityClearance { get; set; } = SecurityClearance.NotRequired;
    public bool SupervisoryStatus { get; set; } = false;
    public bool RelocationAuthorized { get; set; } = false;
    public bool DrugTestRequired { get; set; } = false;
    public int HiringOrganizationId { get; set; }
    public HiringOrganization HiringOrganization { get; set; } = null!;
    public PositionRemuneration PositionRemuneration { get; set; } = null!;
}
```

**Step 4.5 — `PositionRemuneration.cs`** (maps to USAJobs `PositionRemuneration` object)
```csharp
namespace HrMcp.Core.Entities;

public class PositionRemuneration
{
    public int Id { get; set; }

    // USAJobs: MinimumRange — e.g. "68405"
    public decimal MinimumRange { get; set; }

    // USAJobs: MaximumRange
    public decimal MaximumRange { get; set; }

    // USAJobs: RateIntervalCode — "PA"=Per Annum, "PH"=Per Hour, "PD"=Per Day
    public string RateIntervalCode { get; set; } = "PA";

    // Human-readable equivalent e.g. "Per Year"
    public string Description { get; set; } = "Per Year";

    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;
}
```

**Step 4.6 — `IPositionRepository.cs`**
```csharp
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

**Step 4.7 — `IHiringOrganizationRepository.cs`**
```csharp
using HrMcp.Core.Entities;

namespace HrMcp.Core.Interfaces;

public interface IHiringOrganizationRepository
{
    Task<IEnumerable<HiringOrganization>> GetAllAsync(CancellationToken ct = default);
    Task<HiringOrganization?> GetByIdAsync(int id, CancellationToken ct = default);
}
```

### Section 5 — Application Layer (`HrMcp.Application`)

**Step 5.1 — Folder layout**
```
src/HrMcp.Application/
  Services/
    PositionService.cs
    HiringOrganizationService.cs
```

**Step 5.2 — `PositionService.cs`**
```csharp
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

**Step 5.3 — `HiringOrganizationService.cs`**
```csharp
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

### Section 6 — Infrastructure (`HrMcp.Infrastructure.Persistence`)

**Step 6.1 — Add NuGet packages**
```bash
dotnet add src/HrMcp.Infrastructure.Persistence package Microsoft.EntityFrameworkCore.SqlServer --version 9.*
dotnet add src/HrMcp.Infrastructure.Persistence package Microsoft.EntityFrameworkCore.Design --version 9.*
dotnet add src/HrMcp.McpServer package Microsoft.EntityFrameworkCore.Design --version 9.*
```
> Note: EF Core 9 supports .NET 10. EF Core 10 preview may be used when stable.

**Step 6.2 — Folder layout**
```
src/HrMcp.Infrastructure.Persistence/
  HrDbContext.cs
  DbSeeder.cs
  Repositories/
    HiringOrganizationRepository.cs
    PositionRepository.cs
  DependencyInjection.cs
```

**Step 6.3 — `HrDbContext.cs`**
```csharp
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
        modelBuilder.Entity<PositionRemuneration>()
            .HasOne(r => r.Position)
            .WithOne(p => p.PositionRemuneration)
            .HasForeignKey<PositionRemuneration>(r => r.PositionId);
    }
}
```

**Step 6.4 — `PositionRepository.cs`**
```csharp
using HrMcp.Core.Entities;
using HrMcp.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HrMcp.Infrastructure.Persistence.Repositories;

public class PositionRepository(HrDbContext db) : IPositionRepository
{
    private IQueryable<Position> BaseQuery =>
        db.Positions.Include(p => p.HiringOrganization).Include(p => p.PositionRemuneration);

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

**Step 6.5 — `HiringOrganizationRepository.cs`**
```csharp
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

**Step 6.6 — `DbSeeder.cs`** (federal agencies under DHS + USAJobs-format positions)
```csharp
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
                Title = "IT Specialist (SYSADMIN)", Description = "Manages and maintains enterprise IT systems and infrastructure.",
                Duties = "Administers Windows and Linux servers. Manages Active Directory, DNS, and DHCP. Monitors system performance and resolves incidents.",
                Qualifications = "GS-11: One year of specialized experience equivalent to GS-09 managing enterprise server environments.",
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
                Title = "Supervisory IT Specialist (INFOSEC)", Description = "Leads cybersecurity operations and oversees the agency information security program.",
                Duties = "Directs incident response and FISMA compliance. Manages a team of 8 IT security specialists. Briefs senior leadership on cyber risk.",
                Qualifications = "GS-14: One year of specialized experience equivalent to GS-13 leading an agency-wide information security program.",
                IsOpen = false, OccupationalSeries = "2210", PayGradeMin = "GS-14", PayGradeMax = "GS-14",
                AppointmentType = AppointmentType.Permanent, WorkSchedule = WorkSchedule.FullTime,
                OpenDate = today.AddDays(-30), CloseDate = today.AddDays(-16), WhoMayApply = "Open to current federal employees only",
                DutyLocation = "Washington, DC", TeleworkEligible = true,
                SecurityClearance = SecurityClearance.TopSecret, TravelRequired = TravelRequirement.Occasional,
                SupervisoryStatus = true, RelocationAuthorized = true, DrugTestRequired = false,
                HiringOrganizationId = oit.Id,
                PositionRemuneration = new() { MinimumRange = 139_395, MaximumRange = 181_216 }
            },
            new() {
                Title = "Human Resources Specialist (Recruitment)", Description = "Manages full-cycle federal recruitment and staffing operations.",
                Duties = "Develops USAJobs announcements. Rates and ranks applicants using OPM qualification standards. Advises hiring managers on merit promotion procedures.",
                Qualifications = "GS-09: One year of specialized experience equivalent to GS-07 in federal staffing or classification.",
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
                Title = "Management Analyst", Description = "Conducts organizational studies and evaluates federal program effectiveness.",
                Duties = "Analyzes agency workflow and recommends process improvements. Prepares management reports and briefing materials for senior leadership.",
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
                Title = "Financial Analyst", Description = "Supports federal budget formulation and execution for the agency's appropriated funds.",
                Duties = "Prepares budget justifications and spending plans. Monitors obligations and expenditures against appropriations. Coordinates with OMB on apportionment requests.",
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

**Step 6.7 — `DependencyInjection.cs`**
```csharp
using HrMcp.Core.Interfaces;
using HrMcp.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HrMcp.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<HrDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IHiringOrganizationRepository, HiringOrganizationRepository>();
        return services;
    }
}
```

### Section 7 — Wire McpServer Startup

**Step 7.1 — `appsettings.json`**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=HrMcpDb;Trusted_Connection=True;"
  },
  "Urls": "http://localhost:5100"
}
```

**Step 7.2 — `Program.cs`**
```csharp
using HrMcp.Application.Services;
using HrMcp.Infrastructure.Persistence;

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

### Section 8 — EF Migrations

```bash
dotnet add src/HrMcp.McpServer package Microsoft.EntityFrameworkCore.SqlServer --version 9.*

dotnet ef migrations add InitialCreate \
  --project src/HrMcp.Infrastructure.Persistence \
  --startup-project src/HrMcp.McpServer \
  --output-dir Migrations

dotnet ef database update \
  --project src/HrMcp.Infrastructure.Persistence \
  --startup-project src/HrMcp.McpServer
```

### Section 9 — Verify
```bash
dotnet build DotnetAiAgentMcp.sln
dotnet run --project src/HrMcp.McpServer
# Server starts, runs migrations, seeds 4 hiring organizations + 5 positions
```

---

---

# Part 2 — Introduction to Model Context Protocol (MCP)

**File:** `blogs/series-1-ai-agent-mcp/part-2-intro-to-mcp.md`

## Goals
- No code — pure concepts
- Explain the problem MCP solves
- Map MCP components to familiar .NET concepts
- Motivate why the next two parts build what they build

## Sections & Steps

### Section 1 — The Problem Before MCP
- Every AI assistant needed bespoke integration for every tool
- Diagram: N tools × M AI clients = N×M integrations
- MCP reduces this to N + M

### Section 2 — What MCP Is
- Open protocol from Anthropic (November 2024), now industry-standard
- JSON-RPC 2.0 over a transport (stdio, HTTP/SSE, WebSocket)
- Three primitive types:
  - **Tools** — functions the AI can call
  - **Resources** — data the AI can read (files, DB rows)
  - **Prompts** — reusable prompt templates

### Section 3 — MCP Architecture Diagram
```
┌─────────────────────────────────────┐
│          MCP Host                   │
│  (Claude Desktop / VS Code Copilot) │
│  ┌──────────────┐                   │
│  │  MCP Client  │                   │
│  └──────┬───────┘                   │
└─────────┼───────────────────────────┘
          │  JSON-RPC 2.0 (stdio or HTTP/SSE)
┌─────────▼───────────────────────────┐
│        MCP Server                   │
│   (HrMcp.McpServer — our project)   │
│  Tool: GetOpenPositions             │
│  Tool: GetHiringOrganizations       │
│  Tool: GetPositionsByOrganization   │
│  Tool: WriteJobDescription          │
└─────────────────────────────────────┘
```

### Section 4 — Transports Explained
- **stdio** — MCP host spawns server as child process; communicates via stdin/stdout. For Claude Desktop.
- **HTTP/SSE** — Server runs as a web server; clients connect over HTTP. For VS Code Copilot and remote use.

### Section 5 — MCP vs Other Integration Patterns

| Pattern | Problem | MCP Advantage |
|---------|---------|---------------|
| OpenAI Function Calling | Tied to OpenAI API | Works with any MCP host |
| LangChain Tools | Python-first | Native .NET support |
| Custom REST APIs | AI must know your schema | MCP auto-discovers tools |
| Semantic Kernel Plugins | Microsoft-only | MCP is open standard |

### Section 6 — The .NET Angle
- `ModelContextProtocol` NuGet package (Microsoft-maintained)
- Attribute-based tool registration (`[McpServerTool]`) — maps naturally to .NET method patterns
- DI-friendly — tools are registered as services
- Works with `Microsoft.Extensions.AI` and Semantic Kernel

### Section 7 — What We'll Build in Parts 3–5
- Preview tool list: `GetOpenPositions`, `GetHiringOrganizations`, `GetPositionsByOrganization`, `WriteJobDescription`
- Show example of Claude Desktop calling `GetOpenPositions` and getting JSON back
- Tease the Job Description Writer: LLM + HR data = auto-generated JDs

---

---

# Part 3 — Building an MCP Server in .NET 10

**File:** `blogs/series-1-ai-agent-mcp/part-3-mcp-server-dotnet.md`

## Goals
- Install `ModelContextProtocol` SDK
- Implement 4 MCP tools backed by the Application layer from Part 1
- Configure both stdio (Claude Desktop) and HTTP/SSE (VS Code Copilot) transports
- Test with MCP Inspector

## Sections & Steps

### Section 1 — Add the MCP SDK

```bash
dotnet add src/HrMcp.McpServer package ModelContextProtocol --version 0.*
dotnet add src/HrMcp.McpServer package ModelContextProtocol.AspNetCore --version 0.*
```

### Section 2 — Tool Implementation

**Folder layout**
```
src/HrMcp.McpServer/
  Tools/
    PositionTools.cs
    HiringOrganizationTools.cs
    JobDescriptionTools.cs
```

**`PositionTools.cs`**
```csharp
using System.ComponentModel;
using HrMcp.Application.Services;
using HrMcp.Core.Entities;
using ModelContextProtocol.Server;

namespace HrMcp.McpServer.Tools;

[McpServerToolType]
public class PositionTools(PositionService positionService)
{
    [McpServerTool, Description(
        "Returns all open job positions at the company. " +
        "Use this when the user asks what jobs are available or what roles are hiring.")]
    public async Task<IEnumerable<PositionDto>> GetOpenPositions(CancellationToken ct)
    {
        var positions = await positionService.GetOpenPositionsAsync(ct);
        return positions.Select(PositionDto.From);
    }

    [McpServerTool, Description(
        "Returns all job positions (open and filled) for a specific hiring organization. " +
        "Provide the organization ID. Use GetHiringOrganizations first if you don't know the ID.")]
    public async Task<IEnumerable<PositionDto>> GetPositionsByOrganization(
        [Description("The numeric ID of the hiring organization")] int organizationId,
        CancellationToken ct)
    {
        var positions = await positionService.GetPositionsByOrganizationAsync(organizationId, ct);
        return positions.Select(PositionDto.From);
    }

    [McpServerTool, Description(
        "Returns full details for a single position including salary range.")]
    public async Task<PositionDto?> GetPositionById(
        [Description("The numeric ID of the position")] int positionId,
        CancellationToken ct)
    {
        var position = await positionService.GetPositionByIdAsync(positionId, ct);
        return position is null ? null : PositionDto.From(position);
    }
}

public record PositionDto(
    int Id, string Title, string Description, bool IsOpen,
    string OrganizationName, string DepartmentName,
    decimal MinimumRange, decimal MaximumRange, string RateIntervalCode)
{
    public static PositionDto From(Position p) => new(
        p.Id, p.Title, p.Description, p.IsOpen,
        p.HiringOrganization.OrganizationName,
        p.HiringOrganization.DepartmentName,
        p.PositionRemuneration.MinimumRange,
        p.PositionRemuneration.MaximumRange,
        p.PositionRemuneration.RateIntervalCode);
}
```

**`HiringOrganizationTools.cs`**
```csharp
using System.ComponentModel;
using HrMcp.Application.Services;
using HrMcp.Core.Entities;
using ModelContextProtocol.Server;

namespace HrMcp.McpServer.Tools;

[McpServerToolType]
public class HiringOrganizationTools(HiringOrganizationService orgService)
{
    [McpServerTool, Description(
        "Returns all hiring organizations with their IDs and descriptions. " +
        "Use this to look up organization IDs before calling organization-specific tools.")]
    public async Task<IEnumerable<HiringOrganizationDto>> GetHiringOrganizations(CancellationToken ct)
    {
        var orgs = await orgService.GetAllOrganizationsAsync(ct);
        return orgs.Select(HiringOrganizationDto.From);
    }
}

public record HiringOrganizationDto(int Id, string OrganizationName, string DepartmentName, string AgencyDescription, int PositionCount)
{
    public static HiringOrganizationDto From(HiringOrganization o) => new(
        o.Id, o.OrganizationName, o.DepartmentName, o.AgencyDescription, o.Positions.Count);
}
```

**`JobDescriptionTools.cs`** (stub — LLM added in Part 4)
```csharp
using System.ComponentModel;
using HrMcp.Application.Services;
using ModelContextProtocol.Server;

namespace HrMcp.McpServer.Tools;

[McpServerToolType]
public class JobDescriptionTools(PositionService positionService)
{
    [McpServerTool, Description(
        "Generates a professional job description for a position. " +
        "Provide the position ID to fetch the role details.")]
    public async Task<string> WriteJobDescription(
        [Description("The numeric ID of the position")] int positionId,
        CancellationToken ct)
    {
        var position = await positionService.GetPositionByIdAsync(positionId, ct);
        if (position is null) return $"Position {positionId} not found.";

        // Stub — full LLM integration added in Part 4
        return $"""
            ## {position.Title}
            **Organization:** {position.HiringOrganization.OrganizationName}
            **Department:** {position.HiringOrganization.DepartmentName}
            **Salary:** {position.PositionRemuneration.MinimumRange:N0} – {position.PositionRemuneration.MaximumRange:N0} ({position.PositionRemuneration.RateIntervalCode})

            {position.Description}

            [Full AI-generated description will be added in Part 4]
            """;
    }
}
```

### Section 3 — Configure Transports

**Updated `Program.cs`**
```csharp
using HrMcp.Application.Services;
using HrMcp.Infrastructure.Persistence;
using HrMcp.McpServer.Tools;

var builder = WebApplication.CreateBuilder(args);

var isStdio = args.Contains("--stdio") ||
              Environment.GetEnvironmentVariable("MCP_TRANSPORT") == "stdio";

if (isStdio)
    builder.Logging.SetMinimumLevel(LogLevel.None); // protect stdout for JSON-RPC

builder.Services.AddPersistence(
    builder.Configuration.GetConnectionString("DefaultConnection")!);
builder.Services.AddScoped<PositionService>();
builder.Services.AddScoped<HiringOrganizationService>();

builder.Services.AddMcpServer()
    .WithTools<PositionTools>()
    .WithTools<HiringOrganizationTools>()
    .WithTools<JobDescriptionTools>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db);
}

if (isStdio)
{
    await app.RunMcpStdioAsync();
    return;
}

app.MapMcp("/mcp");
app.Run();
```

**`appsettings.json`**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=HrMcpDb;Trusted_Connection=True;"
  },
  "Urls": "http://localhost:5100"
}
```

### Section 4 — Test with MCP Inspector

```bash
# Terminal 1
dotnet run --project src/HrMcp.McpServer

# Terminal 2
npx @modelcontextprotocol/inspector http://localhost:5100/mcp
```

Walkthrough in blog:
- Inspector shows 4 tools listed
- Call `GetHiringOrganizations` → 4 organizations with IDs
- Call `GetOpenPositions` → 5 open positions with remuneration ranges
- Call `GetPositionById` id=1 → full detail
- Call `WriteJobDescription` positionId=1 → stub output

### Section 5 — Verify
```bash
dotnet build DotnetAiAgentMcp.sln
dotnet run --project src/HrMcp.McpServer
```

---

---

# Part 4 — AI Agent with Microsoft.Extensions.AI + Ollama

**File:** `blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md`

## Goals
- Set up Ollama locally with `llama3.2`
- Use `Microsoft.Extensions.AI` (`IChatClient`) for model-agnostic agent code
- Connect agent to MCP server tools via `McpClientTool`
- Build multi-turn console agent loop
- Upgrade `WriteJobDescription` to use real LLM output
- Demonstrate streaming responses

## Sections & Steps

### Section 1 — Set Up Ollama

```bash
ollama pull llama3.2
ollama run llama3.2   # sanity check
curl http://localhost:11434/api/generate -d '{"model":"llama3.2","prompt":"ping"}'
```

### Section 2 — Add Packages to HrMcp.Agent

```bash
dotnet add src/HrMcp.Agent package Microsoft.Extensions.AI --version 9.*
dotnet add src/HrMcp.Agent package Microsoft.Extensions.AI.Ollama --version 9.*
dotnet add src/HrMcp.Agent package ModelContextProtocol --version 0.*
dotnet add src/HrMcp.Agent package Microsoft.Extensions.Hosting
```

### Section 3 — Agent Project Structure

```
src/HrMcp.Agent/
  Agent/
    HrAgent.cs
  Program.cs
```

### Section 4 — `HrAgent.cs`

```csharp
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace HrMcp.Agent.Agent;

public class HrAgent(IChatClient chatClient, IList<McpClientTool> tools)
{
    private readonly List<ChatMessage> _history = [
        new(ChatRole.System, """
            You are an HR assistant with access to real-time HR data.
            Always use the available tools to look up positions, departments,
            and salary ranges before answering. Never guess salary figures.
            When asked to write a job description, use the WriteJobDescription tool.
            """)
    ];

    public async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("HR Agent ready. Type your question (or 'exit' to quit):");
        Console.WriteLine();

        while (!ct.IsCancellationRequested)
        {
            Console.Write("You: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) ||
                input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            _history.Add(new ChatMessage(ChatRole.User, input));

            var response = await chatClient.GetResponseAsync(
                _history,
                new ChatOptions { Tools = [.. tools] },
                ct);

            _history.AddMessages(response);

            Console.Write("Agent: ");
            foreach (var msg in response.Messages.Where(m => m.Role == ChatRole.Assistant))
                foreach (var content in msg.Contents.OfType<TextContent>())
                    Console.WriteLine(content.Text);

            Console.WriteLine();
        }
    }
}
```

### Section 5 — `Program.cs` for HrMcp.Agent

```csharp
using HrMcp.Agent.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        services.AddChatClient(new OllamaChatClient(
            new Uri("http://localhost:11434"), "llama3.2"));

        services.AddSingleton(async _ =>
        {
            var mcpClient = await McpClientFactory.CreateAsync(
                new SseServerTransportOptions
                {
                    Endpoint = new Uri("http://localhost:5100/mcp")
                });
            return await mcpClient.ListToolsAsync();
        });
    })
    .Build();

var tools = await host.Services.GetRequiredService<Task<IList<McpClientTool>>>();
var chatClient = host.Services.GetRequiredService<IChatClient>();
var agent = new HrAgent(chatClient, tools);
await agent.RunAsync();
```

### Section 6 — Upgrade WriteJobDescription with Real LLM

**Add packages to McpServer**
```bash
dotnet add src/HrMcp.McpServer package Microsoft.Extensions.AI --version 9.*
dotnet add src/HrMcp.McpServer package Microsoft.Extensions.AI.Ollama --version 9.*
```

**Updated `JobDescriptionTools.cs`**
```csharp
using System.ComponentModel;
using HrMcp.Application.Services;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace HrMcp.McpServer.Tools;

[McpServerToolType]
public class JobDescriptionTools(PositionService positionService, IChatClient chatClient)
{
    [McpServerTool, Description(
        "Generates a professional job description for a position. " +
        "Provide the position ID to fetch the role details.")]
    public async Task<string> WriteJobDescription(
        [Description("The numeric ID of the position")] int positionId,
        CancellationToken ct)
    {
        var position = await positionService.GetPositionByIdAsync(positionId, ct);
        if (position is null) return $"Position {positionId} not found.";

        var prompt = $"""
            Write a professional job description for the following role.
            Include: role summary, key responsibilities (5 bullets),
            required qualifications (4 bullets), and a compensation section.

            Title: {position.Title}
            Organization: {position.HiringOrganization.OrganizationName}
            Department: {position.HiringOrganization.DepartmentName}
            Summary: {position.Description}
            Salary: {position.PositionRemuneration.MinimumRange:N0} – {position.PositionRemuneration.MaximumRange:N0} ({position.PositionRemuneration.RateIntervalCode})
            """;

        var response = await chatClient.GetResponseAsync(prompt, cancellationToken: ct);
        return response.Text;
    }
}
```

**Register `IChatClient` in `McpServer/Program.cs`**
```csharp
builder.Services.AddChatClient(new OllamaChatClient(
    new Uri("http://localhost:11434"), "llama3.2"));
```

### Section 7 — Sample Conversation (shown in blog)

```
You: What positions are open in the Office of Information Technology?
Agent: [calls GetHiringOrganizations → GetPositionsByOrganization]
       2 open positions in Office of Information Technology (DHS):
       1. IT Specialist (SYSADMIN) — GS-09 to GS-11, $68,405–$107,590 PA
       2. Supervisory IT Specialist (INFOSEC) — GS-14, $139,395–$181,216 PA

You: Write a job description for the IT Specialist role
Agent: [calls WriteJobDescription(1)]
       ## IT Specialist (SYSADMIN)
       Organization: Office of Information Technology | Department: Department of Homeland Security
       ...
```

### Section 8 — Verify

```bash
# Terminal 1
dotnet run --project src/HrMcp.McpServer

# Terminal 2
dotnet run --project src/HrMcp.Agent
```

---

---

# Part 5 — Claude Desktop Integration & End-to-End Demo

**File:** `blogs/series-1-ai-agent-mcp/part-5-claude-desktop-integration.md`

## Goals
- Configure Claude Desktop to launch the MCP server via stdio
- Walk through a live HR session in Claude Desktop
- Show VS Code Copilot as alternative MCP client (HTTP/SSE)
- Cover debugging techniques
- Close the series with next steps

## Sections & Steps

### Section 1 — How stdio Transport Works with Claude Desktop
- Diagram: Claude Desktop spawns the server as a child process
- Claude writes JSON-RPC to stdin; server responds on stdout
- Why stdout must be clean (any stray log line breaks the protocol)
- Claude Desktop reads `claude_desktop_config.json` on startup

### Section 2 — Publish the MCP Server

```bash
dotnet publish src/HrMcp.McpServer \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o publish/McpServer
```

### Section 3 — Configure Claude Desktop

**Config file location**
- Windows: `%APPDATA%\Claude\claude_desktop_config.json`
- macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`

**`claude_desktop_config.json`**
```json
{
  "mcpServers": {
    "hr-mcp": {
      "command": "C:\\path\\to\\publish\\McpServer\\HrMcp.McpServer.exe",
      "args": ["--stdio"],
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Production"
      }
    }
  }
}
```

**Restart Claude Desktop** — look for the hammer icon to confirm tools loaded.

### Section 4 — Live Demo Walkthrough (with screenshots)

| Prompt | Tools called | Result |
|--------|-------------|--------|
| "What hiring organizations do we have and how many open roles in each?" | `GetHiringOrganizations`, `GetOpenPositions` | Table: OIT (1 open), OHR (1 open), OPD (1 open), OCFO (1 open) |
| "Show me all open IT positions with salary ranges" | `GetHiringOrganizations`, `GetPositionsByOrganization` | IT Specialist GS-09/11 ($68k–$107k PA) |
| "Write a job posting for the Senior Software Engineer" | `GetPositionById`, `WriteJobDescription` | Full AI-generated JD |

### Section 5 — VS Code Copilot (HTTP/SSE Transport)

```bash
dotnet run --project src/HrMcp.McpServer
```

Add `.vscode/mcp.json`:
```json
{
  "mcp": {
    "servers": {
      "hr-mcp": {
        "type": "sse",
        "url": "http://localhost:5100/mcp"
      }
    }
  }
}
```

Use in Copilot Chat: `@hr-mcp What positions are open?`

### Section 6 — Debugging

| Problem | Cause | Fix |
|---------|-------|-----|
| Tools don't appear in Claude Desktop | Config path wrong or invalid JSON | Validate JSON; check exact file path |
| stdout corrupted / JSON parse errors | Server logs going to stdout | Ensure `LogLevel.None` when `--stdio` is passed |
| Connection refused | Wrong executable path | Claude spawns server — verify `command` path in config |
| Database not found | LocalDB not initialized | Run `sqllocaldb start MSSQLLocalDB` |
| Tools return empty | Seed not run | Delete DB, restart server to re-seed |

**View MCP logs**
```bash
# macOS
tail -f ~/Library/Logs/Claude/mcp*.log

# Windows
Get-Content "$env:APPDATA\Claude\logs\mcp*.log" -Wait
```

### Section 7 — Series Recap & Next Steps

**What was built:**
- Clean Architecture .NET 10 solution with real HR data
- 4 MCP tools consumed by Claude Desktop, VS Code Copilot, and a custom .NET agent
- AI-powered job description writer backed by local Ollama llama3.2

**What to explore next:**
- Authentication — add API key validation to the HTTP/SSE endpoint
- MCP Resources — expose HR org chart as a browsable resource
- MCP Prompts — pre-built prompt templates for common HR queries
- Azure deployment — host MCP server on Azure Container Apps
- Semantic Kernel — swap `Microsoft.Extensions.AI` for SK plugin-based orchestration
- Series 2 — Full Angular 20 / .NET 10 frontend for the HR system

---

---

## File Creation Order

1. `blogs/series-1-ai-agent-mcp/part-1-clean-architecture-hr-domain.md` ✅
2. `blogs/series-1-ai-agent-mcp/part-2-intro-to-mcp.md`
3. `blogs/series-1-ai-agent-mcp/part-3-mcp-server-dotnet.md`
4. `blogs/series-1-ai-agent-mcp/part-4-ai-agent-extensions-ai.md`
5. `blogs/series-1-ai-agent-mcp/part-5-claude-desktop-integration.md`
6. `blogs/series-1-ai-agent-mcp/part-6-mcp-security-oidc.md`

## Verification Checklist

- [ ] Each post has a clear intro, step-by-step sections, and a "Next Up" footer
- [ ] All code blocks specify the language (`csharp`, `bash`, `json`)
- [ ] Every `dotnet add package` command includes a version constraint
- [ ] File paths in code match the solution structure exactly
- [ ] Part 3's `WriteJobDescription` starts as a stub, upgraded in Part 4
- [ ] Part 5 includes both Claude Desktop (stdio) and VS Code Copilot (HTTP/SSE) instructions
- [ ] Part 6 includes OIDC/JWT auth flow (resource server + client credentials)
- [ ] Debugging section covers stdout contamination pitfall

---

# Future Draft — Series 2: Better Job Description Quality

**Working title:** AI Agents & MCP with .NET 10 — Series 2 (Job Description Quality)

**Goal**
- Improve `WriteJobDescription` quality using richer seed data and example-based grounding.
- Keep backward compatibility with existing MCP tools while adding an "advanced draft" path.

**High-level outcomes**
- Larger and cleaner USAJobs seed dataset for stronger retrieval coverage.
- New reference-selection logic: fetch 3 to 5 similar positions before generating a draft.
- Prompt template and output format improvements (consistent sections and tone).
- Evaluation loop for measuring quality improvements over Series 1 baseline.

## Part A — Data Foundation (placeholder)

**Focus:** more representative seed data for retrieval and style grounding.

**Planned topics**
- Expand `tools/UsaJobsFetcher` to support pagination and configurable query parameters.
- Build a repeatable data curation flow:
    - deduplicate by title + organization + grade band
    - remove low-signal or incomplete records
    - normalize fields used by prompt templates
- Create two seed profiles:
    - **dev-small** (fast local iteration)
    - **demo-large** (richer generation quality)
- Add seed metadata file (generated timestamp, source filters, row counts).

**Deliverables (placeholder)**
- `data/usajobs-seed-dev.json`
- `data/usajobs-seed-large.json`
- `docs/seeding-strategy.md`

## Part B — Reference-Aware Draft Generation (placeholder)

**Focus:** before generating a new job description, retrieve a few strong examples.

**Planned workflow**
1. Load target position by ID.
2. Retrieve similar positions by weighted match:
     - occupational series
     - pay grade band
     - work schedule / appointment type
     - organization or department proximity
3. Pick top 3 to 5 references.
4. Generate draft with structured prompt sections:
     - role summary
     - key duties
     - qualifications
     - pay and conditions
5. Return both the draft and a compact reference summary for traceability.

**Deliverables (placeholder)**
- `src/HrMcp.Application/Services/PositionSimilarityService.cs`
- `src/HrMcp.McpServer/Tools/JobDescriptionTools.cs` (new reference-aware path)
- New MCP tool option: `WriteJobDescriptionAdvanced` (or mode flag on existing tool)

## Part C — Prompting and Output Quality (placeholder)

**Focus:** consistently produce publish-ready, USAJobs-style descriptions.

**Planned topics**
- Introduce versioned prompt templates.
- Add domain constraints (federal tone, non-fabrication, no salary guessing).
- Add style profiles (concise internal posting vs public USAJobs narrative).
- Add optional post-processing for formatting consistency.

## Part D — Quality Evaluation (placeholder)

**Focus:** measure improvement, not just subjective impression.

**Planned metrics**
- Structural completeness (required sections present).
- Factual grounding score (matches source position fields).
- Consistency score across repeated runs.
- Human review checklist for recruiter readability.

**Planned assets**
- `docs/evaluation/job-description-rubric.md`
- `docs/evaluation/baseline-vs-series2.md`

## Post Ideas (placeholder)

1. **Series 2 Part 1:** Scaling USAJobs seed data for retrieval quality
2. **Series 2 Part 2:** Similar-position retrieval before draft generation
3. **Series 2 Part 3:** Reference-aware prompt design for federal job posts
4. **Series 2 Part 4:** Objective quality scoring and side-by-side evaluation
5. **Series 2 Part 5:** Hardening for production (latency, caching, guardrails)

## Notes for Future Execution

- Keep Series 1 behavior available as baseline mode for easy comparison.
- Avoid markdown tables in blog posts; use bullets/prose sections.
- Prefer incremental rollout: enable advanced generation behind a feature flag first.
