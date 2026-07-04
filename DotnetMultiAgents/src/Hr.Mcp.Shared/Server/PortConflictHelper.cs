using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Configuration;

namespace Hr.Mcp.Shared.Server;

public static class PortConflictHelper
{
    public static bool TryDescribePortConflict(IOException ex, string defaultUrl, out string message)
    {
        if (!Uri.TryCreate(defaultUrl, UriKind.Absolute, out var uri))
        {
            message = "The default MCP URL is invalid.";
            return true;
        }

        return TryDescribePortConflict(ex, uri, out message);
    }

    public static bool TryDescribePortConflict(IOException ex, IConfiguration configuration, string configKey, string fallbackUrl, out string message)
    {
        var configuredUrl = configuration[configKey] ?? configuration["Urls"] ?? fallbackUrl;
        if (!Uri.TryCreate(configuredUrl.Split(';', StringSplitOptions.RemoveEmptyEntries)[0], UriKind.Absolute, out var uri))
        {
            message = $"The MCP URL is invalid. Check {configKey} or Urls.";
            return true;
        }

        return TryDescribePortConflict(ex, uri, out message);
    }

    private static bool TryDescribePortConflict(IOException ex, Uri uri, out string message)
    {
        if (!ex.Message.Contains(uri.ToString(), StringComparison.OrdinalIgnoreCase) &&
            !ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
        {
            message = string.Empty;
            return false;
        }

        var conflict = IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .FirstOrDefault(endpoint =>
                endpoint.Port == uri.Port &&
                (IPAddress.IsLoopback(endpoint.Address) || endpoint.Address.Equals(IPAddress.Any) || endpoint.Address.Equals(IPAddress.IPv6Any)));

        if (conflict is null)
        {
            message = $"Port {uri.Port} is already in use. Stop the existing listener or change the configured MCP server URL.";
            return true;
        }

        message = $"Port {uri.Port} is already in use. Stop the running server or change the configured URL before starting another instance.";
        return true;
    }
}
