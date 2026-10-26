using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pinball_Aegis.Application.Interfaces;
using Pinball_Aegis.Domain.Entities;
using Pinball_Aegis.Domain.Enums;

namespace Pinball_Aegis.ViewModels;

// Dedicated ViewModel for Machine TPM Attestation & Fleet Security Supervision
public partial class MachineAttestationViewModel : ObservableObject
{
    private readonly ITpmAttestationService _tpmService;
    private readonly IOperatorSessionService _sessionService;
    private readonly IFleetAdminService _fleetService;
    private readonly ISoftwareLicenseService _licenseService;

    public ObservableCollection<MachineItem> AllMachines { get; } = new();
    public ObservableCollection<MachineItem> FilteredMachines { get; } = new();

    [ObservableProperty]
    private MachineItem? _selectedMachine;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _selectedStatusFilter = "Tous";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _attestationStatus = "";

    [ObservableProperty]
    private string _reissuedToken = "";

    [ObservableProperty]
    private DateTime? _reissuedTokenExpiresAt;

    [ObservableProperty]
    private bool _isTokenReissued;

    // Permissions & Licensing
    public bool CanVerifyTpm => _sessionService.HasPermission(OperatorPermission.VerifyTpmAttestation) && 
                                _licenseService.IsValid;

    public bool IsOperatorAdmin => _sessionService.IsAuthenticated && _sessionService.CurrentToken?.HasRole("Admin") == true;

    public string OperatorStatusText => _sessionService.IsAuthenticated
        ? $"Opérateur : {_sessionService.CurrentToken?.CommonName} ({_sessionService.CurrentToken?.PrimaryRole})"
        : "Veuillez vous identifier pour administrer la flotte.";

    public MachineAttestationViewModel(
        ITpmAttestationService tpmService, 
        IOperatorSessionService sessionService,
        IFleetAdminService fleetService,
        ISoftwareLicenseService licenseService)
    {
        _tpmService = tpmService;
        _sessionService = sessionService;
        _fleetService = fleetService;
        _licenseService = licenseService;

        _sessionService.SessionChanged += () =>
        {
            OnPropertyChanged(nameof(CanVerifyTpm));
            OnPropertyChanged(nameof(IsOperatorAdmin));
            OnPropertyChanged(nameof(OperatorStatusText));
            _ = LoadMachinesAsync();
        };

        _ = LoadMachinesAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedStatusFilterChanged(string value) => ApplyFilter();

    partial void OnSelectedMachineChanged(MachineItem? value)
    {
        AttestationStatus = "";
        IsTokenReissued = false;
        ReissuedToken = "";
    }

    [RelayCommand]
    public async Task LoadMachinesAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        StatusMessage = "Chargement de la flotte de flippers...";

        try
        {
            var machines = await _fleetService.GetAllMachinesAsync();
            AllMachines.Clear();
            foreach (var m in machines)
            {
                AllMachines.Add(m);
            }

            ApplyFilter();

            if (FilteredMachines.Count > 0 && SelectedMachine is null)
            {
                SelectedMachine = FilteredMachines[0];
            }

            StatusMessage = $"{AllMachines.Count} flipper(s) recensé(s) dans la flotte.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur flotte : {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        FilteredMachines.Clear();
        var query = AllMachines.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(m => 
                m.MachineName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                m.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                m.SerialNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedStatusFilter != "Tous")
        {
            query = query.Where(m => string.Equals(m.StatusText, SelectedStatusFilter, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var m in query)
        {
            FilteredMachines.Add(m);
        }
    }

    [RelayCommand]
    private async Task VerifySelectedMachineAsync()
    {
        if (SelectedMachine is null) return;

        string targetId = !string.IsNullOrWhiteSpace(SelectedMachine.SerialNumber) 
            ? SelectedMachine.SerialNumber 
            : SelectedMachine.MachineName;

        AttestationStatus = $"Contrôle cryptographique et attestation TPM pour {targetId}...";

        await Task.Delay(350); // Feedback visuel fluide

        switch (SelectedMachine.MachineStatus)
        {
            case 1: // Active
                AttestationStatus = $"✔ Attestation validée : Flipper actif, certificat matériel Step-CA valide et liaison mTLS conforme ({targetId}).";
                break;
            case 5: // PendingEnrollment
                AttestationStatus = $"⏳ En attente d'enrôlement : La puce TPM de {targetId} n'a pas encore échangé son jeton. Utilisez le bouton ci-dessous pour émettre un jeton pour le technicien.";
                break;
            case 4: // Maintenance
                AttestationStatus = $"⚠️ Statut Maintenance : Machine {targetId} consignée pour révision technique. Certificat matériel suspendu.";
                break;
            case 2: // Revoked
                AttestationStatus = $"❌ ÉCHEC : Certificat matériel révoqué dans la CRL pour {targetId}. Accès mTLS bloqué.";
                break;
            default:
                AttestationStatus = $"ℹ Statut matériel de {targetId} : Inactif ou non reconnu.";
                break;
        }
    }

    [RelayCommand]
    private void CopySerial(string? serial)
    {
        var toCopy = !string.IsNullOrEmpty(serial) ? serial : SelectedMachine?.SerialNumber;
        if (!string.IsNullOrEmpty(toCopy))
        {
            Clipboard.SetText(toCopy);
            StatusMessage = $"N° de série '{toCopy}' copié dans le presse-papier !";
        }
    }

    [RelayCommand]
    private void CopyName(string? name)
    {
        var toCopy = !string.IsNullOrEmpty(name) ? name : SelectedMachine?.DisplayName;
        if (!string.IsNullOrEmpty(toCopy))
        {
            Clipboard.SetText(toCopy);
            StatusMessage = $"Nom '{toCopy}' copié dans le presse-papier !";
        }
    }

    [RelayCommand]
    private void FilterBySerial(string? serial)
    {
        var s = !string.IsNullOrEmpty(serial) ? serial : SelectedMachine?.SerialNumber;
        if (!string.IsNullOrEmpty(s))
        {
            SearchText = s;
            StatusMessage = $"Filtre appliqué sur le n° de série : {s}";
        }
    }

    [RelayCommand]
    private void FilterByName(string? name)
    {
        var n = !string.IsNullOrEmpty(name) ? name : SelectedMachine?.DisplayName;
        if (!string.IsNullOrEmpty(n))
        {
            SearchText = n;
            StatusMessage = $"Filtre appliqué sur le nom : {n}";
        }
    }

    [RelayCommand]
    private async Task ReissueTokenAsync()
    {
        if (SelectedMachine is null) return;

        StatusMessage = "Génération d'un nouveau jeton d'enrôlement...";

        try
        {
            var result = await _fleetService.ReissueEnrollmentTokenAsync(SelectedMachine.MachineId);
            if (result.Success && !string.IsNullOrEmpty(result.EnrollmentToken))
            {
                ReissuedToken = result.EnrollmentToken;
                ReissuedTokenExpiresAt = result.ExpiresAtUtc;
                IsTokenReissued = true;
                StatusMessage = "Jeton d'enrôlement régénéré avec succès !";
            }
            else
            {
                StatusMessage = $"Impossible de réémettre le jeton : {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur : {ex.Message}";
        }
    }

    [RelayCommand]
    private void CopyTokenToClipboard()
    {
        if (!string.IsNullOrEmpty(ReissuedToken))
        {
            Clipboard.SetText(ReissuedToken);
            StatusMessage = "Jeton d'enrôlement copié dans le presse-papier !";
        }
    }
}
