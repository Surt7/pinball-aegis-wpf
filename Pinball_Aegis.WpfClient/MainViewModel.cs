using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pinball_Aegis.Application.Interfaces;

namespace Pinball_Aegis;

// Shell ViewModel for MainWindow: manages global application state and operator profile
public partial class MainViewModel : ObservableObject
{
    private readonly IOperatorSessionService _sessionService;
    private readonly ICredentialStore _credentialStore;
    private const string CredentialTarget = "WorldPinball:OperatorSession";

    [ObservableProperty]
    private string _loginStatus = "";

    [ObservableProperty]
    private bool _isLoggingIn;

    [ObservableProperty]
    private bool _isLoginDrawerOpen;

    public bool IsAuthenticated => _sessionService.IsAuthenticated;

    public string OperatorDisplayName => _sessionService.IsAuthenticated
        ? $"Opérateur : {_sessionService.CurrentToken!.CommonName} ({_sessionService.CurrentToken.PrimaryRole})"
        : "Opérateur : Non connecté (Cliquer pour s'identifier)";

    public MainViewModel(IOperatorSessionService sessionService, ICredentialStore credentialStore)
    {
        _sessionService = sessionService;
        _credentialStore = credentialStore;

        _sessionService.SessionChanged += OnSessionChanged;

        // Auto-restore session from Windows Vault using stored Refresh Token
        var (savedUser, savedRefreshToken) = _credentialStore.Get(CredentialTarget);
        if (!string.IsNullOrEmpty(savedRefreshToken))
        {
            _ = RestoreSessionAsync(savedUser, savedRefreshToken);
        }
    }

    private void OnSessionChanged()
    {
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(OperatorDisplayName));
    }

    [RelayCommand]
    private void ToggleLoginDrawer()
    {
        IsLoginDrawerOpen = !IsLoginDrawerOpen;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsLoggingIn) return;

        IsLoggingIn = true;
        LoginStatus = "Ouverture de la mire d'authentification Keycloak...";

        try
        {
            var success = await _sessionService.LoginWithKeycloakPkceAsync();
            if (success)
            {
                if (!string.IsNullOrEmpty(_sessionService.RefreshToken) && _sessionService.CurrentToken is not null)
                {
                    _credentialStore.Save(CredentialTarget, _sessionService.CurrentToken.CommonName, _sessionService.RefreshToken);
                }
                LoginStatus = "";
                IsLoginDrawerOpen = false;
            }
            else
            {
                LoginStatus = "Échec de l'authentification Keycloak.";
            }
        }
        catch (Exception ex)
        {
            LoginStatus = ex.Message;
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    [RelayCommand]
    private void Logout()
    {
        _sessionService.Logout();
        _credentialStore.Delete(CredentialTarget);
        LoginStatus = "";
        IsLoginDrawerOpen = false;
    }

    private async Task RestoreSessionAsync(string? savedUser, string refreshToken)
    {
        bool restored = await _sessionService.RefreshSessionAsync(refreshToken);
        if (restored)
        {
            if (!string.IsNullOrEmpty(_sessionService.RefreshToken))
            {
                string username = _sessionService.CurrentToken?.CommonName ?? savedUser ?? "Operator";
                _credentialStore.Save(CredentialTarget, username, _sessionService.RefreshToken);
            }
        }
        else
        {
            _credentialStore.Delete(CredentialTarget);
        }
    }
}
