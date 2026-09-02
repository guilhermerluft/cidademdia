using System.Security.Claims;
using System.Text;
using CidadeEmDia.Api.Authorization;
using CidadeEmDia.Api.Endpoints;
using CidadeEmDia.Api.Hubs;
using CidadeEmDia.Api.Middleware;
using CidadeEmDia.Application;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Infrastructure;
using CidadeEmDia.Infrastructure.Identity;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var jwtOptions = JwtOptions.FromConfiguration(builder.Configuration);
var allowedOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? "http://localhost:8080,http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"].ToString();
                if (!string.IsNullOrWhiteSpace(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(userIdValue, out var userId))
                {
                    context.Fail("user_id_invalid");
                    return;
                }

                var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var canUseSession = await dbContext.Users
                    .AsNoTracking()
                    .AnyAsync(
                        user => user.Id == userId
                            && user.Status != UserStatus.Suspended
                            && user.Status != UserStatus.Blocked,
                        context.HttpContext.RequestAborted);

                if (!canUseSession)
                    context.Fail("user_inactive");
            }
        };
    });
builder.Services.AddCidadeEmDiaAuthorization();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("postgres", tags: ["ready"]);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseCors("web");
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var api = app.MapGroup("/api/v1");
api.MapGet("/status", () => Results.Ok(new
{
    service = "CidadeEmDia.Api",
    version = "v1",
    utc = DateTimeOffset.UtcNow
}));
api.MapAuthEndpoints();
api.MapProfileEndpoints();
api.MapAdminEndpoints();
api.MapSubaccountEndpoints();
api.MapInstitutionEndpoints();
api.MapBillingEndpoints();
api.MapMercadoPagoWebhookEndpoints();
api.MapOccurrenceEndpoints();
api.MapOccurrenceTargetDecisionEndpoints();
api.MapOccurrenceLifecycleEndpoints();
api.MapOccurrenceFollowUpEndpoints();
api.MapOccurrenceSupportEndpoints();
api.MapOccurrenceMediaEndpoints();
api.MapOccurrenceAssignmentEndpoints();
api.MapChatEndpoints();
api.MapContentEndpoints();

app.MapHub<ChatHub>("/hubs/chat")
    .RequireAuthorization();

app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();

public partial class Program;
