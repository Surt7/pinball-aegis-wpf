namespace Pinball_Aegis.Domain.Entities;

// Represents a flipper machine in the operator fleet inventory
public sealed class MachineItem
{
    public Guid MachineId { get; set; }
    public string MachineName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public int MachineStatus { get; set; }
    public string? MachineNotes { get; set; }
    public DateTime RegisteredAtUtc { get; set; }
    public DateTime? LastUpdated { get; set; }

    // Real X.509 TPM Certificate metadata (populated in 1 single pass from GetAllMachines)
    public DateTime? CertExpiresAtUtc { get; set; }
    public int? CertStatus { get; set; }

    public bool HasCertificate => CertExpiresAtUtc.HasValue;
    public bool IsCertExpired => CertExpiresAtUtc.HasValue && DateTime.UtcNow > CertExpiresAtUtc.Value;
    public bool IsCertExpiringSoon => CertExpiresAtUtc.HasValue && !IsCertExpired && CertExpiresAtUtc.Value <= DateTime.UtcNow.AddDays(30);
    // Only machines in PendingEnrollment status (5) are eligible for enrollment tokens
    public bool CanIssueToken => MachineStatus == 5;

    // Adaptive button text based on whether a certificate was already provisioned
    public string ReissueButtonText => !HasCertificate 
        ? "Générer Jeton d'Enrôlement" 
        : "Réémettre Jeton d'Enrôlement";
    
    
    public string CertExpirationColor => CertStatus switch
    {
        _ when !CertExpiresAtUtc.HasValue => "#64748B", // No certificate (Slate / Gray)
        2 => "#EF4444", // Revoked (Red)
        3 => "#64748B", // Superseded / Replaced (Slate / Gray)
        _ when IsCertExpired => "#EF4444", // Expired (Red)
        0 when IsCertExpiringSoon => "#F59E0B", // Expiring soon <= 30 days (Amber)
        0 => "#10B981", // Valid (Emerald Green)
        1 when !IsCertExpired => "#F59E0B", // Anomaly: Status is 1 (Inactive in DB) but expiration is in the future (Amber)
        _ => "#64748B" // Inactive / Other (Slate / Gray)
    };

    public string CertStatusText => CertStatus switch
    {
        _ when !CertExpiresAtUtc.HasValue => "Puce TPM non enrôlée / Aucun certificat",
        2 => "Certificat matériel TPM révoqué",
        3 => "Certificat matériel remplacé (obsolète)",
        _ when IsCertExpired => $"Certificat expiré le {CertExpiresAtUtc:dd/MM/yyyy}",
        0 when IsCertExpiringSoon => $"Expire bientôt le {CertExpiresAtUtc:dd/MM/yyyy} ({Math.Max(0, (int)(CertExpiresAtUtc.Value - DateTime.UtcNow).TotalDays)}j restants)",
        0 => $"Certificat TPM valide jusqu'au {CertExpiresAtUtc:dd/MM/yyyy}",
        1 when !IsCertExpired => $"⚠️ Statut inactif en BDD (expiration prévue le {CertExpiresAtUtc:dd/MM/yyyy})",
        _ => "Certificat non actif"
    };

    public string StatusText => MachineStatus switch
    {
        1 => "Active",
        2 => "Révoquée",
        3 => "Inactive",
        4 => "Maintenance",
        5 => "En attente d'enrôlement",
        _ => "Inconnu"
    };

    public string StatusColor => MachineStatus switch
    {
        1 => "#51CF66", // Green
        2 => "#FF6B6B", // Red
        3 => "#868E96", // Gray
        4 => "#FCC419", // Yellow
        5 => "#FF922B", // Orange
        _ => "#868E96"
    };

    public bool IsEnrolled => MachineStatus == 1;
}
