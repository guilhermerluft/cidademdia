using CidadeEmDia.Api.Authorization;

namespace CidadeEmDia.Api.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin")
            .RequireAuthorization(AuthorizationPolicies.AdminAccess);

        admin.MapGet("/status", () => Results.Ok(new
        {
            access = "admin",
            utc = DateTimeOffset.UtcNow
        }));

        return api;
    }
}
