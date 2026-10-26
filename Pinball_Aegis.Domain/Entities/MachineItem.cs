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

    public string CertExpirationColor => CertStatus switch
    {
        2 => "#EF4444", // Revoked (Red)
        _ when !CertExpiresAtUtc.HasValue => "#64748B", // No certificate (Slate / Gray)
        _ when IsCertExpired => "#EF4444", // Expired (Red)
        _ when IsCertExpiringSoon => "#F59E0B", // Expiring soon <= 30 days (Amber)
        _ => "#10B981" // Valid (Emerald Green)
    };

    public string CertStatusText => CertStatus switch
    {
        2 => "Certificat matériel TPM révoqué",
        _ when !CertExpiresAtUtc.HasValue => "Puce TPM non enrôlée / Aucun certificat",
        _ when IsCertExpired => $"Certificat expiré le {CertExpiresAtUtc:dd/MM/yyyy}",
        _ when IsCertExpiringSoon => $"Expire bientôt le {CertExpiresAtUtc:dd/MM/yyyy} ({Math.Max(0, (int)(CertExpiresAtUtc.Value - DateTime.UtcNow).TotalDays)}j restants)",
        _ => $"Certificat TPM valide jusqu'au {CertExpiresAtUtc:dd/MM/yyyy}"
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
