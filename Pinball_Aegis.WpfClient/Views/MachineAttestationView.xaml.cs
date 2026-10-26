using System.Windows.Controls;
using Pinball_Aegis.ViewModels;

namespace Pinball_Aegis.Views;

public partial class MachineAttestationView : Page
{
    public MachineAttestationView()
    {
        InitializeComponent();

        DataContext = new MachineAttestationViewModel(
            App.TpmService, 
            App.SessionService, 
            App.FleetService, 
            App.LicenseService
        );
    }
}
