using System.Net.Http;
using System.Windows;
using Pinball_Aegis.Application.Interfaces;
using Pinball_Aegis.Infrastructure.Services;

namespace Pinball_Aegis;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public static IWorkstationCertificateProvider CertificateProvider { get; } = new WorkstationCertificateProvider();
    public static ISoftwareLicenseService LicenseService { get; } = new SoftwareLicenseService(CertificateProvider);
    public static HttpClient HttpClient { get; } = MtlsHttpClientFactory.CreateClient(CertificateProvider);
    public static IOperatorSessionService SessionService { get; } = new KeycloakOperatorSessionService(HttpClient);
    public static ICredentialStore CredentialStore { get; } = new WindowsCredentialStore();
    public static ITpmAttestationService TpmService { get; } = new HttpTpmAttestationService(HttpClient);
    public static IFleetAdminService FleetService { get; } = new HttpFleetAdminService(HttpClient, SessionService);
}
