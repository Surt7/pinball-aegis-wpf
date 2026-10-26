using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Pinball_Aegis.Application.Constants;
using Pinball_Aegis.Application.Interfaces;
using Pinball_Aegis.Domain.Entities;

namespace Pinball_Aegis.Infrastructure.Services;

// Implementation communicating with the Monolith API using hardware mTLS and Keycloak Bearer token
public class HttpFleetAdminService : IFleetAdminService
{
    private readonly HttpClient _httpClient;
    private readonly IOperatorSessionService _sessionService;

    public HttpFleetAdminService(HttpClient httpClient, IOperatorSessionService sessionService)
    {
        _httpClient = httpClient;
        _sessionService = sessionService;
    }

    private sealed class MachineDto
    {
        public Guid MachineId { get; set; }
        public string MachineName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public System.Text.Json.JsonElement MachineStatus { get; set; }
        public string? MachineNotes { get; set; }
        public DateTime RegistredAtUtc { get; set; }
        public DateTime? LastUpdated { get; set; }
        public string SerialNumber { get; set; } = "";
        public DateTime? CertExpiresAtUtc { get; set; }
        public int? CertStatus { get; set; }

        public int GetStatusInt()
        {
            if (MachineStatus.ValueKind == System.Text.Json.JsonValueKind.Number && MachineStatus.TryGetInt32(out var i))
                return i;
            if (MachineStatus.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var s = MachineStatus.GetString()?.ToLowerInvariant();
                return s switch
                {
                    "active" => 1,
                    "revoked" => 2,
                    "inactive" => 3,
                    "maintenance" => 4,
                    "pendingenrollment" => 5,
                    _ => 0
                };
            }
            return 0;
        }
    }

    private sealed class RawIpLicenseDto
    {
        public Guid IpId { get; set; }
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
        public RawPublisherDto? Publisher { get; set; }
    }

    private sealed class RawPublisherDto
    {
        public Guid PublisherId { get; set; }
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
    }

