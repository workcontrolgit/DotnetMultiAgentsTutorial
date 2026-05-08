// src/Hr.Compliance.Mcp/Program.cs
using Hr.Compliance.Mcp.Rules;
using Hr.Compliance.Mcp.Tools;
using Hr.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "logs", "compliance-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // ── Persistence (read-only usage — fetch position data for rule checks) ──
    builder.Services.AddPersistence(
        builder.Configuration.GetConnectionString("DefaultConnection")!);

    // ── OPM Rule Engine (singleton — rule definitions are immutable) ─────────
    builder.Services.AddSingleton<OpmStandardsRepository>();
    builder.Services.AddSingleton<OpmRuleEngine>();

    // ── OIDC (same feature-flag pattern as Hr.Jobs.Mcp) ─────────────────
    var enableOidc = builder.Configuration.GetValue<bool>("Features:EnableOidc");
    if (enableOidc)
    {
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = builder.Configuration["Oidc:Authority"];
                options.Audience  = builder.Configuration["Oidc:Audience"];
                if (builder.Environment.IsDevelopment())
                    options.BackchannelHttpHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    };
            });
        builder.Services.AddAuthorization();
    }

    // ── MCP Server ───────────────────────────────────────────────────────────
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

    var route = app.MapMcp("/compliance");
    if (enableOidc)
        route.RequireAuthorization();

    await app.RunAsync();
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
