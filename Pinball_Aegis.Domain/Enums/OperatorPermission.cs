namespace Pinball_Aegis.Domain.Enums;


// Granular security permissions required to execute sensitive actions
public enum OperatorPermission
{
    // Read-only views
    ViewFleet,
    ViewAuditLogs,
    // Security & Cryptography
    VerifyTpmAttestation,
    RevokeCertificate,
    // Hardware & Maintenance
    RunHardwareDiagnostics,
    ReflashFirmware,
    // Venue & Tournament Operations
    ConfigurePricing,
    ManageTournaments
}