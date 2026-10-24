using System.Windows.Controls;

namespace Pinball_Aegis.Views;

public partial class FleetView : Page
{
    public FleetView()
    {
        InitializeComponent();
        
        DataContext = new ViewModels.FleetViewModel();
    }
}