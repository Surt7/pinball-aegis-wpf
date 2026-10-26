using Pinball_Aegis.Domain.Entities;
using Pinball_Aegis.Domain.Enums;

namespace Pinball_Aegis.Application.Interfaces;

// Contract for managing current operator authentication and active session
public interface IOperatorSessionService
{
    // The currently active operator token (null if unauthenticated)
    OperatorToken? CurrentToken { get; }

    // Whether an operator is currently authenticated with a non-expired token
    bool IsAuthenticated { get; }

    // Event fired whenever session state changes (login, refresh, logout)
    event Action? SessionChanged;

    // Active refresh token (used to restore session without asking for password)
    string? RefreshToken { get; }

    // Active JWT access token (Bearer token sent to protected API endpoints)
    string? RawAccessToken { get; }

    // Authenticates operator using raw token payload / certificate thumbprint
    Task<bool> AuthenticateAsync(string tokenData, CancellationToken cancellationToken = default);

    // Checks if the current operator has a specific permission
    bool HasPermission(OperatorPermission permission);

    // Logs out the current operator
    void Logout();

    // Authenticates operator via OIDC Authorization Code Flow with PKCE (RFC 7636)
    Task<bool> LoginWithKeycloakPkceAsync(CancellationToken cancellationToken = default);

    // Legacy direct password authentication (kept for backward compatibility or scripts)
    Task<bool> LoginToKeycloakAsync(string username, string password, CancellationToken cancellationToken = default);

    // Renews the active session using a stored Keycloak refresh token
    Task<bool> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken = default);
}
