// src/HrMcp.Core/Enums/SecurityClearance.cs
// USAJobs API: securityClearance
namespace HrMcp.Core.Enums;

public enum SecurityClearance
{
    NotRequired,
    PublicTrust,
    Confidential,
    Secret,
    TopSecret,
    TopSecretSCI    // TS/SCI
}
