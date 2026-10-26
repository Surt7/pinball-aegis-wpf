using System.Windows.Controls;
using Pinball_Aegis.ViewModels;

namespace Pinball_Aegis.Views;

public partial class OperatorTokensView : Page
{
    public OperatorTokensView()
    {
        InitializeComponent();
        DataContext = new OperatorTokensViewModel(App.FleetService);
    }
}
