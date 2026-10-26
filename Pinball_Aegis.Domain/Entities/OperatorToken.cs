using Pinball_Aegis.Domain.Enums;

namespace Pinball_Aegis.Domain.Entities;

// Represents an authenticated operator cryptographic session token
public sealed class OperatorToken
{
    public string OperatorId { get; }
    public string CommonName { get; }
    public IReadOnlyList<string> Roles { get; }
    public string PrimaryRole => Roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase)) 
        ? "Admin" 
        : (Roles.FirstOrDefault(r => !r.StartsWith("default-roles", StringComparison.OrdinalIgnoreCase)) ?? "User");
    public DateTimeOffset ExpiresAtUtc { get; }
    public string Issuer { get; }

    public OperatorToken(
        string operatorId, 
        string commonName, 
        IEnumerable<string> roles, 
        DateTimeOffset expiresAtUtc,
        string issuer = "WorldPinball")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(commonName);

        OperatorId = operatorId;
        CommonName = commonName;
        Roles = roles.ToList();
        ExpiresAtUtc = expiresAtUtc;
        Issuer = issuer;
    }

    // Checks whether the token has expired
    public bool IsExpired(DateTimeOffset utcNow) => utcNow >= ExpiresAtUtc;

    // Checks if the user has a specific Keycloak role
    public bool HasRole(string role) => Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));

    // Checks if the operator roles grant the requested permission
    public bool HasPermission(OperatorPermission permission)
    {
        // Admin has full unrestricted access
        if (HasRole("Admin"))
            return true;

        return permission switch
        {
            OperatorPermission.VerifyTpmAttestation => HasRole("MachineProvision"),
            OperatorPermission.ViewFleet => true,
            _ => false
        };
    }
}
