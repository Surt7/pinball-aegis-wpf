using Pinball_Aegis.Application.Interfaces;
using Pinball_Aegis.ViewModels;

namespace Pinball_Aegis.Tests.ViewModels;

public class ProvisionMachineViewModelTests
{
    private readonly IFleetAdminService _fleetService = Substitute.For<IFleetAdminService>();

    [Fact]
    public async Task Provision_WhenSerialNumberIsEmpty_ShouldSetErrorMessage()
    {
        // Arrange
        var vm = new ProvisionMachineViewModel(_fleetService)
        {
            SerialNumber = "",
            DisplayName = "Flipper Test"
        };

        // Act
        await vm.ProvisionCommand.ExecuteAsync(null);

        // Assert
        vm.ErrorMessage.Should().Be("Le numéro de série World Pinball est obligatoire.");
        vm.HasError.Should().BeTrue();
        vm.HasResult.Should().BeFalse();
    }

    [Fact]
    public async Task Provision_WhenDisplayNameIsEmpty_ShouldSetErrorMessage()
    {
        // Arrange
        var vm = new ProvisionMachineViewModel(_fleetService)
        {
            SerialNumber = "WP-999",
            DisplayName = ""
        };

        // Act
        await vm.ProvisionCommand.ExecuteAsync(null);

        // Assert
        vm.ErrorMessage.Should().Be("Le nom d'affichage du flipper est obligatoire.");
        vm.HasError.Should().BeTrue();
    }

    [Fact]
    public async Task Provision_WhenNoLicenseSelected_ShouldSetErrorMessage()
    {
        // Arrange
        var vm = new ProvisionMachineViewModel(_fleetService)
        {
            SerialNumber = "WP-999",
            DisplayName = "Flipper Test",
            SelectedLicense = null
        };

        // Act
        await vm.ProvisionCommand.ExecuteAsync(null);

        // Assert
        vm.ErrorMessage.Should().Be("Veuillez sélectionner une licence IP pour ce flipper.");
        vm.HasError.Should().BeTrue();
    }

    [Fact]
    public async Task Provision_WhenValid_ShouldCallFleetServiceAndSetResult()
    {
        // Arrange
        var machineId = Guid.NewGuid();
        var license = new IpLicenseItem(Guid.NewGuid(), "Resident Evil", "resident-evil", "Capcom");
        _fleetService.ProvisionMachineAsync(Arg.Any<ProvisionMachineParams>(), Arg.Any<CancellationToken>())
            .Returns(new ProvisionMachineResult(true, machineId, "TOKEN-OTP-123456", null));

        var vm = new ProvisionMachineViewModel(_fleetService)
        {
            SerialNumber = "WP-DI-LE-001",
            DisplayName = "Resident Evil Death Island Limited",
            SelectedLicense = license
        };

        // Act
        await vm.ProvisionCommand.ExecuteAsync(null);

        // Assert
        vm.HasError.Should().BeFalse();
        vm.HasResult.Should().BeTrue();
        vm.ResultMachineId.Should().Be(machineId.ToString());
        vm.ResultToken.Should().Be("TOKEN-OTP-123456");
        vm.StatusMessage.Should().Contain("provisionné avec succès");
    }

    [Fact]
    public async Task Provision_WhenServiceFails_ShouldSetErrorMessage()
    {
        // Arrange
        var license = new IpLicenseItem(Guid.NewGuid(), "Resident Evil", "resident-evil", "Capcom");
        _fleetService.ProvisionMachineAsync(Arg.Any<ProvisionMachineParams>(), Arg.Any<CancellationToken>())
            .Returns(new ProvisionMachineResult(false, null, null, "Serial already registered."));

        var vm = new ProvisionMachineViewModel(_fleetService)
        {
            SerialNumber = "WP-DI-LE-001",
            DisplayName = "Resident Evil Death Island Limited",
            SelectedLicense = license
        };

        // Act
        await vm.ProvisionCommand.ExecuteAsync(null);

        // Assert
        vm.HasError.Should().BeTrue();
        vm.HasResult.Should().BeFalse();
        vm.ErrorMessage.Should().Be("Serial already registered.");
    }
}
