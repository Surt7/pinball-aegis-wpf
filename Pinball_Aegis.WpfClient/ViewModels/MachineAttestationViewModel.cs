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
    private string _attestationStatusColor = "#E2E8F0";

    [ObservableProperty]
    private string _attestationStatusBorder = "Transparent";

    [ObservableProperty]
    private string _attestationStatusBackground = "Transparent";

    [ObservableProperty]
    private string _reissuedToken = "";

    [ObservableProperty]
    private DateTime? _reissuedTokenExpiresAt;

    [ObservableProperty]
    private bool _isTokenReissued;

    [ObservableProperty]
    private MachineSecurityDto? _securityDetails;

    public bool HasSecurityDetails => SecurityDetails is not null && SecurityDetails.HasCertificate;

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
        SetAttestationStatus("", "#E2E8F0");
        IsTokenReissued = false;
        ReissuedToken = "";
        SecurityDetails = null;
        OnPropertyChanged(nameof(HasSecurityDetails));
        OnPropertyChanged(nameof(CanReissueToken));
        OnPropertyChanged(nameof(ReissueButtonText));
    }

    private void SetAttestationStatus(string status, string textColor, string borderColor = "Transparent", string backgroundColor = "Transparent")
    {
        AttestationStatus = status;
        AttestationStatusColor = textColor;
        AttestationStatusBorder = borderColor;
        AttestationStatusBackground = backgroundColor;
    }
    
    
    // Computed permission and state for issuing / reissuing flipper enrollment tokens
    public bool CanReissueToken => IsOperatorAdmin && SelectedMachine != null && SelectedMachine.CanIssueToken;
    public string ReissueButtonText => SelectedMachine?.ReissueButtonText ?? "Générer Jeton d'Enrôlement";
    
    
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

        SetAttestationStatus($"Interrogation de l'autorité Step-CA et du registre matériel pour {targetId}...", "#38BDF8", "#38BDF84D", "#1A38BDF8");

        try
        {
            var sec = await _fleetService.GetMachineSecurityAsync(SelectedMachine.MachineId);
            SecurityDetails = sec;
            OnPropertyChanged(nameof(HasSecurityDetails));

            if (sec is null)
            {
                SetAttestationStatus($"❌ Erreur : Impossible de joindre le registre de sécurité pour {targetId}.", "#EF4444", "#EF44444D", "#1AEF4444");
                return;
            }

            if (!sec.HasAttachedDevice)
            {
                SetAttestationStatus($"❌ REJETÉ : Aucun périphérique physique (TPM) n'est rattaché à ce flipper.", "#EF4444", "#EF44444D", "#1AEF4444");
            }
            else if (!sec.HasCertificate)
            {
                SetAttestationStatus($"⏳ EN ATTENTE : Puce TPM ({sec.DevicePhysicalSerial}) détectée mais aucun certificat émis. Enrôlement requis.", "#38BDF8", "#38BDF84D", "#1A38BDF8");
            }
            else if (sec.CertStatus == 2)
            {
                SetAttestationStatus($"❌ RÉVOQUÉ : Le certificat matériel {sec.CertSerialNumber} est révoqué dans la CRL.", "#EF4444", "#EF44444D", "#1AEF4444");
            }
            else if (sec.CertStatus == 3)
            {
                SetAttestationStatus($"🔄 REMPLACÉ : Le certificat matériel {sec.CertSerialNumber} a été remplacé (obsolète).", "#94A3B8", "#94A3B84D", "#1A94A3B8");
            }
            else if (sec.IsExpired)
            {
                SetAttestationStatus($"⚠️ EXPIRÉ : Le certificat matériel {sec.CertSerialNumber} a expiré le {sec.NotAfterUtc:dd/MM/yyyy}.", "#EF4444", "#EF44444D", "#1AEF4444");
            }
            else if (sec.CertStatus == 1)
            {
                SetAttestationStatus($"⚠️ ANOMALIE : Le certificat {sec.CertSerialNumber} est marqué inactif en BDD (Status=1) mais sa date d'expiration ({sec.NotAfterUtc:dd/MM/yyyy}) est encore valide.", "#F59E0B", "#F59E0B4D", "#1AF59E0B");
            }
            else if (sec.IsValid)
            {
                SetAttestationStatus($"✔ CERTIFIÉ : Certificat {sec.CertSerialNumber} valide jusqu'au {sec.NotAfterUtc:dd/MM/yyyy} ({sec.IssuerDn}). mTLS actif.", "#10B981", "#10B9814D", "#1A10B981");
            }
            else
            {
                SetAttestationStatus($"ℹ Statut matériel : {sec.StatusExplanation}", "#F59E0B", "#F59E0B4D", "#1AF59E0B");
            }
        }
        catch (Exception ex)
        {
            SetAttestationStatus($"Erreur d'attestation mTLS : {ex.Message}", "#EF4444", "#EF44444D", "#1AEF4444");
        }
    }

    [RelayCommand]
    private void CopyMachineId(Guid? machineId)
    {
        var id = machineId ?? SelectedMachine?.MachineId;
        if (id.HasValue && id.Value != Guid.Empty)
        {
            Clipboard.SetText(id.Value.ToString());
            StatusMessage = $"ID Machine '{id.Value}' copié dans le presse-papier !";
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
