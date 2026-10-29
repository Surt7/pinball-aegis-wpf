using Pinball_Aegis.Application.Interfaces;
using Pinball_Aegis.Domain.Entities;
using Pinball_Aegis.Domain.Enums;
using Pinball_Aegis.ViewModels;

namespace Pinball_Aegis.Tests.ViewModels;

public class MachineAttestationViewModelTests
{
    private readonly ITpmAttestationService _tpmService = Substitute.For<ITpmAttestationService>();
    private readonly IOperatorSessionService _sessionService = Substitute.For<IOperatorSessionService>();
    private readonly IFleetAdminService _fleetService = Substitute.For<IFleetAdminService>();
    private readonly ISoftwareLicenseService _licenseService = Substitute.For<ISoftwareLicenseService>();

    public MachineAttestationViewModelTests()
    {
        // Default permissions for testing
        _sessionService.HasPermission(OperatorPermission.VerifyTpmAttestation).Returns(true);
        _licenseService.IsValid.Returns(true);
    }

    private MachineAttestationViewModel CreateViewModel() =>
        new(_tpmService, _sessionService, _fleetService, _licenseService);

    [Fact]
    public async Task LoadMachines_ShouldPopulateFleetAndSelectFirstMachine()
    {
        // Arrange
        var machines = new List<MachineItem>
        {
            new() { MachineId = Guid.NewGuid(), DisplayName = "Resident Evil 1", SerialNumber = "WP-001", MachineStatus = 1 },
            new() { MachineId = Guid.NewGuid(), DisplayName = "Resident Evil 2", SerialNumber = "WP-002", MachineStatus = 5 }
        };
        _fleetService.GetAllMachinesAsync(Arg.Any<CancellationToken>()).Returns(machines);

        var vm = CreateViewModel();

        // Act
        await vm.LoadMachinesCommand.ExecuteAsync(null);

        // Assert
        vm.AllMachines.Should().HaveCount(2);
        vm.FilteredMachines.Should().HaveCount(2);
        vm.SelectedMachine.Should().Be(machines[0]);
        vm.StatusMessage.Should().Contain("2 flipper(s)");
    }

    [Fact]
    public async Task SearchText_ShouldFilterMachinesByDisplayNameOrSerial()
    {
        // Arrange
        var machines = new List<MachineItem>
        {
            new() { MachineId = Guid.NewGuid(), DisplayName = "Medieval Madness", SerialNumber = "WP-155", MachineStatus = 1 },
            new() { MachineId = Guid.NewGuid(), DisplayName = "Resident Evil Death Island", SerialNumber = "WP-DI-LE", MachineStatus = 1 }
        };
        _fleetService.GetAllMachinesAsync(Arg.Any<CancellationToken>()).Returns(machines);

        var vm = CreateViewModel();
        await vm.LoadMachinesCommand.ExecuteAsync(null);

        // Act
        vm.SearchText = "Resident";

        // Assert
        vm.FilteredMachines.Should().HaveCount(1);
        vm.FilteredMachines[0].DisplayName.Should().Be("Resident Evil Death Island");
    }

    [Fact]
    public async Task StatusFilter_ShouldFilterMachinesByStatusText()
    {
        // Arrange
        var machines = new List<MachineItem>
        {
            new() { MachineId = Guid.NewGuid(), DisplayName = "Machine Active", SerialNumber = "WP-001", MachineStatus = 1 },
            new() { MachineId = Guid.NewGuid(), DisplayName = "Machine En Attente", SerialNumber = "WP-002", MachineStatus = 5 }
        };
        _fleetService.GetAllMachinesAsync(Arg.Any<CancellationToken>()).Returns(machines);

        var vm = CreateViewModel();
        await vm.LoadMachinesCommand.ExecuteAsync(null);

        // Act
        vm.SelectedStatusFilter = "En attente d'enrôlement";

        // Assert
        vm.FilteredMachines.Should().HaveCount(1);
        vm.FilteredMachines[0].DisplayName.Should().Be("Machine En Attente");
    }

