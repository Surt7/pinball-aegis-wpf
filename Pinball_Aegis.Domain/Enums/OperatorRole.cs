namespace Pinball_Aegis.Domain.Enums;

// Defines high-level operator profile roles
public enum OperatorRole
{
    // Full system administration and PKI management
    SuperAdmin,

    // On-site maintenance, hardware diagnostics, and firmware flashing
    FieldTechnician,

    // Arcade venue manager: tournaments, pricing, and game settings
    ArcadeOperator,

    // Read-only compliance auditor for secure cryptographic logs
    Auditor
}