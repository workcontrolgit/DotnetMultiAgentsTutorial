// Orchestration/HrOrchestrator.cs
using Hr.SelectorOrchestrator.Agents;

namespace Hr.SelectorOrchestrator.Orchestration;

/// <summary>
/// Top-level orchestrator that routes each user query to the appropriate
/// specialist agent and streams the response back to the console.
///
/// Pattern: selector multi-agent — one agent handles each turn, chosen by the router.
/// </summary>
public sealed class HrOrchestrator(
    AgentRouter router,
    SpecialistAgent positionSearchAgent,
    SpecialistAgent jobDescriptionAgent,
    SpecialistAgent orgSummaryAgent,
    SpecialistAgent complianceAgent,
    SpecialistAgent generalAgent)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("HR Orchestrator ready. Multiple specialist agents are standing by.");
        Console.WriteLine("Type 'exit' to quit.\n");

        while (!ct.IsCancellationRequested)
        {
            Console.Write("You: ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            // Step 1 — classify intent
            var intent = await router.ClassifyAsync(input, ct);
            var agent = SelectAgent(intent);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[Router → {agent.Name}]");
            Console.ResetColor();

            // Step 2 — delegate to specialist
            var reply = await agent.HandleAsync(input, ct);

            Console.WriteLine($"\nAssistant ({agent.Name}): {reply}\n");
        }
    }

    private SpecialistAgent SelectAgent(AgentIntent intent) => intent switch
    {
        AgentIntent.PositionSearch => positionSearchAgent,
        AgentIntent.JobDescription => jobDescriptionAgent,
        AgentIntent.OrgSummary     => orgSummaryAgent,
        AgentIntent.Compliance     => complianceAgent,
        _                          => generalAgent,
    };
}
