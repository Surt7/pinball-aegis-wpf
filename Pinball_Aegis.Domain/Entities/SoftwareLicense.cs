namespace Pinball_Aegis.Domain.Entities;

// Represents an authentic cryptographic software license bound to a workstation
public sealed class SoftwareLicense
{
    public string LicenseId { get; }
    public string Product { get; }
    public string Edition { get; }
    public string LicensedTo { get; }
    public string HardwareBinding { get; }
    public DateTimeOffset IssuedAtUtc { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public IReadOnlyList<string> Features { get; }

    public SoftwareLicense(
        string licenseId,
        string product,
        string edition,
        string licensedTo,
        string hardwareBinding,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc,
        IEnumerable<string> features)
    {
        LicenseId = licenseId;
        Product = product;
        Edition = edition;
        LicensedTo = licensedTo;
        HardwareBinding = hardwareBinding;
        IssuedAtUtc = issuedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Features = features.ToList();
    }

    public bool IsExpired(DateTimeOffset utcNow) => utcNow >= ExpiresAtUtc;

    public bool IsHardwareBoundTo(string? workstationCommonName)
    {
        if (string.IsNullOrWhiteSpace(HardwareBinding) || HardwareBinding == "*")
            return true;

        return string.Equals(HardwareBinding, workstationCommonName, StringComparison.OrdinalIgnoreCase);
    }
}
