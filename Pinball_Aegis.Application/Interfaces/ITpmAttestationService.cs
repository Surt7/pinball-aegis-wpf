using Pinball_Aegis.Domain.Entities;

namespace Pinball_Aegis.Application.Interfaces;

// Contract for validating machine hardware identity and TPM 2.0 attestation
public interface ITpmAttestationService
{
    // Verifies machine identity using public key and TPM endorsement certificate
    Task<bool> VerifyAttestationAsync(string machineId, CancellationToken cancellationToken = default);
    
    // Retrieves current attested machine details
    Task<PinballMachine?> GetMachineDetailsAsync(string machineId, CancellationToken cancellationToken = default);
}