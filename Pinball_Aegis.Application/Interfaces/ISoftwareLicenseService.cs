using Pinball_Aegis.Domain.Entities;

namespace Pinball_Aegis.Application.Interfaces;

// Contract for verifying and inspecting the cryptographic software license
public interface ISoftwareLicenseService
{
    // True if a license file was loaded and its signature verified
    bool HasLicense { get; }

    // True if the license is valid, not expired, and bound to the current workstation
    bool IsValid { get; }

    // The active verified license entity (null if invalid or missing)
    SoftwareLicense? ActiveLicense { get; }

    // Human-readable status description (e.g. "Licence Active", "Expirée", "Poste non concordant")
    string StatusDescription { get; }

    // Detailed diagnostic or validation message
    string ValidationDetails { get; }

    // Re-evaluates license validity against the current workstation identity
    void RefreshLicense(string? workstationCommonName);
}
