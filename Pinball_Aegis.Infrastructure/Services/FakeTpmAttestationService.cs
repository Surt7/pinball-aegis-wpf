using Pinball_Aegis.Application.Interfaces;
using Pinball_Aegis.Domain.Entities;

namespace Pinball_Aegis.Infrastructure.Services;

// In-memory simulated TPM 2.0 attestation service for local testing and UI prototyping
public class FakeTpmAttestationService : ITpmAttestationService
{
    public Task<bool> VerifyAttestationAsync(string machineId, CancellationToken cancellationToken = default)
    {
        // Simulated hardware check: only machines with valid NUC prefix pass attestation
        bool isValid = !string.IsNullOrWhiteSpace(machineId) && 
                       machineId.StartsWith("NUC-", StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(isValid);
    }

    public Task<PinballMachine?> GetMachineDetailsAsync(string machineId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(machineId))
            return Task.FromResult<PinballMachine?>(null);

        var machine = new PinballMachine(machineId, "Medieval Madness Remake");
        
        // Simulate successful attestation with a dummy SHA-256 certificate thumbprint
        machine.AttestWithTpm("8F4A2B9C0E1D3F5A7B8C9D0E1F2A3B4C5D6E7F8A");

        return Task.FromResult<PinballMachine?>(machine);
    }
}