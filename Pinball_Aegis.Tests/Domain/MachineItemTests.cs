using Pinball_Aegis.Domain.Entities;

namespace Pinball_Aegis.Tests.Domain;

public class MachineItemTests
{
    [Fact]
    public void MachineItem_WhenNoCertificate_ShouldDisplayUnenrolledStatusAndGrayColor()
    {
        // Arrange
        var item = new MachineItem
        {
            CertExpiresAtUtc = null,
            CertStatus = null
        };

        // Assert
        item.HasCertificate.Should().BeFalse();
        item.IsCertExpired.Should().BeFalse();
        item.CertExpirationColor.Should().Be("#64748B");
        item.CertStatusText.Should().Be("Puce TPM non enrôlée / Aucun certificat");
        item.ReissueButtonText.Should().Be("Générer Jeton d'Enrôlement");
    }

    [Fact]
    public void MachineItem_WhenActiveAndValid_ShouldDisplayValidStatusAndGreenColor()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddYears(1);
        var item = new MachineItem
        {
            CertExpiresAtUtc = futureDate,
            CertStatus = 0 // Active
        };

        // Assert
        item.HasCertificate.Should().BeTrue();
        item.IsCertExpired.Should().BeFalse();
        item.CertExpirationColor.Should().Be("#10B981");
        item.CertStatusText.Should().Be($"Certificat TPM valide jusqu'au {futureDate:dd/MM/yyyy}");
        item.ReissueButtonText.Should().Be("Réémettre Jeton d'Enrôlement");
    }

    [Fact]
    public void MachineItem_WhenActiveAndExpiringWithin30Days_ShouldDisplayExpiringSoonAndAmberColor()
    {
        // Arrange
        var expiringSoon = DateTime.UtcNow.AddDays(15);
        var item = new MachineItem
        {
            CertExpiresAtUtc = expiringSoon,
            CertStatus = 0 // Active
        };

        // Assert
        item.HasCertificate.Should().BeTrue();
        item.IsCertExpired.Should().BeFalse();
        item.IsCertExpiringSoon.Should().BeTrue();
        item.CertExpirationColor.Should().Be("#F59E0B");
        item.CertStatusText.Should().StartWith("Expire bientôt le");
    }

    [Fact]
    public void MachineItem_WhenDateInPast_ShouldDisplayExpiredStatusAndRedColor()
    {
        // Arrange
        var pastDate = DateTime.UtcNow.AddDays(-10);
        var item = new MachineItem
        {
            CertExpiresAtUtc = pastDate,
            CertStatus = 0
        };

        // Assert
        item.IsCertExpired.Should().BeTrue();
        item.CertExpirationColor.Should().Be("#EF4444");
        item.CertStatusText.Should().Be($"Certificat expiré le {pastDate:dd/MM/yyyy}");
    }

    [Fact]
    public void MachineItem_WhenStatusIsInactiveInDbButDateInFuture_ShouldDetectAnomalyAndDisplayAmberColor()
    {
        // Arrange - WP-DI-LE-AR1 case
        var futureDate = DateTime.UtcNow.AddYears(1);
        var item = new MachineItem
        {
            CertExpiresAtUtc = futureDate,
            CertStatus = 1 // Status = 1 (inactive in DB)
        };

        // Assert
        item.IsCertExpired.Should().BeFalse();
        item.CertExpirationColor.Should().Be("#F59E0B");
        item.CertStatusText.Should().Be($"⚠️ Statut inactif en BDD (expiration prévue le {futureDate:dd/MM/yyyy})");
    }

    [Fact]
    public void MachineItem_WhenStatusIsRevoked_ShouldDisplayRevokedAndRedColor()
    {
        // Arrange
        var item = new MachineItem
        {
            CertExpiresAtUtc = DateTime.UtcNow.AddYears(1),
            CertStatus = 2 // Revoked
        };

        // Assert
        item.CertExpirationColor.Should().Be("#EF4444");
        item.CertStatusText.Should().Be("Certificat matériel TPM révoqué");
    }

    [Fact]
    public void MachineItem_WhenStatusIsSuperseded_ShouldDisplaySupersededAndGrayColor()
    {
        // Arrange
        var item = new MachineItem
        {
            CertExpiresAtUtc = DateTime.UtcNow.AddYears(1),
            CertStatus = 3 // Superseded
        };

        // Assert
        item.CertExpirationColor.Should().Be("#64748B");
        item.CertStatusText.Should().Be("Certificat matériel remplacé (obsolète)");
    }

    [Theory]
    [InlineData(1, "Active", "#51CF66")]
    [InlineData(2, "Révoquée", "#FF6B6B")]
    [InlineData(3, "Inactive", "#868E96")]
    [InlineData(4, "Maintenance", "#FCC419")]
    [InlineData(5, "En attente d'enrôlement", "#FF922B")]
    [InlineData(99, "Inconnu", "#868E96")]
    public void MachineItem_StatusTextAndColor_ShouldMapCorrectly(int status, string expectedText, string expectedColor)
    {
        // Arrange
        var item = new MachineItem { MachineStatus = status };

        // Assert
        item.StatusText.Should().Be(expectedText);
        item.StatusColor.Should().Be(expectedColor);
    }

    [Fact]
    public void MachineItem_CanIssueToken_ShouldBeTrueOnlyForPendingEnrollment()
    {
        // Arrange & Assert
        new MachineItem { MachineStatus = 5 }.CanIssueToken.Should().BeTrue();
        new MachineItem { MachineStatus = 1 }.CanIssueToken.Should().BeFalse();
        new MachineItem { MachineStatus = 4 }.CanIssueToken.Should().BeFalse();
    }
}
