using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pinball_Aegis.Application.Interfaces;
using Pinball_Aegis.Domain.Enums;

namespace Pinball_Aegis.ViewModels;

// Dedicated ViewModel for the Machine Attestation & Security view
public partial class MachineAttestationViewModel : ObservableObject
{
    private readonly ITpmAttestationService _tpmService;
    private readonly IOperatorSessionService _sessionService;

    [ObservableProperty]
    private string _targetMachineId = "NUC-FLIPPER-042";

    [ObservableProperty]
    private string _connectionStatus = "En attente d'attestation TPM / Certificat...";

    // RBAC: only allowed if active operator has the permission
    public bool CanVerifyTpm => _sessionService.HasPermission(OperatorPermission.VerifyTpmAttestation);

    public MachineAttestationViewModel(ITpmAttestationService tpmService, IOperatorSessionService sessionService)
    {
        _tpmService = tpmService;
        _sessionService = sessionService;
    }

    [RelayCommand]
    private async Task VerifyMachineIdentityAsync()
    {
        ConnectionStatus = $"Vérification en cours pour {TargetMachineId}...";
        
        bool isAttested = await _tpmService.VerifyAttestationAsync(TargetMachineId);
        
        if (isAttested)
        {
            var details = await _tpmService.GetMachineDetailsAsync(TargetMachineId);
            ConnectionStatus = $"Attestation validée pour {TargetMachineId} ({details?.ModelName}) !";
        }
        else
        {
            ConnectionStatus = $"ÉCHEC : {TargetMachineId} rejeté (TPM invalide ou non reconnu).";
        }
    }
    
    
    [RelayCommand]
    private async Task LoginAsTechnicianAsync()
    {
        ConnectionStatus = "Connexion à Keycloak en cours...";
        try
        {
            await _sessionService.LoginToKeycloakAsync("", "");
            ConnectionStatus = "Connecté avec succès à Keycloak !";
        }
        catch (Exception ex)
        {
            ConnectionStatus = ex.Message;
        }
        OnPropertyChanged(nameof(CanVerifyTpm));
    }
}