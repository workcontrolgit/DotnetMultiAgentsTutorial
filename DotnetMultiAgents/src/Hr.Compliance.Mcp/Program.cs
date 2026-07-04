using System.Net;
using System.Net.NetworkInformation;
using Hr.Application.Services;
using Hr.Compliance.Mcp.Rules;
using Hr.Compliance.Mcp.Tools;
using Hr.Infrastructure;
using Hr.Mcp.Shared.Server;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var explicitStdio = args.Contains("--stdio");
var explicitStreamHttp = args.Contains("--stream-http");

var tempConfig = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile(
        $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json",
        optional: true)
    .AddEnvironmentVariables()
    .Build();

var configuredTransport = tempConfig["McpServer:Transport:Type"] ?? "streamHttp";
var useStdio = explicitStdio || (!explicitStreamHttp &&
    string.Equals(configuredTransport, "stdio", StringComparison.OrdinalIgnoreCase));

var enableDebug = args.Contains("--debug") ||
    tempConfig.GetValue<bool>("Features:EnableDebug");

var logConfig = new LoggerConfiguration().ReadFrom.Configuration(tempConfig);
if (enableDebug)
    logConfig = logConfig.MinimumLevel.Debug();

Log.Logger = logConfig
    .WriteTo.Conditional(_ => !useStdio, wt => wt.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
    .WriteTo.Conditional(_ => useStdio, wt => wt.Console(
        standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose,
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
    .CreateLogger();

try
{
    if (useStdio)
    {
        var hostBuilder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });
        hostBuilder.Services.AddSerilog();

        ConfigureCommonServices(hostBuilder.Services, hostBuilder.Configuration);

        hostBuilder.Services
            .AddMcpServer()
            .WithTools<ComplianceTools>()
            .WithStdioServerTransport();

        using var host = hostBuilder.Build();
        await host.RunAsync();
        return;
    }

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    });
    builder.Host.UseSerilog();

    ConfigureCommonServices(builder.Services, builder.Configuration);

    var enableOidc = builder.Configuration.GetValue<bool>("Features:EnableOidc");
    if (enableOidc)
    {
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = builder.Configuration["Oidc:Authority"];
                options.Audience = builder.Configuration["Oidc:Audience"];
                if (builder.Environment.IsDevelopment())
                    options.BackchannelHttpHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    };
            });
        builder.Services.AddAuthorization();
    }

    builder.Services
        .AddMcpServer()
        .WithTools<ComplianceTools>()
        .WithHttpTransport();

    var app = builder.Build();

    if (enableOidc)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    var mcpPath = builder.Configuration["McpServer:Transport:StreamHttp:Path"] ?? "/compliance";
    var route = app.MapMcp(mcpPath);
    if (enableOidc)
        route.RequireAuthorization();

    await app.RunAsync();
}
catch (IOException ex) when (!useStdio && PortConflictHelper.TryDescribePortConflict(ex, tempConfig, "McpServer:Transport:StreamHttp:Url", "http://localhost:5200", out var conflictMessage))
{
    Log.Fatal(ex, "{ConflictMessage}", conflictMessage);
    Console.Error.WriteLine(conflictMessage);
    Environment.ExitCode = 1;
}
catch (Exception ex)
{
    Log.Fatal(ex, "ComplianceMcp terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static void ConfigureCommonServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddPersistence(
        configuration.GetConnectionString("DefaultConnection")!);
    services.AddScoped<PositionService>();
    services.AddSingleton<OpmStandardsRepository>();
    services.AddSingleton<OpmRuleEngine>();
}

