using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        var sv = FindVisualParent<ScrollViewer>(this);
        if (sv != null)
        {
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
                return parent;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}
