using Microsoft.Extensions.AI;

namespace Hr.ConsoleShared.Startup;

public static class StartupBannerWriter
{
    public static void Write(
        string appName,
        string transport,
        string provider,
        string model,
        int? numCtx,
        IReadOnlyList<string> serverNames,
        IReadOnlyList<(string Server, AITool Tool)> toolEntries)
    {
        const int width = 43;

        static string Pad(string value, int width) => value.Length >= width ? value[..width] : value.PadRight(width);
        static string Line(string value, int width) => $"|  {Pad(value, width)}|";
        static string Item(string value, int width) => $"|    - {Pad(value, width - 4)}|";

        Console.WriteLine($"+{new string('-', width + 2)}+");
        Console.WriteLine(Line(appName, width));
        Console.WriteLine(Line($"Transport : {transport}", width));
        Console.WriteLine(Line($"Provider  : {provider}", width));
        Console.WriteLine(Line($"Model     : {model}", width));
        if (numCtx.HasValue)
            Console.WriteLine(Line($"NumCtx    : {numCtx.Value:N0}", width));

        Console.WriteLine(Line($"Servers ({serverNames.Count}) :", width));
        foreach (var serverName in serverNames.OrderBy(name => name, StringComparer.Ordinal))
            Console.WriteLine(Item(serverName, width));

        Console.WriteLine(Line($"Tools ({toolEntries.Count})  :", width));
        foreach (var entry in toolEntries.OrderBy(t => t.Server, StringComparer.Ordinal).ThenBy(t => t.Tool.Name, StringComparer.Ordinal))
            Console.WriteLine(Item($"{entry.Server}: {entry.Tool.Name}", width));

        Console.WriteLine(Line("Status    : READY", width));
        Console.WriteLine($"+{new string('-', width + 2)}+");
        Console.WriteLine();
    }
}
