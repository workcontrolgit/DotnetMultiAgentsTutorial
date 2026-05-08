// src/Hr.Core/Enums/AnnouncementStatus.cs
namespace Hr.Core.Enums;

public enum AnnouncementStatus
{
    /// <summary>Generated but not yet compliance-checked.</summary>
    Draft,

    /// <summary>All 7 OPM compliance rules passed.</summary>
    CompliancePassed,

    /// <summary>One or more compliance rules failed; revision required.</summary>
    ComplianceFailed,

    /// <summary>Approved and posted on USAJobs.</summary>
    Published
}
