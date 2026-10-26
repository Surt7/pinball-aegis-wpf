using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pinball_Aegis.Application.Interfaces;

namespace Pinball_Aegis.ViewModels;

public partial class OperatorTokensViewModel : ObservableObject
{
    private readonly IFleetAdminService _fleetService;

    [ObservableProperty]
    private string _name = "Clé Technicien Atelier";

    [ObservableProperty]
    private int _validityDays = 30;

    [ObservableProperty]
    private bool _scopeEnroll = true;

    [ObservableProperty]
    private bool _scopeDiagnostics = true;

    [ObservableProperty]
    private bool _scopeAudit = false;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string _resultTokenId = "";

    [ObservableProperty]
    private string _resultRawToken = "";

    [ObservableProperty]
    private string _resultExpiresAt = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = "";

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public OperatorTokensViewModel(IFleetAdminService fleetService)
    {
        _fleetService = fleetService;
    }

    private string BuildScopesString()
    {
        var list = new List<string>();
        if (ScopeEnroll) list.Add("enroll");
        if (ScopeDiagnostics) list.Add("diagnostics");
        if (ScopeAudit) list.Add("audit");
        return list.Count > 0 ? string.Join(" ", list) : "*";
    }

    [RelayCommand]
    public async Task GenerateTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Le nom ou l'étiquette de la clé opérateur est obligatoire.";
            return;
        }

        if (ValidityDays <= 0)
        {
            ErrorMessage = "La durée de validité doit être supérieure à zéro.";
            return;
        }

        ErrorMessage = "";
        IsGenerating = true;
        StatusMessage = "Génération de la clé opérateur auprès du backend...";

        try
        {
            var scopes = BuildScopesString();
            var p = new CreateOperatorTokenParams(Name.Trim(), scopes, ValidityDays);

            var result = await _fleetService.CreateOperatorTokenAsync(p);
            if (result.Success && !string.IsNullOrEmpty(result.RawToken))
            {
                ResultTokenId = result.TokenId?.ToString() ?? "N/A";
                ResultRawToken = result.RawToken;
                ResultExpiresAt = result.ExpiresAtUtc?.ToString("dd/MM/yyyy HH:mm") ?? "N/A";
                HasResult = true;
                StatusMessage = $"Clé opérateur '{Name}' émise avec succès !";
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Échec de l'émission de la clé opérateur.";
                StatusMessage = "Erreur lors de la génération.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Exception : {ex.Message}";
            StatusMessage = "Erreur inattendue.";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private void CopyToken()
    {
        if (!string.IsNullOrEmpty(ResultRawToken))
        {
            Clipboard.SetText(ResultRawToken);
            StatusMessage = "Clé opérateur copiée dans le presse-papier !";
        }
    }

    [RelayCommand]
    private void ResetForm()
    {
        Name = "Clé Technicien Atelier";
        ValidityDays = 30;
        ScopeEnroll = true;
        ScopeDiagnostics = true;
        ScopeAudit = false;
        HasResult = false;
        ResultTokenId = "";
        ResultRawToken = "";
        ResultExpiresAt = "";
        ErrorMessage = "";
        StatusMessage = "";
    }
}
