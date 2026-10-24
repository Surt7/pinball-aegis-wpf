namespace Pinball_Aegis.Application.Constants;

// Centralized API routes and endpoints configuration
public static class ApiRoutes
{
    // Base enterprise domain defined in Nginx
    public const string BaseAddress = "https://api.worldpinball.local";

    // Machine Hardware & Attestation endpoints
    public static class Attestation
    {
        public const string Verify = "/api/v1/hardware/attestation/verify";
        public const string MachineDetails = "/api/v1/hardware/machines";
    }

    // Operator Authentication & Token verification endpoints
    public static class Operator
    {
        public const string Authenticate = "/api/v1/operator/authenticate";
        public const string ValidateSession = "/api/v1/operator/session/validate";
    }

    // Fleet Management endpoints
    public static class Fleet
    {
        public const string Overview = "/api/v1/fleet/overview";
        public const string Machines = "/api/v1/fleet/machines";
    }
    
    // Keycloak Identity Provider endpoints
    public static class Keycloak
    {
        public const string Authority = "http://localhost:8081/realms/worldpinball";
        public const string TokenEndpoint = "http://localhost:8081/realms/worldpinball/protocol/openid-connect/token";
        public const string ClientId = "admin-cli";
    }
}