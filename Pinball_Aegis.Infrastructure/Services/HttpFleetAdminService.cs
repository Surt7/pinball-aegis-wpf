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

    public async Task<IReadOnlyList<MachineItem>> GetAllMachinesAsync(CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Fleet.Machines);

        if (!string.IsNullOrEmpty(_sessionService.RawAccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sessionService.RawAccessToken);
        }

        var response = await _httpClient.SendAsync(request, ct);

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
            LastUpdated = m.LastUpdated
        }).ToList();
    }

    public async Task<ReissueTokenResult> ReissueEnrollmentTokenAsync(Guid machineId, CancellationToken ct = default)
    {
        var url = $"{ApiRoutes.Fleet.Machines}/{machineId}/tokens";
        var request = new HttpRequestMessage(HttpMethod.Post, url);

        if (!string.IsNullOrEmpty(_sessionService.RawAccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sessionService.RawAccessToken);
        }

        var response = await _httpClient.SendAsync(request, ct);

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
}
