// src/Hr.EvaluatorOrchestrator/Models/EvaluationResult.cs
namespace Hr.EvaluatorOrchestrator.Models;

/// <summary>
/// Structured output returned by the EvaluatorAgent after scoring a draft.
/// </summary>
public sealed record EvaluationResult(int Score, Dictionary<string, string> Feedback)
{
    /// <summary>Draft meets the quality threshold when score ≥ 80.</summary>
    public bool MeetsThreshold => Score >= 80;
}
