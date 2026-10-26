using System.Security.Cryptography.X509Certificates;

namespace Pinball_Aegis.Application.Interfaces;

// Provides the hardware-bound mTLS identity certificate of this operator workstation
public interface IWorkstationCertificateProvider
{
    // True if a valid mTLS workstation certificate was found and loaded
    bool HasCertificate { get; }

    // Gets the active X.509 workstation certificate with its private key
    X509Certificate2? GetCertificate();

    // SHA-256 thumbprint of the active certificate
    string? CertificateThumbprint { get; }
}