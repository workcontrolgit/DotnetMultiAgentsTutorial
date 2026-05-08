// src/HrMcp.Agent/HrAgent.cs
using Microsoft.Extensions.AI;

namespace HrMcp.Agent;

public sealed class HrAgent(IChatClient chatClient, IList<AITool> tools)
{
    private const string SystemPrompt = """
        You are an HR assistant for a U.S. federal agency. You help users explore open job
        positions, hiring organizations, and generate job announcements.

        Guidelines:
        - Always call GetHiringOrganizations before GetPositionsByOrganization to get valid IDs.
        - When asked about open positions, use GetOpenPositions first for an overview, then
          GetPositionById for full detail on a specific role.
        - When asked to write or generate a job description, call WriteJobDescription with the
          position ID — do not write one yourself.
        - Present pay ranges in a readable format (e.g., "$85,000 – $110,000 per year").
        - Keep answers concise; offer to go deeper when the user wants more detail.
        """;

    private readonly List<ChatMessage> _history =
    [
        new(ChatRole.System, SystemPrompt)
    ];

    public async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("HR Assistant ready. Ask about open positions, organizations, or job descriptions.");
        Console.WriteLine("Type 'exit' to quit.\n");

        while (!ct.IsCancellationRequested)
        {
            Console.Write("You: ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            _history.Add(new ChatMessage(ChatRole.User, input));

            var response = await chatClient.GetResponseAsync(
                _history,
                new ChatOptions { Tools = tools },
                ct);

            _history.AddMessages(response);

            Console.WriteLine($"\nAssistant: {response.Text}\n");
        }
    }
}
