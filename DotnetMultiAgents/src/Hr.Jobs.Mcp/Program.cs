// src/Hr.Jobs.Mcp/Program.cs
using Hr.Application.Services;
using Hr.Infrastructure;
using Hr.Jobs.Mcp.Tools;
using Hr.Mcp.Shared.Server;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OllamaSharp;
using Serilog;

var isStdio = args.Contains("--stdio");

// Bootstrap Serilog from appsettings (file sinks).
// Console sink is added conditionally: suppressed in stdio mode so stdout
// carries only JSON-RPC messages.
var tempConfig = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile(
        $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json",
        optional: true)
    .AddEnvironmentVariables()
    .Build();

// Resolve log path relative to the app binary so it is consistent
// regardless of which directory dotnet run is invoked from.
// Anchor log paths to the app binary directory so location is consistent
// regardless of which directory dotnet run is invoked from.
//   logs/info/info-YYYYMMDD.log  — Information and above
//   logs/error/error-YYYYMMDD.log — Error and Fatal only
var logBase = Path.Combine(AppContext.BaseDirectory, "logs");

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(tempConfig)
    .WriteTo.Conditional(_ => !isStdio, wt => wt.Console())
    .WriteTo.File(
        Path.Combine(logBase, "info", "info-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        Path.Combine(logBase, "error", "error-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    if (isStdio)
    {
        var hostBuilder = Host.CreateApplicationBuilder(args);
        hostBuilder.Services.AddSerilog();

        ConfigureCommonServices(hostBuilder.Services, hostBuilder.Configuration);

        hostBuilder.Services
            .AddMcpServer()
            .WithTools<PositionTools>()
            .WithTools<HiringOrganizationTools>()
            .WithTools<ExportTools>()
            .WithTools<JobDescriptionTools>()
            .WithTools<JobAnnouncementTools>()
            .WithStdioServerTransport();

        using var host = hostBuilder.Build();
        await InitializeDatabaseAsync(host.Services);
        await host.RunAsync();
        return;
    }

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    ConfigureCommonServices(builder.Services, builder.Configuration);

    // ── OIDC feature flag ────────────────────────────────────────────────────
    // Set Features:EnableOidc = false in appsettings.Development.json to run
    // without an identity provider (e.g. when testing with MCP Inspector).
    // Set to true in production to enforce JWT Bearer authentication.
    var enableOidc = builder.Configuration.GetValue<bool>("Features:EnableOidc");

    if (enableOidc)
    {
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = builder.Configuration["Oidc:Authority"];
                options.Audience  = builder.Configuration["Oidc:Audience"];
                // Trust self-signed certs when running against a local dev IdentityServer container
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
        .WithTools<PositionTools>()
        .WithTools<HiringOrganizationTools>()
        .WithTools<ExportTools>()
        .WithTools<JobDescriptionTools>()
        .WithTools<JobAnnouncementTools>()
        .WithHttpTransport();

    var app = builder.Build();
    await InitializeDatabaseAsync(app.Services);

    if (enableOidc)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    if (!isStdio)
    {
        var route = app.MapMcp("/mcp");
        if (enableOidc)
            route.RequireAuthorization();
    }

    await app.RunAsync();
}
catch (IOException ex) when (!isStdio && PortConflictHelper.TryDescribePortConflict(ex, "http://127.0.0.1:5100", out var conflictMessage))
{
    Log.Fatal(ex, "{ConflictMessage}", conflictMessage);
    Console.Error.WriteLine(conflictMessage);
    Environment.ExitCode = 1;
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
    services.AddScoped<HiringOrganizationService>();
    services.AddScoped<JobAnnouncementService>();

    // IChatClient used by WriteJobDescription tool to generate LLM narratives
    services.AddSingleton<IChatClient>(
        new OllamaApiClient(
            new Uri(configuration["AI:Ollama:Endpoint"] ?? "http://localhost:11434"),
            configuration["AI:Ollama:Model"] ?? "gemma4:latest"));
}

static async Task InitializeDatabaseAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();

    await db.Database.MigrateAsync();

    // Looks for data/usajobs-seed.json in the working directory (solution root when using dotnet run)
    var seedPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "usajobs-seed.json");
    DbSeeder.Seed(db, seedPath);
}

