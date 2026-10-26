using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pinball_Aegis.Application.Interfaces;
using Pinball_Aegis.Domain.Enums;

namespace Pinball_Aegis.ViewModels;

// Dedicated ViewModel for Machine TPM Attestation & Hardware Verification
public partial class MachineAttestationViewModel : ObservableObject
{
    private readonly ITpmAttestationService _tpmService;
    private readonly IOperatorSessionService _sessionService;

    [ObservableProperty]
    private string _targetMachineId = "NUC-FLIPPER-042";

    [ObservableProperty]
    private string _connectionStatus = "Prêt à vérifier l'attestation du flipper.";

    // RBAC: button is unlocked if active operator has permission
    public bool CanVerifyTpm => _sessionService.HasPermission(OperatorPermission.VerifyTpmAttestation);

    public bool IsOperatorConnected => _sessionService.IsAuthenticated;
    public string OperatorStatusText => _sessionService.IsAuthenticated
        ? $"Opérateur : {_sessionService.CurrentToken?.CommonName} (Droits d'attestation validés)"
        : "Veuillez vous identifier en bas à gauche pour vérifier l'attestation.";

    public MachineAttestationViewModel(ITpmAttestationService tpmService, IOperatorSessionService sessionService)
    {
        _tpmService = tpmService;
        _sessionService = sessionService;

        // Auto-refresh UI when operator logs in or out from the global navigation sidebar
        _sessionService.SessionChanged += () =>
        {
            OnPropertyChanged(nameof(CanVerifyTpm));
            OnPropertyChanged(nameof(IsOperatorConnected));
            OnPropertyChanged(nameof(OperatorStatusText));
        };
    }

    [RelayCommand]
    private async Task VerifyMachineIdentityAsync()
    {
        ConnectionStatus = $"Vérification mTLS en cours pour {TargetMachineId}...";

        bool isAttested = await _tpmService.VerifyAttestationAsync(TargetMachineId);

        if (isAttested)
        {
            var details = await _tpmService.GetMachineDetailsAsync(TargetMachineId);
            ConnectionStatus = $"✔ Attestation validée pour {TargetMachineId} ({details?.ModelName}) !";
        }
        else
        {
            ConnectionStatus = $"❌ ÉCHEC : {TargetMachineId} rejeté (TPM invalide ou non reconnu).";
        }
    }
}
