using Pinball_Aegis.Domain.Entities;

namespace Pinball_Aegis.Application.Interfaces;

public sealed record ReissueTokenResult(bool Success, string? EnrollmentToken, DateTime? ExpiresAtUtc, string? ErrorMessage);

// Contract for administrative fleet supervision and certificate/token lifecycle
public interface IFleetAdminService
{
    // Retrieves the real registered flipper fleet from the backend API
    Task<IReadOnlyList<MachineItem>> GetAllMachinesAsync(CancellationToken ct = default);

    // Reissues an enrollment token for a flipper machine in PendingEnrollment status
    Task<ReissueTokenResult> ReissueEnrollmentTokenAsync(Guid machineId, CancellationToken ct = default);
}
