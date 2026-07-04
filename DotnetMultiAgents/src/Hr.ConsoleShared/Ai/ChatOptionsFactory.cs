using Microsoft.Extensions.AI;

namespace Hr.ConsoleShared.Ai;

public static class ChatOptionsFactory
{
    public static ChatOptions Create(int? numCtx) => Create([], numCtx);

    public static ChatOptions Create(IReadOnlyList<AITool> toolList, int? numCtx)
    {
        var options = new ChatOptions();
        if (toolList.Count > 0)
            options.Tools = [.. toolList];

        if (numCtx.HasValue)
        {
            options.AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["num_ctx"] = numCtx.Value
            };
        }

        return options;
    }
}
