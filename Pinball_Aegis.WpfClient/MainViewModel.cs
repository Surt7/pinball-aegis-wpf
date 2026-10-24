using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pinball_Aegis.Application.Interfaces;

namespace Pinball_Aegis;

// Shell ViewModel for MainWindow: manages global application state and operator profile
public partial class MainViewModel : ObservableObject
{
    private readonly IOperatorSessionService _sessionService;

    // Displayed in the sidebar footer
    public string OperatorDisplayName => _sessionService.IsAuthenticated 
        ? $"Opérateur : {_sessionService.CurrentToken!.CommonName} ({_sessionService.CurrentToken.Role})" 
        : "Opérateur : Non authentifié";

    public MainViewModel(IOperatorSessionService sessionService)
    {
        _sessionService = sessionService;
    }
}