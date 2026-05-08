// src/Hr.Core/Enums/SecurityClearance.cs
// USAJobs API: securityClearance
namespace Hr.Core.Enums;

public enum SecurityClearance
{
    NotRequired,
    PublicTrust,
    Confidential,
    Secret,
    TopSecret,
    TopSecretSCI    // TS/SCI
}
