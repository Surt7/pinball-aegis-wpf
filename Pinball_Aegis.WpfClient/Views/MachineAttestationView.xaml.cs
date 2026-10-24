using System.Net.Http;
using System.Windows.Controls;
using Pinball_Aegis.Infrastructure.Services;
using Pinball_Aegis.ViewModels; 

namespace Pinball_Aegis.Views;

public partial class MachineAttestationView : Page
{
    public MachineAttestationView()
    {
        InitializeComponent();
        
        var httpClient = new HttpClient();
        
 
        DataContext = new MachineAttestationViewModel(
            new HttpTpmAttestationService(httpClient),
            new KeycloakOperatorSessionService(httpClient)
        );
    }
}