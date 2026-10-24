using System.Buffers.Text;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pinball_Aegis.Application.Constants;
using Pinball_Aegis.Application.Interfaces;
using Pinball_Aegis.Domain.Entities;
using Pinball_Aegis.Domain.Enums;

namespace Pinball_Aegis.Infrastructure.Services;

// Production session service authenticating operators directly against Keycloak
public class KeycloakOperatorSessionService : IOperatorSessionService
{
    private readonly HttpClient _httpClient;

    public OperatorToken? CurrentToken { get; private set; }

    public bool IsAuthenticated => CurrentToken is not null && !CurrentToken.IsExpired(DateTimeOffset.UtcNow);

    public KeycloakOperatorSessionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> AuthenticateAsync(string rawTokenOrJwt, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rawTokenOrJwt))
                return false;

            // Support local demo / test technician badges
            if (rawTokenOrJwt.StartsWith("fake", StringComparison.OrdinalIgnoreCase))
            {
                CurrentToken = new OperatorToken(
                    operatorId: "TECH-007",
                    commonName: "Alice Martin",
                    role: OperatorRole.FieldTechnician,
                    expiresAtUtc: DateTimeOffset.UtcNow.AddHours(8),
                    issuer: ApiRoutes.Keycloak.Authority
                );
                return true;
            }
            
            
            // Parse JWT payload (middle segment)
            var parts = rawTokenOrJwt.Split('.');
            if (parts.Length < 2)
                return false;

            // Decode base64url payload
            byte[] payloadBytes = Base64Url.DecodeFromUtf8(Encoding.UTF8.GetBytes(parts[1]));
            var jsonNode = JsonNode.Parse(payloadBytes);
            if (jsonNode is null)
                return false;

            string username = jsonNode["preferred_username"]?.ToString() ?? "operator";
            long expSeconds = jsonNode["exp"]?.GetValue<long>() ?? 0;
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expSeconds);

            // Extract roles from Keycloak JWT claims
            var role = OperatorRole.FieldTechnician; // Default role
            var rolesArray = jsonNode["roles"]?.AsArray();
            if (rolesArray is not null)
            {
                foreach (var r in rolesArray)
                {
                    if (string.Equals(r?.ToString(), "Admin", StringComparison.OrdinalIgnoreCase))
                    {
                        role = OperatorRole.SuperAdmin;
                        break;
                    }
                }
            }

            // Create Domain OperatorToken
            CurrentToken = new OperatorToken(
                operatorId: username,
                commonName: username,
                role: role,
                expiresAtUtc: expiresAt,
                issuer: ApiRoutes.Keycloak.Authority
            );

            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool HasPermission(OperatorPermission permission)
    {
        return IsAuthenticated && CurrentToken!.HasPermission(permission);
    }

    public void Logout()
    {
        CurrentToken = null;
    }
    
    
    // Authenticates against real Keycloak container using Direct Access / Password Grant
    public async Task<bool> LoginToKeycloakAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = ApiRoutes.Keycloak.ClientId,
                ["username"] = username,
                ["password"] = password,
                ["scope"] = "openid profile roles"
            };

            var response = await _httpClient.PostAsync(
                ApiRoutes.Keycloak.TokenEndpoint, 
                new FormUrlEncodedContent(requestBody), 
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Keycloak HTTP {(int)response.StatusCode}: {errorDetails}");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
            string? accessToken = json?["access_token"]?.ToString();

            if (string.IsNullOrWhiteSpace(accessToken))
                return false;

            // Parse and populate session from real Keycloak JWT
            return await AuthenticateAsync(accessToken, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Erreur réseau Keycloak : {ex.Message}");
        }
    }
}