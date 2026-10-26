using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pinball_Aegis.Application.Interfaces;

namespace Pinball_Aegis.ViewModels;

public partial class ProvisionMachineViewModel : ObservableObject
{
    private readonly IFleetAdminService _fleetService;

    [ObservableProperty]
    private string _serialNumber = "WP-";

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private string _location = "";

    [ObservableProperty]
    private string _notes = "";

    [ObservableProperty]
    private IpLicenseItem? _selectedLicense;

    [ObservableProperty]
    private bool _isLoadingLicenses;

    [ObservableProperty]
    private bool _isProvisioning;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string _resultMachineId = "";

    [ObservableProperty]
    private string _resultToken = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = "";

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public ObservableCollection<IpLicenseItem> IpLicenses { get; } = new();

    public ProvisionMachineViewModel(IFleetAdminService fleetService)
    {
        _fleetService = fleetService;
        _ = LoadLicensesAsync();
    }

    [RelayCommand]
    public async Task LoadLicensesAsync()
    {
        if (IsLoadingLicenses) return;
        IsLoadingLicenses = true;
        StatusMessage = "Chargement des licences disponibles...";

        try
        {
            var licenses = await _fleetService.GetIpLicensesAsync();
            IpLicenses.Clear();
            foreach (var lic in licenses)
            {
                IpLicenses.Add(lic);
            }

            if (IpLicenses.Count > 0 && SelectedLicense == null)
            {
                SelectedLicense = IpLicenses[0];
            }

            StatusMessage = $"{IpLicenses.Count} licence(s) récupérée(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur lors du chargement des licences : {ex.Message}";
        }
        finally
        {
            IsLoadingLicenses = false;
        }
    }

    [RelayCommand]
    public async Task ProvisionAsync()
    {
        if (string.IsNullOrWhiteSpace(SerialNumber) || SerialNumber == "WP-")
        {
            ErrorMessage = "Le numéro de série World Pinball est obligatoire.";
            return;
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            ErrorMessage = "Le nom d'affichage du flipper est obligatoire.";
            return;
        }

        if (SelectedLicense == null)
        {
            ErrorMessage = "Veuillez sélectionner une licence IP pour ce flipper.";
            return;
        }

        ErrorMessage = "";
        IsProvisioning = true;
        StatusMessage = "Provisioning du flipper auprès du backend...";

        try
        {
            var p = new ProvisionMachineParams(
                SerialNumber: SerialNumber.Trim(),
                DisplayName: DisplayName.Trim(),
                IpSlug: SelectedLicense.Slug,
                Location: string.IsNullOrWhiteSpace(Location) ? null : Location.Trim(),
                Notes: string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
            );

            var result = await _fleetService.ProvisionMachineAsync(p);
            if (result.Success && result.MachineId.HasValue && !string.IsNullOrEmpty(result.EnrollmentToken))
            {
                ResultMachineId = result.MachineId.Value.ToString();
                ResultToken = result.EnrollmentToken;
                HasResult = true;
                StatusMessage = $"Flipper '{DisplayName}' provisionné avec succès en statut 'PendingEnrollment' !";
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Échec du provisioning de la machine.";
                StatusMessage = "Erreur lors du provisioning.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Exception : {ex.Message}";
            StatusMessage = "Erreur inattendue.";
        }
        finally
        {
            IsProvisioning = false;
        }
    }

    [RelayCommand]
    private void CopyToken()
    {
        if (!string.IsNullOrEmpty(ResultToken))
        {
            Clipboard.SetText(ResultToken);
            StatusMessage = "Jeton d'enrôlement copié dans le presse-papier !";
        }
    }

    [RelayCommand]
    private void CopyMachineId()
    {
        if (!string.IsNullOrEmpty(ResultMachineId))
        {
            Clipboard.SetText(ResultMachineId);
            StatusMessage = "ID Machine copié dans le presse-papier !";
        }
    }

    [RelayCommand]
    private void ResetForm()
    {
        SerialNumber = "WP-";
        DisplayName = "";
        Location = "";
        Notes = "";
        HasResult = false;
        ResultMachineId = "";
        ResultToken = "";
        ErrorMessage = "";
        StatusMessage = "";
    }
}