    // Sends an HTTP request with automatic Keycloak token refresh on 401 Unauthorized or imminent expiry
    private async Task<HttpResponseMessage> SendWithAutoRefreshAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken ct = default)
    {
        // 1. Proactive refresh: if token is close to expiry (<15s) or expired, refresh before sending
        if (!string.IsNullOrEmpty(_sessionService.RefreshToken) &&
            (_sessionService.CurrentToken == null || _sessionService.CurrentToken.IsExpired(DateTimeOffset.UtcNow.AddSeconds(15))))
        {
            await _sessionService.RefreshSessionAsync(_sessionService.RefreshToken, ct);
        }

        // 2. Prepare request with current Bearer token
        var request = requestFactory();
        if (!string.IsNullOrEmpty(_sessionService.RawAccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sessionService.RawAccessToken);
        }

        var response = await _httpClient.SendAsync(request, ct);

        // 3. Reactive refresh: if server returns 401 Unauthorized, refresh token and retry request once
        if (response.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(_sessionService.RefreshToken))
        {
            var refreshed = await _sessionService.RefreshSessionAsync(_sessionService.RefreshToken, ct);
            if (refreshed && !string.IsNullOrEmpty(_sessionService.RawAccessToken))
            {
                var retryRequest = requestFactory();
                retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sessionService.RawAccessToken);
                response = await _httpClient.SendAsync(retryRequest, ct);
            }
        }

        return response;
    }

    public async Task<IReadOnlyList<MachineItem>> GetAllMachinesAsync(CancellationToken ct = default)
    {
        var response = await SendWithAutoRefreshAsync(() => new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Fleet.Machines), ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Array.Empty<MachineItem>();
        }

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"API HTTP {(int)response.StatusCode} : {err}");
        }

        var list = await response.Content.ReadFromJsonAsync<List<MachineDto>>(cancellationToken: ct);
        if (list is null)
            return Array.Empty<MachineItem>();

        return list.Select(m => new MachineItem
        {
            MachineId = m.MachineId,
            MachineName = m.MachineName,
            DisplayName = m.DisplayName,
            SerialNumber = m.SerialNumber,
            MachineStatus = m.GetStatusInt(),
            MachineNotes = m.MachineNotes,
            RegisteredAtUtc = m.RegistredAtUtc,
            LastUpdated = m.LastUpdated,
            CertExpiresAtUtc = m.CertExpiresAtUtc,
            CertStatus = m.CertStatus
        }).ToList();
    }

    public async Task<ReissueTokenResult> ReissueEnrollmentTokenAsync(Guid machineId, CancellationToken ct = default)
    {
        var url = $"{ApiRoutes.Fleet.Machines}/{machineId}/tokens";
        var response = await SendWithAutoRefreshAsync(() => new HttpRequestMessage(HttpMethod.Post, url), ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return new ReissueTokenResult(false, null, null, error);
        }

        var json = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);
        string? token = json?["enrollmentToken"]?.ToString();
        DateTime? expires = null;
        if (DateTime.TryParse(json?["expiresAtUtc"]?.ToString(), out var dt))
        {
            expires = dt;
        }

        return new ReissueTokenResult(true, token, expires, null);
    }

    public async Task<MachineSecurityDto?> GetMachineSecurityAsync(Guid machineId, CancellationToken ct = default)
    {
        var url = $"{ApiRoutes.Fleet.Machines}/{machineId}/security";
        var response = await SendWithAutoRefreshAsync(() => new HttpRequestMessage(HttpMethod.Get, url), ct);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MachineSecurityDto>(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<IpLicenseItem>> GetIpLicensesAsync(CancellationToken ct = default)
    {
        var response = await SendWithAutoRefreshAsync(() => new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Licenses.IpLicenses), ct);

        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<IpLicenseItem>();
        }

        var list = await response.Content.ReadFromJsonAsync<List<RawIpLicenseDto>>(cancellationToken: ct);
        if (list is null)
            return Array.Empty<IpLicenseItem>();

        return list.Select(l => new IpLicenseItem(l.IpId, l.Name, l.Slug, l.Publisher?.Name)).ToList();
    }

    public async Task<ProvisionMachineResult> ProvisionMachineAsync(ProvisionMachineParams parameters, CancellationToken ct = default)
    {
        var payload = new
        {
            worldPinballSerialNumber = parameters.SerialNumber,
            ipSlug = parameters.IpSlug,
            displayName = parameters.DisplayName,
            location = parameters.Location,
            machineNotes = parameters.Notes,
            publicKey = (string?)null,
            validityHours = (int?)null
        };

        var response = await SendWithAutoRefreshAsync(() =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Fleet.Provision)
            {
                Content = JsonContent.Create(payload)
            };
            req.Headers.Add("X-Idempotency-Key", Guid.NewGuid().ToString());
            return req;
        }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            return new ProvisionMachineResult(false, null, null, $"API HTTP {(int)response.StatusCode}: {err}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);
        var machineIdStr = json?["machineId"]?.ToString();
        var token = json?["enrollmentToken"]?.ToString();
        Guid? machineId = Guid.TryParse(machineIdStr, out var g) ? g : null;

        return new ProvisionMachineResult(true, machineId, token, null);
    }

    public async Task<CreateOperatorTokenResult> CreateOperatorTokenAsync(CreateOperatorTokenParams parameters, CancellationToken ct = default)
    {
        var payload = new
        {
            name = parameters.Name,
            scopes = parameters.Scopes,
            validityDays = parameters.ValidityDays
        };

        var response = await SendWithAutoRefreshAsync(() =>
        {
            return new HttpRequestMessage(HttpMethod.Post, ApiRoutes.OperatorTokens.Create)
            {
                Content = JsonContent.Create(payload)
            };
        }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            return new CreateOperatorTokenResult(false, null, null, null, null, $"API HTTP {(int)response.StatusCode}: {err}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);
        var tokenIdStr = json?["tokenId"]?.ToString();
        var name = json?["name"]?.ToString() ?? parameters.Name;
        var rawToken = json?["rawToken"]?.ToString();
        DateTime? expiresAt = DateTime.TryParse(json?["expiresAt"]?.ToString(), out var dt) ? dt : null;
        Guid? tokenId = Guid.TryParse(tokenIdStr, out var g) ? g : null;

        return new CreateOperatorTokenResult(true, tokenId, name, rawToken, expiresAt, null);
    }
}
