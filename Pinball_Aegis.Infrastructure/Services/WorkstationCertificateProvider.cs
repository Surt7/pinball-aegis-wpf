using System.Security.Cryptography.X509Certificates;
using Pinball_Aegis.Application.Interfaces;

namespace Pinball_Aegis.Infrastructure.Services;

// Hardened Zero-Trust provider: strictly loads the workstation mTLS certificate from the native Windows Certificate Store.
// File-based fallbacks (.crt/.key) are explicitly forbidden to prevent private keys from lingering on disk.
public class WorkstationCertificateProvider : IWorkstationCertificateProvider
{
    private readonly X509Certificate2? _certificate;

    public bool HasCertificate => _certificate is not null;
    public bool IsValid => _certificate is not null && 
                           DateTime.UtcNow >= _certificate.NotBefore.ToUniversalTime() && 
                           DateTime.UtcNow <= _certificate.NotAfter.ToUniversalTime() && 
                           _certificate.HasPrivateKey;

    public X509Certificate2? GetCertificate() => _certificate;
    public string? CommonName { get; }
    public string? Issuer { get; }
    public DateTime? ExpiresAt => _certificate?.NotAfter;
    public string? StorageLocation { get; }
    public string? CertificateThumbprint => _certificate?.Thumbprint;

    public WorkstationCertificateProvider()
    {
        var (cert, location) = ResolveStoreCertificate();
        _certificate = cert;
        StorageLocation = location;

        if (_certificate is not null)
        {
            CommonName = _certificate.GetNameInfo(X509NameType.SimpleName, false);
            Issuer = _certificate.GetNameInfo(X509NameType.SimpleName, true);
        }
    }

    private static (X509Certificate2? cert, string? location) ResolveStoreCertificate()
    {
        // 1. Primary check: Windows LocalMachine Certificate Store (Machine-level / TPM KSP hardware)
        try
        {
            using var localStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            localStore.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            foreach (var cert in localStore.Certificates)
            {
                if (IsMatchingWorkstationCertificate(cert))
                {
                    return (cert, "Magasin Windows (LocalMachine TPM)");
                }
            }
        }
        catch { }

        // 2. Secondary check: Windows CurrentUser Certificate Store
        try
        {
            using var userStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            userStore.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            foreach (var cert in userStore.Certificates)
            {
                if (IsMatchingWorkstationCertificate(cert))
                {
                    return (cert, "Magasin Windows (CurrentUser)");
                }
            }
        }
        catch { }

        // Strict security posture: No file system fallback (.crt/.key on disk is forbidden)
        return (null, null);
    }

    private static bool IsMatchingWorkstationCertificate(X509Certificate2 cert)
    {
        return cert.HasPrivateKey && 
               (cert.Subject.Contains("OU=Workstations", StringComparison.OrdinalIgnoreCase) ||
                cert.Issuer.Contains("WorldPinball Device Intermediate CA", StringComparison.OrdinalIgnoreCase) ||
                cert.Issuer.Contains("WorldPinball", StringComparison.OrdinalIgnoreCase));
    }
}
