using System.Windows.Controls;
using Pinball_Aegis.ViewModels;

namespace Pinball_Aegis.Views;

public partial class ProvisionMachineView : Page
{
    public ProvisionMachineView()
    {
        InitializeComponent();
        DataContext = new ProvisionMachineViewModel(App.FleetService);
    }
}
