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
        public const string Machines = "/api/v1/admin/machines";
    }
    
    // Keycloak Identity Provider endpoints (served through reverse proxy with valid TLS)
    public static class Keycloak
    {
        public const string Authority = "https://auth.worldpinball.local/realms/worldpinball";
        public const string AuthorizationEndpoint = "https://auth.worldpinball.local/realms/worldpinball/protocol/openid-connect/auth";
        public const string TokenEndpoint = "https://auth.worldpinball.local/realms/worldpinball/protocol/openid-connect/token";
        public const string ClientId = "pinball-aegis";
        public const string RedirectUri = "http://127.0.0.1:8282/callback";
    }
}
