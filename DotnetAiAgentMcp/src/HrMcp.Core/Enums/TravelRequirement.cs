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
