using CommunityToolkit.Mvvm.ComponentModel;

namespace Pinball_Aegis.ViewModels;

public partial class FleetViewModel : ObservableObject
{
    [ObservableProperty]
    private int _onlineMachinesCount = 3;

    [ObservableProperty]
    private int _securityAlertsCount = 0;

    public string FleetStatusSummary => 
        $"{OnlineMachinesCount} machines en ligne / {SecurityAlertsCount} alerte de sécurité";
}