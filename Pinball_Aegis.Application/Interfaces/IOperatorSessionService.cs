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

    // Authenticates operator using raw token payload / certificate thumbprint
    Task<bool> AuthenticateAsync(string tokenData, CancellationToken cancellationToken = default);

    // Checks if the current operator has a specific permission
    bool HasPermission(OperatorPermission permission);

    // Logs out the current operator
    void Logout();
    
    // Authenticates operator against Identity Provider using username and password
    Task<bool> LoginToKeycloakAsync(string username, string password, CancellationToken cancellationToken = default);
}