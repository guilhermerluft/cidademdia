using System.Security.Claims;
using CidadeEmDia.Application.Authentication;

namespace CidadeEmDia.Api.Endpoints;

public static class AuthEndpoints
{
    private const string RefreshCookieName = "cidademdia_refresh";
    private const string RefreshCookiePath = "/api/v1/auth";

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth");

        auth.MapPost("/register", async (RegisterRequest request, IAuthService authService, HttpContext context, CancellationToken cancellationToken) =>
        {
            var result = await authService.RegisterAsync(request.Email, request.Password, request.DisplayName, cancellationToken);
            if (!result.Succeeded || result.Session is null)
                return MapFailure(result.ErrorCode);

            SetRefreshCookie(context, result.Session.RefreshToken, result.Session.RefreshTokenExpiresAt);
            return Results.Created("/api/v1/auth/me", ToResponse(result.Session));
        });

        auth.MapPost("/login", async (LoginRequest request, IAuthService authService, HttpContext context, CancellationToken cancellationToken) =>
        {
            var result = await authService.LoginAsync(request.Email, request.Password, cancellationToken);
            if (!result.Succeeded || result.Session is null)
                return MapFailure(result.ErrorCode);

            SetRefreshCookie(context, result.Session.RefreshToken, result.Session.RefreshTokenExpiresAt);
            return Results.Ok(ToResponse(result.Session));
        });

        auth.MapPost("/refresh", async (IAuthService authService, HttpContext context, CancellationToken cancellationToken) =>
        {
            context.Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken);
            var result = await authService.RefreshAsync(refreshToken ?? string.Empty, cancellationToken);
            if (!result.Succeeded || result.Session is null)
            {
                DeleteRefreshCookie(context);
                return MapFailure(result.ErrorCode);
            }

            SetRefreshCookie(context, result.Session.RefreshToken, result.Session.RefreshTokenExpiresAt);
            return Results.Ok(ToResponse(result.Session));
        });

        auth.MapPost("/logout", async (IAuthService authService, HttpContext context, CancellationToken cancellationToken) =>
        {
            context.Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken);
            await authService.LogoutAsync(refreshToken, cancellationToken);
            DeleteRefreshCookie(context);
            return Results.NoContent();
        });

        auth.MapPost("/password/forgot", async (ForgotPasswordRequest request, IAuthService authService, CancellationToken cancellationToken) =>
        {
            await authService.RequestPasswordResetAsync(request.Email, cancellationToken);
            return Results.Accepted();
        });

        auth.MapPost("/password/reset", async (ResetPasswordRequest request, IAuthService authService, HttpContext context, CancellationToken cancellationToken) =>
        {
            var result = await authService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken);
            if (!result.Succeeded)
                return MapPasswordResetFailure(result.ErrorCode);

            DeleteRefreshCookie(context);
            return Results.NoContent();
        });

        auth.MapGet("/me", async (IAuthService authService, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
        {
            var rawUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(rawUserId, out var userId))
                return Results.Unauthorized();

            var user = await authService.GetCurrentUserAsync(userId, cancellationToken);
            return user is null ? Results.Unauthorized() : Results.Ok(ToUserResponse(user));
        }).RequireAuthorization();

        return api;
    }

    private static IResult MapFailure(string? errorCode) => errorCode switch
    {
        "invalid_input" => Results.BadRequest(new { error = errorCode }),
        "email_already_registered" => Results.Conflict(new { error = errorCode }),
        "account_unavailable" => Results.Json(new { error = errorCode }, statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Unauthorized()
    };

    private static IResult MapPasswordResetFailure(string? errorCode) => errorCode switch
    {
        "account_unavailable" => Results.Json(new { error = errorCode }, statusCode: StatusCodes.Status403Forbidden),
        "invalid_input" => Results.BadRequest(new { error = errorCode }),
        "invalid_or_expired_reset_token" => Results.BadRequest(new { error = errorCode }),
        _ => Results.BadRequest(new { error = "invalid_password_reset" })
    };

    private static AuthSessionResponse ToResponse(AuthSession session) =>
        new(session.AccessToken, session.AccessTokenExpiresAt, ToUserResponse(session.User));

    private static AuthenticatedUserResponse ToUserResponse(AuthenticatedUser user) =>
        new(user.Id, user.Email, user.DisplayName, user.Roles);

    private static void SetRefreshCookie(HttpContext context, string token, DateTimeOffset expiresAt) =>
        context.Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            Expires = expiresAt,
            IsEssential = true
        });

    private static void DeleteRefreshCookie(HttpContext context) =>
        context.Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath
        });

    public sealed record RegisterRequest(string Email, string Password, string DisplayName);
    public sealed record LoginRequest(string Email, string Password);
    public sealed record ForgotPasswordRequest(string Email);
    public sealed record ResetPasswordRequest(string Token, string NewPassword);
    public sealed record AuthSessionResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, AuthenticatedUserResponse User);
    public sealed record AuthenticatedUserResponse(Guid Id, string Email, string DisplayName, IReadOnlyCollection<string> Roles);
}
