using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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
    public string? RefreshToken { get; private set; }
    public bool IsAuthenticated => CurrentToken is not null && !CurrentToken.IsExpired(DateTimeOffset.UtcNow);

    public event Action? SessionChanged;

    public KeycloakOperatorSessionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<bool> AuthenticateAsync(string rawTokenOrJwt, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ParseTokens(rawTokenOrJwt, null, null));
    }

    public bool HasPermission(OperatorPermission permission)
    {
        return IsAuthenticated && CurrentToken!.HasPermission(permission);
    }

    public void Logout()
    {
        CurrentToken = null;
        RefreshToken = null;
        SessionChanged?.Invoke();
    }

    // Authenticates operator using OAuth 2.1 / RFC 7636 Authorization Code Flow with PKCE (S256)
    public async Task<bool> LoginWithKeycloakPkceAsync(CancellationToken cancellationToken = default)
    {
        // 1. Generate high-entropy cryptographic code_verifier and code_challenge (RFC 7636)
        byte[] verifierBytes = new byte[32];
        RandomNumberGenerator.Fill(verifierBytes);
        string codeVerifier = Base64UrlEncode(verifierBytes);

        using var sha256 = SHA256.Create();
        byte[] challengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        string codeChallenge = Base64UrlEncode(challengeBytes);

        // 2. Generate cryptographically random state to protect against CSRF
        byte[] stateBytes = new byte[16];
        RandomNumberGenerator.Fill(stateBytes);
        string state = Base64UrlEncode(stateBytes);

        // 3. Start local ephemeral loopback listener (RFC 8252 section 7.3)
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:8282/");
        listener.Start();

        try
        {
            // 4. Construct authorization URL and open system browser
            var authUrl = $"{ApiRoutes.Keycloak.AuthorizationEndpoint}?" +
                $"response_type=code" +
                $"&client_id={Uri.EscapeDataString(ApiRoutes.Keycloak.ClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(ApiRoutes.Keycloak.RedirectUri)}" +
                $"&scope={Uri.EscapeDataString("openid profile roles email")}" +
                $"&code_challenge={codeChallenge}" +
                $"&code_challenge_method=S256" +
                $"&state={state}";

            Process.Start(new ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });

            // 5. Wait for loopback authorization response (timeout 120s)
            var getContextTask = listener.GetContextAsync();
            var delayTask = Task.Delay(TimeSpan.FromSeconds(120), cancellationToken);

            var completedTask = await Task.WhenAny(getContextTask, delayTask);
            if (completedTask != getContextTask)
            {
                throw new TimeoutException("Délai d'attente d'authentification Keycloak dépassé (120 secondes).");
            }

            var context = await getContextTask;
            var query = context.Request.QueryString;
            string? returnedState = query["state"];
            string? code = query["code"];
            string? error = query["error"];
            string? errorDescription = query["error_description"];

            // 6. Return friendly HTML response to close browser tab
            byte[] responseBytes = Encoding.UTF8.GetBytes(
                "<!DOCTYPE html><html><head><meta charset='utf-8'><title>Pinball Aegis</title>" +
                "<style>body{background:#0f172a;color:#f8fafc;font-family:system-ui,-apple-system,sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;margin:0;}" +
                ".card{background:#1e293b;border:1px solid #334155;border-radius:12px;padding:32px;text-align:center;box-shadow:0 10px 25px rgba(0,0,0,0.5);max-width:440px;}" +
                "h2{color:#38bdf8;margin-top:0;}p{color:#94a3b8;font-size:14px;line-height:1.6;}</style></head>" +
                "<body><div class='card'><h2>Authentification Réussie</h2><p>Vous êtes désormais connecté à <strong>Pinball Aegis</strong>.<br/>Vous pouvez fermer cet onglet et retourner à l'application.</p></div></body></html>");

            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = responseBytes.Length;
            await context.Response.OutputStream.WriteAsync(responseBytes, cancellationToken);
            context.Response.OutputStream.Close();

            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException($"Erreur Keycloak : {error} ({errorDescription})");

            if (returnedState != state)
                throw new InvalidOperationException("Validation de sécurité échouée : paramètre 'state' non concordant.");

            if (string.IsNullOrEmpty(code))
                throw new InvalidOperationException("Aucun code d'autorisation reçu de Keycloak.");

            // 7. Exchange authorization code and code_verifier at Token Endpoint
            var requestBody = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = ApiRoutes.Keycloak.ClientId,
                ["code"] = code,
                ["redirect_uri"] = ApiRoutes.Keycloak.RedirectUri,
                ["code_verifier"] = codeVerifier
            };

            var tokenResponse = await _httpClient.PostAsync(
                ApiRoutes.Keycloak.TokenEndpoint,
                new FormUrlEncodedContent(requestBody),
                cancellationToken
            );

            if (!tokenResponse.IsSuccessStatusCode)
            {
                var errorDetails = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Échange de code Keycloak HTTP {(int)tokenResponse.StatusCode}: {errorDetails}");
            }

            var json = await tokenResponse.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
            string? accessToken = json?["access_token"]?.ToString();
            string? idToken = json?["id_token"]?.ToString();

            if (string.IsNullOrWhiteSpace(accessToken))
                return false;

            RefreshToken = json?["refresh_token"]?.ToString();

            // 8. Parse real tokens and extract realm_access.roles
            return ParseTokens(accessToken, idToken, null);
        }
        finally
        {
            if (listener.IsListening)
                listener.Stop();
        }
    }

    // Authenticates against real Keycloak container using Direct Access / Password Grant (legacy fallback)
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
            string? idToken = json?["id_token"]?.ToString();

            if (string.IsNullOrWhiteSpace(accessToken))
                return false;

            RefreshToken = json?["refresh_token"]?.ToString();

            return ParseTokens(accessToken, idToken, username);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Erreur réseau Keycloak : {ex.Message}");
        }
    }

    public async Task<bool> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return false;

            var requestBody = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = ApiRoutes.Keycloak.ClientId,
                ["refresh_token"] = refreshToken
            };

            var response = await _httpClient.PostAsync(
                ApiRoutes.Keycloak.TokenEndpoint, 
                new FormUrlEncodedContent(requestBody), 
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: cancellationToken);
            string? newAccessToken = json?["access_token"]?.ToString();
            string? newIdToken = json?["id_token"]?.ToString();
            string? newRefreshToken = json?["refresh_token"]?.ToString();

            if (string.IsNullOrWhiteSpace(newAccessToken))
                return false;

            RefreshToken = newRefreshToken ?? refreshToken;
            return ParseTokens(newAccessToken, newIdToken, CurrentToken?.CommonName);
        }
        catch
        {
            return false;
        }
    }

    private bool ParseTokens(string accessToken, string? idToken, string? fallbackUsername)
    {
        try
        {
            var accessParts = accessToken.Split('.');
            if (accessParts.Length < 2)
                return false;

            byte[] accessBytes = DecodeBase64Url(accessParts[1]);
            var accessNode = JsonNode.Parse(accessBytes);
            if (accessNode is null)
                return false;

            JsonNode? idNode = null;
            if (!string.IsNullOrWhiteSpace(idToken))
            {
                var idParts = idToken.Split('.');
                if (idParts.Length >= 2)
                {
                    byte[] idBytes = DecodeBase64Url(idParts[1]);
                    idNode = JsonNode.Parse(idBytes);
                    try
                    {
                        var idDebugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keycloak_id_token_debug.json");
                        File.WriteAllText(idDebugPath, idNode.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                    }
                    catch { }
                }
            }

            // Dump for diagnostic verification
            try
            {
                var debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keycloak_token_debug.json");
                File.WriteAllText(debugPath, accessNode.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }

            // Extract human display name from OIDC ID Token first, then Access Token, then fallback username
            string displayName = idNode?["name"]?.ToString()
                ?? accessNode["name"]?.ToString()
                ?? idNode?["preferred_username"]?.ToString()
                ?? accessNode["preferred_username"]?.ToString()
                ?? (!string.IsNullOrWhiteSpace(fallbackUsername) ? fallbackUsername : null)
                ?? idNode?["sub"]?.ToString()
                ?? accessNode["sub"]?.ToString()
                ?? "Opérateur";

            string userId = idNode?["sub"]?.ToString() ?? accessNode["sub"]?.ToString() ?? displayName;
            long expSeconds = accessNode["exp"]?.GetValue<long>() ?? 0;
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expSeconds);

            // Collect all real roles from realm_access, direct roles, resource_access in both Access Token and ID Token
            var rolesList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void CollectRoles(JsonNode? node)
            {
                if (node is null) return;

                if (node["realm_access"]?["roles"] is JsonArray realmRoles)
                {
                    foreach (var r in realmRoles)
                        if (r?.ToString() is { } s && !string.IsNullOrWhiteSpace(s))
                            rolesList.Add(s);
                }

                if (node["roles"] is JsonArray directRoles)
                {
                    foreach (var r in directRoles)
                        if (r?.ToString() is { } s && !string.IsNullOrWhiteSpace(s))
                            rolesList.Add(s);
                }

                if (node["resource_access"] is JsonObject resAccess)
                {
                    foreach (var clientEntry in resAccess)
                    {
                        if (clientEntry.Value?["roles"] is JsonArray clientRoles)
                        {
                            foreach (var r in clientRoles)
                                if (r?.ToString() is { } s && !string.IsNullOrWhiteSpace(s))
                                    rolesList.Add(s);
                        }
                    }
                }
            }

            CollectRoles(accessNode);
            CollectRoles(idNode);

            CurrentToken = new OperatorToken(
                operatorId: userId,
                commonName: displayName,
                roles: rolesList,
                expiresAtUtc: expiresAt,
                issuer: ApiRoutes.Keycloak.Authority
            );

            SessionChanged?.Invoke();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] DecodeBase64Url(string base64Url)
    {
        string padded = base64Url.Trim().Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
