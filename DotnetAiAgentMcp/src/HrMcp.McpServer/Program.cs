// src/HrMcp.McpServer/Program.cs
using HrMcp.Application.Services;
using HrMcp.Infrastructure.Persistence;
using HrMcp.McpServer.Tools;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
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
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    if (isStdio)
        builder.WebHost.UseUrls(); // no HTTP listener in stdio mode

    builder.Services.AddPersistence(
        builder.Configuration.GetConnectionString("DefaultConnection")!);
    builder.Services.AddScoped<PositionService>();
    builder.Services.AddScoped<HiringOrganizationService>();

    // IChatClient used by WriteJobDescription tool to generate LLM narratives
    builder.Services.AddSingleton<IChatClient>(
        new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.2"));

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

    var mcp = builder.Services
        .AddMcpServer()
        .WithTools<PositionTools>()
        .WithTools<HiringOrganizationTools>()
        .WithTools<JobDescriptionTools>();

    if (isStdio)
        mcp.WithStdioServerTransport();
    else
        mcp.WithHttpTransport();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        db.Database.Migrate();

        // Looks for data/usajobs-seed.json in the working directory (solution root when using dotnet run)
        var seedPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "usajobs-seed.json");
        DbSeeder.Seed(db, seedPath);
    }

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
finally
{
    await Log.CloseAndFlushAsync();
}
