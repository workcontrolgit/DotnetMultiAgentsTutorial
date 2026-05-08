// Orchestration/AgentIntent.cs
namespace Hr.Orchestrator.Orchestration;

/// <summary>
/// Represents the classified intent of a user query.
/// The router maps each incoming message to one of these values,
/// and the orchestrator delegates to the matching specialist agent.
/// </summary>
public enum AgentIntent
{
    /// <summary>User wants to find, list, or inspect job positions.</summary>
    PositionSearch,

    /// <summary>User wants to draft or generate a job description.</summary>
    JobDescription,

    /// <summary>User wants information about hiring organizations or departments.</summary>
    OrgSummary,

    /// <summary>User wants to check OPM compliance for a position or job announcement.</summary>
    Compliance,

    /// <summary>Catch-all for greetings, clarifications, or ambiguous queries.</summary>
    General,
}
