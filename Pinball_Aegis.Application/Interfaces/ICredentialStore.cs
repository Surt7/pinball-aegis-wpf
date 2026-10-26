namespace Pinball_Aegis.Application.Interfaces;
// Secure storage for credentials backed by Windows Credential Manager
public interface ICredentialStore
{
    // Saves a generic credential into the Windows Vault
    void Save(string targetName, string username, string secret);
    // Retrieves username and secret from the Windows Vault
    (string? Username, string? Secret) Get(string targetName);
    // Removes the credential from the Windows Vault
    void Delete(string targetName);
}