    [Fact]
    public async Task VerifySelectedMachine_WhenCertified_ShouldSetGreenStatusAndCertifiedText()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        var selectedMachine = new MachineItem { MachineId = machineId, SerialNumber = "WP-DI-LE-AR1" };
        var secDto = new MachineSecurityDto(
            machineId,
            "WP-DI-LE-AR1",
            "Resident Evil Death Island AR1",
            1,
            HasAttachedDevice: true,
            Guid.NewGuid(),
            "DEV-001",
            HasCertificate: true,
            CertSerialNumber: "CERT-001",
            CertThumbprintSha256: "SHA256",
            SubjectDn: "CN=WP-DI-LE-AR1",
            IssuerDn: "CN=WorldPinball CA",
            NotBeforeUtc: DateTime.UtcNow.AddDays(-10),
            NotAfterUtc: DateTime.UtcNow.AddYears(1),
            CertStatus: 0,
            IsExpired: false,
            IsValid: true,
            StatusExplanation: "Certificat matériel TPM valide et vérifié."
        );

        _fleetService.GetMachineSecurityAsync(machineId, Arg.Any<CancellationToken>()).Returns(secDto);

        var vm = CreateViewModel();
        vm.SelectedMachine = selectedMachine;

        // Act
        await vm.VerifySelectedMachineCommand.ExecuteAsync(null);

