using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;

namespace Hr.Mcp.Shared.Client;

public static class McpClientTransportFactory
{
    public static Task<IClientTransport> CreateAsync(
        IConfiguration configuration,
        McpServerDefinition server,
        IReadOnlyDictionary<string, string>? additionalHeaders = null)
    {
        if (string.Equals(server.TransportType, "stdio", StringComparison.OrdinalIgnoreCase))
        {
            var command = configuration[$"{server.ConfigPath}:Transport:Stdio:Command"] ?? "dotnet";
            var workingDirectory = configuration[$"{server.ConfigPath}:Transport:Stdio:WorkingDirectory"];
            if (string.IsNullOrWhiteSpace(workingDirectory))
                workingDirectory = WorkspaceRootLocator.FindWorkspaceRoot();

            var projectPath = configuration[$"{server.ConfigPath}:Transport:Stdio:ProjectPath"]
                ?? throw new InvalidOperationException($"Missing configuration: {server.ConfigPath}:Transport:Stdio:ProjectPath");

            IClientTransport transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = command,
                Arguments = ["run", "--project", projectPath, "--", "--stdio"],
                WorkingDirectory = workingDirectory,
                Name = $"{server.Name.ToLowerInvariant()}-mcp-stdio"
            });
            return Task.FromResult(transport);
        }

        var url = configuration[$"{server.ConfigPath}:Transport:StreamHttp:Url"]
            ?? configuration[$"{server.ConfigPath}:Url"]
            ?? throw new InvalidOperationException($"Missing configuration: {server.ConfigPath}:Transport:StreamHttp:Url");

        IClientTransport httpTransport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(url),
            AdditionalHeaders = additionalHeaders is null ? null : new Dictionary<string, string>(additionalHeaders),
            TransportMode = HttpTransportMode.StreamableHttp,
            Name = $"{server.Name.ToLowerInvariant()}-mcp-stream-http"
        });

        return Task.FromResult(httpTransport);
    }
}
