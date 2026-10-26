using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Pinball_Aegis.Application.Constants;
using Pinball_Aegis.Application.Interfaces;

namespace Pinball_Aegis.Infrastructure.Services;

// Factory creating HttpClients configured with hardware mTLS workstation identity
public static class MtlsHttpClientFactory
{
    public static HttpClient CreateClient(IWorkstationCertificateProvider certificateProvider)
    {
        var handler = new SocketsHttpHandler();

        // 1. Attach workstation mTLS client certificate if present
        if (certificateProvider.HasCertificate)
        {
            var cert = certificateProvider.GetCertificate();
            if (cert is not null)
            {
                handler.SslOptions.ClientCertificates = new X509CertificateCollection { cert };
            }
        }

        // 2. Allow local development Step-CA certificates (api.worldpinball.local)
        handler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            // In dev environment, trust our local domain
            if (sender is HttpRequestMessage req && 
                req.RequestUri?.Host.Contains("worldpinball.local", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            return false;
        };

        return new HttpClient(handler)
        {
            BaseAddress = new Uri(ApiRoutes.BaseAddress),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }
}