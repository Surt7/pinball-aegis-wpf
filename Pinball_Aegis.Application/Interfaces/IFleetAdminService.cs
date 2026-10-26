using Pinball_Aegis.Domain.Entities;

namespace Pinball_Aegis.Application.Interfaces;

public sealed record ReissueTokenResult(bool Success, string? EnrollmentToken, DateTime? ExpiresAtUtc, string? ErrorMessage);

public sealed record IpLicenseItem(Guid IpId, string Name, string Slug, string? PublisherName);

public sealed record ProvisionMachineParams(
    string SerialNumber,
    string DisplayName,
    string IpSlug,
    string? Location = null,
    string? Notes = null
);

public sealed record ProvisionMachineResult(
    bool Success, 
    Guid? MachineId, 
    string? EnrollmentToken, 
    string? ErrorMessage
);

public sealed record CreateOperatorTokenParams(
    string Name,
    string Scopes,
    int ValidityDays
);

public sealed record CreateOperatorTokenResult(
    bool Success,
    Guid? TokenId,
    string? Name,
    string? RawToken,
    DateTime? ExpiresAtUtc,
    string? ErrorMessage
);

// Contract for administrative fleet supervision, provisioning, and token lifecycle
public interface IFleetAdminService
{
    // Retrieves the real registered flipper fleet from the backend API
    Task<IReadOnlyList<MachineItem>> GetAllMachinesAsync(CancellationToken ct = default);

    // Reissues an enrollment token for a flipper machine in PendingEnrollment status
    Task<ReissueTokenResult> ReissueEnrollmentTokenAsync(Guid machineId, CancellationToken ct = default);

    // Queries the real X.509 hardware certificate and TPM status from the backend
    Task<MachineSecurityDto?> GetMachineSecurityAsync(Guid machineId, CancellationToken ct = default);

    // Retrieves all available IP licenses for machine provisioning
    Task<IReadOnlyList<IpLicenseItem>> GetIpLicensesAsync(CancellationToken ct = default);

    // Provisions a new flipper machine in the fleet and returns its initial enrollment token
    Task<ProvisionMachineResult> ProvisionMachineAsync(ProvisionMachineParams parameters, CancellationToken ct = default);

    // Creates an operator token (e.g. for USB keys or field technician authorization)
    Task<CreateOperatorTokenResult> CreateOperatorTokenAsync(CreateOperatorTokenParams parameters, CancellationToken ct = default);
}
