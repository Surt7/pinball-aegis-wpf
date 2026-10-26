using System.Windows.Input;
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

        DataContext = new MainViewModel(
            App.SessionService, 
            App.CredentialStore, 
            App.CertificateProvider, 
            App.LicenseService
        );

        // Navigate to Security & TPM page on startup
        Loaded += (_, _) =>
        {
            RootNavigation.Navigate(typeof(MachineAttestationView));
        };
    }

    // Light-dismiss: Click outside the floating card closes it automatically
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);

        if (DataContext is MainViewModel vm && vm.IsAnyPanelOpen)
        {
            // If the mouse click did not happen inside the card or on one of the 3 trigger buttons
            if (!SecurityCardBorder.IsMouseOver &&
                !WorkstationNavItem.IsMouseOver &&
                !LicenseNavItem.IsMouseOver &&
                !OperatorNavItem.IsMouseOver)
            {
                vm.ClosePanelCommand.Execute(null);
            }
        }
    }

    // Keyboard UX: Pressing Escape immediately closes the open security card
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key == Key.Escape && DataContext is MainViewModel vm && vm.IsAnyPanelOpen)
        {
            vm.ClosePanelCommand.Execute(null);
            e.Handled = true;
        }
    }
}
