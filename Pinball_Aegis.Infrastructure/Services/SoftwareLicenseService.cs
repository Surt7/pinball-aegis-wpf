using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pinball_Aegis.Application.Interfaces;
using Pinball_Aegis.Domain.Entities;

namespace Pinball_Aegis.Infrastructure.Services;

// Cryptographic software license validator using ECDSA P-256 signatures issued by Step-CA
public class SoftwareLicenseService : ISoftwareLicenseService
{
    private readonly IWorkstationCertificateProvider _workstationProvider;
    private SoftwareLicense? _activeLicense;
    private bool _signatureValid;
    private string _validationDetails = "Aucune licence vérifiée.";
    private string _statusDescription = "Licence Non Trouvée";

    public bool HasLicense => _activeLicense is not null;
    public bool IsValid => _signatureValid && 
                           _activeLicense is not null && 
                           !_activeLicense.IsExpired(DateTimeOffset.UtcNow) && 
                           _activeLicense.IsHardwareBoundTo(_workstationProvider.CommonName);

    public SoftwareLicense? ActiveLicense => _activeLicense;
    public string StatusDescription => _statusDescription;
    public string ValidationDetails => _validationDetails;

    public SoftwareLicenseService(IWorkstationCertificateProvider workstationProvider)
    {
        _workstationProvider = workstationProvider;
        RefreshLicense(_workstationProvider.CommonName);
    }

    public void RefreshLicense(string? workstationCommonName)
    {
        var (licensePath, signerCert) = ResolveLicenseAndSigner();
        if (licensePath is null || !File.Exists(licensePath))
        {
            _activeLicense = null;
            _signatureValid = false;
            _statusDescription = "Licence Manquante";
            _validationDetails = "Fichier aegis.lic introuvable. Exécutez 'wp-tools software license' pour générer une licence signée.";
            return;
        }

        if (signerCert is null)
        {
            _activeLicense = null;
            _signatureValid = false;
            _statusDescription = "Certificat Émetteur Manquant";
            _validationDetails = "Certificat de signature logicielle Step-CA introuvable (software-signing.crt).";
            return;
        }

        try
        {
            string licenseContent = File.ReadAllText(licensePath);
            var root = JsonNode.Parse(licenseContent);
            if (root is null)
            {
                SetInvalid("Format de licence invalide (JSON corrompu).");
                return;
            }

            string? payloadBase64 = root["payloadBase64"]?.ToString();
            string? signatureBase64 = root["signatureBase64"]?.ToString();

            if (string.IsNullOrWhiteSpace(payloadBase64) || string.IsNullOrWhiteSpace(signatureBase64))
            {
                SetInvalid("Structure de licence invalide : payloadBase64 ou signatureBase64 manquant.");
                return;
            }

            byte[] payloadBytes = Convert.FromBase64String(payloadBase64);
            byte[] signatureBytes = Convert.FromBase64String(signatureBase64);

            // 1. Verify ECDSA P-256 digital signature against Step-CA Software Signing Certificate
            using var ecdsa = signerCert.GetECDsaPublicKey();
            if (ecdsa is null)
            {
                SetInvalid("Le certificat d'autorité logicielle n'utilise pas l'algorithme ECDSA.");
                return;
            }

            bool signatureMatches = ecdsa.VerifyData(
                payloadBytes, 
                signatureBytes, 
                HashAlgorithmName.SHA256, 
                DSASignatureFormat.Rfc3279DerSequence
            );

            if (!signatureMatches)
            {
                SetInvalid("Signature cryptographique falsifiée ou invalide. Licence rejetée.");
                return;
            }

            _signatureValid = true;

            // 2. Parse payload contents
            var payloadNode = JsonNode.Parse(payloadBytes);
            if (payloadNode is null)
            {
                SetInvalid("Contenu de la licence illisible.");
                return;
            }

            string licenseId = payloadNode["licenseId"]?.ToString() ?? "UNKNOWN";
            string product = payloadNode["product"]?.ToString() ?? "Pinball Aegis";
            string edition = payloadNode["edition"]?.ToString() ?? "Standard";
            string licensedTo = payloadNode["licensedTo"]?.ToString() ?? "WorldPinball";
            string hardwareBinding = payloadNode["hardwareBinding"]?.ToString() ?? "*";
            DateTimeOffset issuedAt = DateTimeOffset.Parse(payloadNode["issuedAtUtc"]?.ToString() ?? DateTimeOffset.UtcNow.ToString("O"));
            DateTimeOffset expiresAt = DateTimeOffset.Parse(payloadNode["expiresAtUtc"]?.ToString() ?? DateTimeOffset.UtcNow.ToString("O"));

            var features = new List<string>();
            if (payloadNode["features"] is JsonArray featArray)
            {
                foreach (var f in featArray)
                {
                    if (f?.ToString() is { } s && !string.IsNullOrWhiteSpace(s))
                        features.Add(s);
                }
            }

            _activeLicense = new SoftwareLicense(
                licenseId,
                product,
                edition,
                licensedTo,
                hardwareBinding,
                issuedAt,
                expiresAt,
                features
            );

            // 3. Check expiration
            if (_activeLicense.IsExpired(DateTimeOffset.UtcNow))
            {
                _statusDescription = "Licence Expirée";
                _validationDetails = $"Licence '{licenseId}' expirée depuis le {expiresAt:dd/MM/yyyy HH:mm}.";
                return;
            }

            // 4. Check hardware binding against current workstation
            string targetMachine = workstationCommonName ?? _workstationProvider.CommonName ?? "";
            if (!_activeLicense.IsHardwareBoundTo(targetMachine))
            {
                _statusDescription = "Poste Non Concordant";
                _validationDetails = $"Licence liée au matériel '{hardwareBinding}', mais ce poste est '{targetMachine}'. Accès refusé.";
                return;
            }

            _statusDescription = "Licence Active (Signée & Liée)";
            _validationDetails = $"Produit : {product} ({edition})\nTitulaire : {licensedTo}\nLiaison : {hardwareBinding}\nExpiration : {expiresAt:dd/MM/yyyy}\nÉmetteur : {signerCert.Subject}";
        }
        catch (Exception ex)
        {
            SetInvalid($"Erreur lors de la vérification de licence : {ex.Message}");
        }
    }

    private void SetInvalid(string message)
    {
        _activeLicense = null;
        _signatureValid = false;
        _statusDescription = "Licence Invalide";
        _validationDetails = message;
    }

    private static (string? licensePath, X509Certificate2? signerCert) ResolveLicenseAndSigner()
    {
        var candidateDirs = new[]
        {
            AppDomain.CurrentDomain.BaseDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WorldPinball", "Aegis"),
            @"D:\Workspace\GoProject\wp-tools"
        };

        string? foundLic = null;
        X509Certificate2? foundSigner = null;

        foreach (var dir in candidateDirs)
        {
            var licPath = Path.Combine(dir, "aegis.lic");
            if (foundLic is null && File.Exists(licPath))
            {
                foundLic = licPath;
            }

            var certPath = Path.Combine(dir, "software-signing.crt");
            if (foundSigner is null && File.Exists(certPath))
            {
                try
                {
                    foundSigner = X509CertificateLoader.LoadCertificateFromFile(certPath);
                }
                catch { }
            }
        }

        return (foundLic, foundSigner);
    }
}
