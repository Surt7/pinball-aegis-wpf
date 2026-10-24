using System.Net.Http.Json;
using Pinball_Aegis.Application.Constants;
using Pinball_Aegis.Application.Interfaces;
using Pinball_Aegis.Domain.Entities;

namespace Pinball_Aegis.Infrastructure.Services;

// Production HTTP service communicating with the Nginx / WorldPinball API
public class HttpTpmAttestationService : ITpmAttestationService
{
    private readonly HttpClient _httpClient;

    public HttpTpmAttestationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        
        // Base domain pointing to our Nginx reverse proxy
        _httpClient.BaseAddress ??= new Uri(ApiRoutes.BaseAddress);
    }

    public async Task<bool> VerifyAttestationAsync(string machineId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Payload sent to the backend
            var payload = new { MachineId = machineId };

            // Call the endpoint using our centralized route constant
            var response = await _httpClient.PostAsJsonAsync(
                ApiRoutes.Attestation.Verify, 
                payload, 
                cancellationToken
            );

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            // Network unreachable or TLS handshake failed
            return false;
        }
    }

    public async Task<PinballMachine?> GetMachineDetailsAsync(string machineId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Call machine details endpoint using constant
            var url = $"{ApiRoutes.Attestation.MachineDetails}/{Uri.EscapeDataString(machineId)}";
            return await _httpClient.GetFromJsonAsync<PinballMachine>(url, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}