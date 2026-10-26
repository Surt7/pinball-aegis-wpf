using System.Security.Cryptography.X509Certificates;

namespace Pinball_Aegis.Application.Interfaces;

// Provides the hardware-bound mTLS identity certificate of this operator workstation
public interface IWorkstationCertificateProvider
{
    // True if an mTLS workstation certificate was found and loaded
    bool HasCertificate { get; }

    // True if the loaded certificate is currently within its validity period and has an active private key
    bool IsValid { get; }

    // Gets the active X.509 workstation certificate with its private key
    X509Certificate2? GetCertificate();

    // Workstation identity Common Name (e.g. "poste-admin-02")
    string? CommonName { get; }

    // Certificate issuer Common Name (e.g. "WorldPinball Device Intermediate CA")
    string? Issuer { get; }

    // Expiration date of the workstation certificate
    DateTime? ExpiresAt { get; }

    // Storage mechanism: "Magasin Windows (LocalMachine)" or "Fichier matériel local"
    string? StorageLocation { get; }

    // SHA-1 thumbprint of the active certificate
    string? CertificateThumbprint { get; }
}
