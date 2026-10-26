using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pinball_Aegis.Application.Interfaces;

namespace Pinball_Aegis;

public enum SecurityPanelMode
{
    None,
    Workstation,
    License,
    Operator
}

// Shell ViewModel for MainWindow: manages global application state, 3-layer Zero-Trust barriers (Hardware, Software, Operator)
public partial class MainViewModel : ObservableObject
{
    private readonly IOperatorSessionService _sessionService;
    private readonly ICredentialStore _credentialStore;
    private readonly IWorkstationCertificateProvider _certificateProvider;
    private readonly ISoftwareLicenseService _licenseService;
    private const string CredentialTarget = "WorldPinball:OperatorSession";

    [ObservableProperty]
    private string _loginStatus = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoginWithKeycloak))]
    private bool _isLoggingIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorkstationPanelOpen))]
    [NotifyPropertyChangedFor(nameof(IsLicensePanelOpen))]
    [NotifyPropertyChangedFor(nameof(IsOperatorPanelOpen))]
    [NotifyPropertyChangedFor(nameof(IsAnyPanelOpen))]
    private SecurityPanelMode _activePanel = SecurityPanelMode.None;

    public bool IsWorkstationPanelOpen => ActivePanel == SecurityPanelMode.Workstation;
    public bool IsLicensePanelOpen => ActivePanel == SecurityPanelMode.License;
    public bool IsOperatorPanelOpen => ActivePanel == SecurityPanelMode.Operator;
    public bool IsAnyPanelOpen => ActivePanel != SecurityPanelMode.None;

    // --- BARRIÈRE 1 : Identité Matérielle du Poste (Hardware mTLS) ---
    public bool IsWorkstationEnrolled => _certificateProvider.HasCertificate && _certificateProvider.IsValid;
    public string WorkstationDisplayName => IsWorkstationEnrolled
        ? $"Poste : {_certificateProvider.CommonName}"
        : "Poste : Non Enrôlé (Accès Restreint)";
    public string WorkstationStatusBadge => IsWorkstationEnrolled
        ? "Matériel Approuvé (mTLS P-256)"
        : "Certificat Matériel Manquant";
    public string WorkstationCommonName => _certificateProvider.CommonName ?? "Non défini";
    public string WorkstationIssuer => _certificateProvider.Issuer ?? "Non défini";
    public string WorkstationStorageLocation => _certificateProvider.StorageLocation ?? "Aucun";
    public string WorkstationExpiresAt => _certificateProvider.ExpiresAt?.ToString("dd/MM/yyyy HH:mm") ?? "N/A";
    public string WorkstationThumbprint => _certificateProvider.CertificateThumbprint ?? "N/A";

    // --- BARRIÈRE 2 : Licence Logicielle Cryptographique (Software License) ---
    public bool IsSoftwareLicensed => _licenseService.IsValid;
    public string LicenseDisplayName => _licenseService.IsValid && _licenseService.ActiveLicense is not null
        ? $"Licence : {_licenseService.ActiveLicense.Product}"
        : "Licence : Non Validée (Accès Restreint)";
    public string LicenseStatusBadge => _licenseService.StatusDescription;
    public string LicenseProduct => _licenseService.ActiveLicense?.Product ?? "Pinball Aegis";
    public string LicenseEdition => _licenseService.ActiveLicense?.Edition ?? "Non licencié";
    public string LicenseLicensedTo => _licenseService.ActiveLicense?.LicensedTo ?? "Non attribué";
    public string LicenseHardwareBinding => _licenseService.ActiveLicense?.HardwareBinding ?? "Aucune";
    public string LicenseExpiresAt => _licenseService.ActiveLicense?.ExpiresAtUtc.ToString("dd/MM/yyyy") ?? "N/A";
    public IReadOnlyList<string> LicenseFeatures => _licenseService.ActiveLicense?.Features ?? Array.Empty<string>();
    public string LicenseValidationDetails => _licenseService.ValidationDetails;

    // Condition Zero-Trust absolue
    public bool CanLoginWithKeycloak => IsWorkstationEnrolled && IsSoftwareLicensed && !IsLoggingIn;

    // --- BARRIÈRE 3 : Identité Humaine (Opérateur Keycloak) ---
    public bool IsAuthenticated => _sessionService.IsAuthenticated;
    public string OperatorDisplayName => _sessionService.IsAuthenticated
        ? $"Opérateur : {_sessionService.CurrentToken!.CommonName} ({_sessionService.CurrentToken.PrimaryRole})"
        : "Opérateur : Non connecté (Cliquer pour s'identifier)";
    public string OperatorName => _sessionService.CurrentToken?.CommonName ?? "Non connecté";
    public string OperatorRole => _sessionService.CurrentToken?.PrimaryRole ?? "Aucun";
    public string OperatorIssuer => _sessionService.CurrentToken?.Issuer ?? "Keycloak";
    public string OperatorExpiresAt => _sessionService.CurrentToken?.ExpiresAtUtc.ToString("dd/MM/yyyy HH:mm") ?? "N/A";

    public MainViewModel(
        IOperatorSessionService sessionService, 
        ICredentialStore credentialStore,
        IWorkstationCertificateProvider certificateProvider,
        ISoftwareLicenseService licenseService)
    {
        _sessionService = sessionService;
        _credentialStore = credentialStore;
        _certificateProvider = certificateProvider;
        _licenseService = licenseService;

        _sessionService.SessionChanged += OnSessionChanged;

        // Auto-restore session from Windows Vault ONLY if both Hardware and Software barriers are valid
        if (IsWorkstationEnrolled && IsSoftwareLicensed)
        {
            var (savedUser, savedRefreshToken) = _credentialStore.Get(CredentialTarget);
            if (!string.IsNullOrEmpty(savedRefreshToken))
            {
                _ = RestoreSessionAsync(savedUser, savedRefreshToken);
            }
        }
    }

    private void OnSessionChanged()
    {
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(OperatorDisplayName));
        OnPropertyChanged(nameof(OperatorName));
        OnPropertyChanged(nameof(OperatorRole));
        OnPropertyChanged(nameof(OperatorIssuer));
        OnPropertyChanged(nameof(OperatorExpiresAt));
    }

    [RelayCommand]
    private void ToggleWorkstationPanel()
    {
        ActivePanel = ActivePanel == SecurityPanelMode.Workstation ? SecurityPanelMode.None : SecurityPanelMode.Workstation;
    }

    [RelayCommand]
    private void ToggleLicensePanel()
    {
        ActivePanel = ActivePanel == SecurityPanelMode.License ? SecurityPanelMode.None : SecurityPanelMode.License;
    }

    [RelayCommand]
    private void ToggleOperatorPanel()
    {
        ActivePanel = ActivePanel == SecurityPanelMode.Operator ? SecurityPanelMode.None : SecurityPanelMode.Operator;
    }

    [RelayCommand]
    private void ClosePanel()
    {
        ActivePanel = SecurityPanelMode.None;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (!IsWorkstationEnrolled)
        {
            LoginStatus = "Accès refusé : ce poste n'a pas de certificat matériel valide.";
            return;
        }

        if (!IsSoftwareLicensed)
        {
            LoginStatus = "Accès refusé : la licence logicielle est manquante, invalide ou non liée à ce poste.";
            return;
        }

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
                ClosePanel();
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
        ClosePanel();
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
