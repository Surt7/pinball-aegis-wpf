using Pinball_Aegis.Domain.Enums;

namespace Pinball_Aegis.Domain.Entities;

// Represents an authenticated operator cryptographic session token
public sealed class OperatorToken
{
    public string OperatorId { get; }
    public string CommonName { get; }
    public OperatorRole Role { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public string Issuer { get; }

    public OperatorToken(
        string operatorId, 
        string commonName, 
        OperatorRole role, 
        DateTimeOffset expiresAtUtc,
        string issuer = "WorldPinball Operational Intermediate CA")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(commonName);

        OperatorId = operatorId;
        CommonName = commonName;
        Role = role;
        ExpiresAtUtc = expiresAtUtc;
        Issuer = issuer;
    }

    // Checks whether the token has expired
    public bool IsExpired(DateTimeOffset utcNow) => utcNow >= ExpiresAtUtc;

    // Checks if the operator role grants the requested permission
    public bool HasPermission(OperatorPermission permission)
    {
        return Role switch
        {
            // SuperAdmin has unrestricted access
            OperatorRole.SuperAdmin => true,

            // Field Technician: diagnostics, maintenance, and firmware
            OperatorRole.FieldTechnician => permission is 
                OperatorPermission.ViewFleet or
                OperatorPermission.ViewAuditLogs or
                OperatorPermission.VerifyTpmAttestation or
                OperatorPermission.RunHardwareDiagnostics or
                OperatorPermission.ReflashFirmware,

            // Arcade Operator: pricing, venue tournaments, fleet view
            OperatorRole.ArcadeOperator => permission is 
                OperatorPermission.ViewFleet or
                OperatorPermission.ConfigurePricing or
                OperatorPermission.ManageTournaments,

            // Auditor: read-only access to fleet and security logs
            OperatorRole.Auditor => permission is 
                OperatorPermission.ViewFleet or
                OperatorPermission.ViewAuditLogs,

            _ => false
        };
    }
}