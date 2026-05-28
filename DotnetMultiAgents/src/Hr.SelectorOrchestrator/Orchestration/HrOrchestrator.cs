using Hr.SelectorOrchestrator.Agents;
using Hr.SelectorOrchestrator.Ui;

namespace Hr.SelectorOrchestrator.Orchestration;

/// <summary>
/// Top-level orchestrator that routes each user query to the appropriate
/// specialist agent and renders the response to the console.
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
        ConsoleRenderer.RenderWelcome();

        while (!ct.IsCancellationRequested)
        {
            var input = ConsoleRenderer.ReadUserInput();

            if (string.IsNullOrWhiteSpace(input))
                continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            var intent = await router.ClassifyAsync(input, ct);
            var agent = SelectAgent(intent);

            ConsoleRenderer.RenderRoute(agent.Name);

            var reply = await agent.HandleAsync(input, ct);
            ConsoleRenderer.RenderReply(agent.Name, reply);
        }
    }

    private SpecialistAgent SelectAgent(AgentIntent intent) => intent switch
    {
        AgentIntent.PositionSearch => positionSearchAgent,
        AgentIntent.JobDescription => jobDescriptionAgent,
        AgentIntent.OrgSummary => orgSummaryAgent,
        AgentIntent.Compliance => complianceAgent,
        _ => generalAgent,
    };
}
