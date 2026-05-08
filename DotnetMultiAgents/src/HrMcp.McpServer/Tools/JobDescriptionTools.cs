// src/HrMcp.McpServer/Tools/JobDescriptionTools.cs
using System.ComponentModel;
using HrMcp.Application.Services;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace HrMcp.McpServer.Tools;

[McpServerToolType]
public sealed class JobDescriptionTools(PositionService positions, IChatClient chatClient)
{
    [McpServerTool(Name = "WriteJobDescription"),
     Description("Generates a USAJobs-style job announcement for the specified position using AI. Returns a fully written narrative with Summary, Duties, Qualifications, and How to Apply sections.")]
    public async Task<string> WriteJobDescription(
        [Description("The numeric ID of the position to write a description for")] int positionId,
        CancellationToken ct = default)
    {
        var p = await positions.GetPositionByIdAsync(positionId, ct);
        if (p is null) return $"Position {positionId} not found.";

        var prompt = $"""
            Write a compelling USAJobs-style job announcement for the following federal position.
            Use professional government HR writing style. Be specific and engaging.

            Position Data:
            - Title: {p.Title}
            - Department: {p.HiringOrganization?.DepartmentName}
            - Organization: {p.HiringOrganization?.OrganizationName}
            - Series & Grade: {p.OccupationalSeries} | {p.PayGradeMin}–{p.PayGradeMax}
            - Salary: ${p.PositionRemuneration?.MinimumRange:N0} – ${p.PositionRemuneration?.MaximumRange:N0} per year
            - Location: {p.DutyLocation}
            - Telework: {(p.TeleworkEligible ? "Eligible" : "Not eligible")}
            - Security Clearance: {p.SecurityClearance}
            - Who May Apply: {p.WhoMayApply}
            - Description: {p.Description}
            - Duties: {p.Duties}
            - Qualifications: {p.Qualifications}

            Format the output as a complete job announcement with these sections:
            ## Summary
            ## Duties
            ## Qualifications Required
            ## How to Apply
            """;

        var response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, prompt)],
            cancellationToken: ct);
        return response.Text ?? $"Unable to generate description for position {positionId}.";
    }
}
