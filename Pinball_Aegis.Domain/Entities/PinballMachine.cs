namespace Pinball_Aegis.Domain.Entities;

public class PinballMachine
{
    public PinballMachine(string machineId, string modelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        MachineId = machineId;
        ModelName = modelName;
        IsTpmAttested = false; // Not attested by default 
    }
    
    
    // Unique hardware identifier (e.g. "NUC-FLIPPER-042")
    public string MachineId { get; }
    // Table model name (e.g. "Medieval Madness")
    public string ModelName { get; }
    // Whether hardware TPM 2.0 attestation is cryptographically valid
    public bool IsTpmAttested { get; private set; }
    // SHA-256 fingerprint of the machine's active X.509 certificate
    public string? CertificateThumbprint { get; private set; }


    // Cryptographically attest the machine using verified certificate
    public void AttestWithTpm(string certificateThumbprint)
    {
        ArgumentNullException.ThrowIfNull(certificateThumbprint);
        
        CertificateThumbprint = certificateThumbprint;
        IsTpmAttested = true;

        // Invalidate security attestation (revocation, expired, tampered)
    
    }
    
    public void InvalidateAttestation()
    {
        IsTpmAttested = false;
    }
}