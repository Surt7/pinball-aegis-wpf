using System.Windows.Controls;
using Pinball_Aegis.Views;
using Wpf.Ui.Controls;

namespace Pinball_Aegis;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = new MainViewModel(App.SessionService, App.CredentialStore);

        // Navigate to Security & TPM page on startup
        Loaded += (_, _) =>
        {
            RootNavigation.Navigate(typeof(MachineAttestationView));
        };
    }
}
