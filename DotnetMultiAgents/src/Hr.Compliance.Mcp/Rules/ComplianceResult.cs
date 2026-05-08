// Rules/ComplianceResult.cs
namespace Hr.Compliance.Mcp.Rules;

public enum ComplianceStatus { Pass, Warning, Fail }

/// <summary>Result of a single OPM compliance rule check.</summary>
public record ComplianceResult(
    string RuleName,
    ComplianceStatus Status,
    string Message)
{
    public static ComplianceResult Pass(string ruleName) =>
        new(ruleName, ComplianceStatus.Pass, "OK");

    public static ComplianceResult Warn(string ruleName, string message) =>
        new(ruleName, ComplianceStatus.Warning, message);

    public static ComplianceResult Fail(string ruleName, string message) =>
        new(ruleName, ComplianceStatus.Fail, message);
}

/// <summary>
/// Aggregated compliance report for a single position.
/// OverallStatus is Fail if any rule failed, Warning if any warned, Pass otherwise.
/// </summary>
public record ComplianceReport(
    int PositionId,
    string PositionTitle,
    string OccupationalSeries,
    ComplianceStatus OverallStatus,
    IReadOnlyList<ComplianceResult> Results)
{
    public IEnumerable<ComplianceResult> Failures  => Results.Where(r => r.Status == ComplianceStatus.Fail);
    public IEnumerable<ComplianceResult> Warnings  => Results.Where(r => r.Status == ComplianceStatus.Warning);
    public IEnumerable<ComplianceResult> Passes    => Results.Where(r => r.Status == ComplianceStatus.Pass);
}