        // Assert
        vm.AttestationStatus.Should().StartWith("✔ CERTIFIÉ");
        vm.AttestationStatusColor.Should().Be("#10B981");
    }

    [Fact]
    public async Task VerifySelectedMachine_WhenStatusIsInactiveInDbWithFutureDate_ShouldDetectAnomaly()
    {
        // Arrange - WP-DI-LE-AR1 anomaly case
        var machineId = Guid.NewGuid();
        var selectedMachine = new MachineItem { MachineId = machineId, SerialNumber = "WP-DI-LE-AR1" };
        var secDto = new MachineSecurityDto(
            machineId,
            "WP-DI-LE-AR1",
            "Resident Evil Death Island AR1",
            1,
            HasAttachedDevice: true,
            Guid.NewGuid(),
            "DEV-001",
            HasCertificate: true,
            CertSerialNumber: "CERT-WP-DI-LE-AR1",
            CertThumbprintSha256: "SHA256",
            SubjectDn: "CN=WP-DI-LE-AR1",
            IssuerDn: "CN=WorldPinball CA",
            NotBeforeUtc: DateTime.UtcNow.AddDays(-10),
            NotAfterUtc: DateTime.UtcNow.AddYears(1),
            CertStatus: 1, // Status = 1 (inactive in DB)
            IsExpired: false,
            IsValid: false,
            StatusExplanation: "Certificat non actif."
        );

        _fleetService.GetMachineSecurityAsync(machineId, Arg.Any<CancellationToken>()).Returns(secDto);

        var vm = CreateViewModel();
        vm.SelectedMachine = selectedMachine;

        // Act
        await vm.VerifySelectedMachineCommand.ExecuteAsync(null);

        // Assert
        vm.AttestationStatus.Should().StartWith("⚠️ ANOMALIE");
        vm.AttestationStatusColor.Should().Be("#F59E0B");
    }

    [Fact]
    public async Task VerifySelectedMachine_WhenExpired_ShouldSetRedStatusAndExpiredText()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        var selectedMachine = new MachineItem { MachineId = machineId, SerialNumber = "WP-00020" };
        var secDto = new MachineSecurityDto(
            machineId,
            "WP-00020",
            "Flipper 20",
            1,
            HasAttachedDevice: true,
            Guid.NewGuid(),
            "DEV-020",
            HasCertificate: true,
            CertSerialNumber: "CERT-020",
            CertThumbprintSha256: "SHA256",
            SubjectDn: "CN=WP-020",
            IssuerDn: "CN=WorldPinball CA",
            NotBeforeUtc: DateTime.UtcNow.AddYears(-2),
            NotAfterUtc: DateTime.UtcNow.AddDays(-5),
            CertStatus: 0,
            IsExpired: true,
            IsValid: false,
            StatusExplanation: "Certificat matériel expiré."
        );

        _fleetService.GetMachineSecurityAsync(machineId, Arg.Any<CancellationToken>()).Returns(secDto);

        var vm = CreateViewModel();
        vm.SelectedMachine = selectedMachine;

        // Act
        await vm.VerifySelectedMachineCommand.ExecuteAsync(null);

        // Assert
        vm.AttestationStatus.Should().StartWith("⚠️ EXPIRÉ");
        vm.AttestationStatusColor.Should().Be("#EF4444");
    }

    [Fact]
    public async Task VerifySelectedMachine_WhenRevoked_ShouldSetRedStatusAndRevokedText()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        var selectedMachine = new MachineItem { MachineId = machineId, SerialNumber = "WP-REV" };
        var secDto = new MachineSecurityDto(
            machineId,
            "WP-REV",
            "Flipper Revoked",
            2,
            HasAttachedDevice: true,
            Guid.NewGuid(),
            "DEV-REV",
            HasCertificate: true,
            CertSerialNumber: "CERT-REV",
            CertThumbprintSha256: "SHA256",
            SubjectDn: "CN=WP-REV",
            IssuerDn: "CN=WorldPinball CA",
            NotBeforeUtc: DateTime.UtcNow.AddMonths(-6),
            NotAfterUtc: DateTime.UtcNow.AddMonths(6),
            CertStatus: 2, // Revoked
            IsExpired: false,
            IsValid: false,
            StatusExplanation: "Certificat révoqué."
        );

        _fleetService.GetMachineSecurityAsync(machineId, Arg.Any<CancellationToken>()).Returns(secDto);

        var vm = CreateViewModel();
        vm.SelectedMachine = selectedMachine;

        // Act
        await vm.VerifySelectedMachineCommand.ExecuteAsync(null);

        // Assert
        vm.AttestationStatus.Should().StartWith("❌ RÉVOQUÉ");
        vm.AttestationStatusColor.Should().Be("#EF4444");
    }

    [Fact]
    public async Task VerifySelectedMachine_WhenSuperseded_ShouldSetGrayStatusAndReplacedText()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        var selectedMachine = new MachineItem { MachineId = machineId, SerialNumber = "WP-OLD" };
        var secDto = new MachineSecurityDto(
            machineId,
            "WP-OLD",
            "Flipper Old",
            1,
            HasAttachedDevice: true,
            Guid.NewGuid(),
            "DEV-OLD",
            HasCertificate: true,
            CertSerialNumber: "CERT-OLD",
            CertThumbprintSha256: "SHA256",
            SubjectDn: "CN=WP-OLD",
            IssuerDn: "CN=WorldPinball CA",
            NotBeforeUtc: DateTime.UtcNow.AddYears(-1),
            NotAfterUtc: DateTime.UtcNow.AddMonths(6),
            CertStatus: 3, // Superseded
            IsExpired: false,
            IsValid: false,
            StatusExplanation: "Certificat matériel remplacé."
        );

        _fleetService.GetMachineSecurityAsync(machineId, Arg.Any<CancellationToken>()).Returns(secDto);

        var vm = CreateViewModel();
        vm.SelectedMachine = selectedMachine;

        // Act
        await vm.VerifySelectedMachineCommand.ExecuteAsync(null);

        // Assert
        vm.AttestationStatus.Should().StartWith("🔄 REMPLACÉ");
        vm.AttestationStatusColor.Should().Be("#94A3B8");
    }

    [Fact]
    public void WhenSelectedMachineChanges_ShouldResetAttestationState()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.SelectedMachine = new MachineItem { MachineId = Guid.NewGuid(), SerialNumber = "WP-001" };

        // Act
        vm.SelectedMachine = new MachineItem { MachineId = Guid.NewGuid(), SerialNumber = "WP-002" };

        // Assert
        vm.AttestationStatus.Should().BeEmpty();
        vm.SecurityDetails.Should().BeNull();
        vm.HasSecurityDetails.Should().BeFalse();
    }
}
