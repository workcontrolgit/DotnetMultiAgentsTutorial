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
