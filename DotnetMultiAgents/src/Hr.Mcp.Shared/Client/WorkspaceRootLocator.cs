namespace Hr.Mcp.Shared.Client;

public static class WorkspaceRootLocator
{
    public static string FindWorkspaceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "DotnetMultiAgents")))
                return dir.FullName;
        }

        return AppContext.BaseDirectory;
    }
}
