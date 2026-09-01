using System.Security.Claims;
using CidadeEmDia.Api.Authorization;
using CidadeEmDia.Application.Billing;

namespace CidadeEmDia.Api.Endpoints;

public static class BillingEndpoints
{
    public static RouteGroupBuilder MapBillingEndpoints(this RouteGroupBuilder api)
    {
        var billing = api.MapGroup("/billing");

        billing.MapGet("/catalog", async (
            IBillingCatalogService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListCurrentOffersAsync(cancellationToken: cancellationToken)));

        billing.MapGet("/me", async (
            IBillingEntitlementService service,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var masterUserId))
                return Results.Unauthorized();

            var entitlements = await service.GetForMasterAsync(masterUserId, cancellationToken: cancellationToken);
            return entitlements is null
                ? Results.NotFound(new { error = "subscription_not_found" })
                : Results.Ok(entitlements);
        }).RequireAuthorization(AuthorizationPolicies.MasterScope);

        billing.MapPost("/checkout", async (
            BillingCheckoutRequest request,
            IBillingCheckoutService service,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(
                    principal.FindFirstValue(
                        ClaimTypes.NameIdentifier),
                    out var masterUserId))
            {
                return Results.Unauthorized();
            }

            var result =
                await service.CreateCheckoutAsync(
                    masterUserId,
                    request.PlanVersionId,
                    cancellationToken:
                        cancellationToken);

            if (!result.Succeeded)
                return MapCheckoutError(result);

            return Results.Json(
                new
                {
                    subscriptionId =
                        result.SubscriptionId,
                    planVersionId =
                        result.PlanVersionId,
                    checkoutUrl =
                        result.CheckoutUrl,
                    providerStatus =
                        result.ProviderStatus
                },
                statusCode:
                    StatusCodes.Status201Created);
        })
        .RequireAuthorization(
            AuthorizationPolicies.MasterScope);

        billing.MapPut("/plan", async (
            BillingPlanChangeRequest request,
            IBillingSubscriptionManagementService service,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(
                    principal.FindFirstValue(
                        ClaimTypes.NameIdentifier),
                    out var masterUserId))
            {
                return Results.Unauthorized();
            }

            var result =
                await service.SchedulePlanChangeAsync(
                    masterUserId,
                    request.PlanVersionId,
                    cancellationToken:
                        cancellationToken);

            return MapManagementResult(
                result);
        })
        .RequireAuthorization(
            AuthorizationPolicies.MasterScope);

        billing.MapPost("/cancellation", async (
            IBillingSubscriptionManagementService service,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(
                    principal.FindFirstValue(
                        ClaimTypes.NameIdentifier),
                    out var masterUserId))
            {
                return Results.Unauthorized();
            }

            var result =
                await service.RequestCancellationAsync(
                    masterUserId,
                    cancellationToken:
                        cancellationToken);

            return MapManagementResult(
                result);
        })
        .RequireAuthorization(
            AuthorizationPolicies.MasterScope);

        billing.MapPost("/reactivation", async (
            IBillingSubscriptionManagementService service,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(
                    principal.FindFirstValue(
                        ClaimTypes.NameIdentifier),
                    out var masterUserId))
            {
                return Results.Unauthorized();
            }

            var result =
                await service.ReactivateAsync(
                    masterUserId,
                    cancellationToken:
                        cancellationToken);

            return MapManagementResult(
                result);
        })
        .RequireAuthorization(
            AuthorizationPolicies.MasterScope);

        var admin = api.MapGroup("/admin/billing")
            .RequireAuthorization(AuthorizationPolicies.AdminAccess);

        admin.MapPost("/subscriptions", async (
            AdminCreateSubscriptionRequest request,
            IBillingSubscriptionService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.CreatePendingAsync(
                request.MasterUserId,
                request.PlanVersionId,
                request.StartedAt,
                request.CurrentPeriodEnd,
                cancellationToken);

            return MapOperation(result, created: true);
        });

        admin.MapPost("/subscriptions/{masterUserId:guid}/activate", async (
            Guid masterUserId,
            AdminActivateSubscriptionRequest request,
            IBillingSubscriptionService service,
            CancellationToken cancellationToken) =>
            MapOperation(await service.ActivateAsync(
                masterUserId,
                request.SignupFeePaid,
                request.ActivatedAt,
                cancellationToken)));

        admin.MapPost("/subscriptions/{masterUserId:guid}/past-due", async (
            Guid masterUserId,
            AdminMarkPastDueRequest request,
            IBillingSubscriptionService service,
            CancellationToken cancellationToken) =>
            MapOperation(await service.MarkPastDueAsync(masterUserId, request.FailedAt, cancellationToken)));

        admin.MapPut("/subscriptions/{masterUserId:guid}/pending-plan", async (
            Guid masterUserId,
            AdminSchedulePlanChangeRequest request,
            IBillingSubscriptionService service,
            CancellationToken cancellationToken) =>
            MapOperation(await service.ScheduleChangeAsync(masterUserId, request.PlanVersionId, cancellationToken)));

        admin.MapPost("/subscriptions/{masterUserId:guid}/request-cancellation", async (
            Guid masterUserId,
            IBillingSubscriptionService service,
            CancellationToken cancellationToken) =>
            MapOperation(await service.RequestCancellationAsync(masterUserId, cancellationToken)));

        admin.MapPost("/subscriptions/{masterUserId:guid}/renew", async (
            Guid masterUserId,
            AdminRenewSubscriptionRequest request,
            IBillingSubscriptionService service,
            CancellationToken cancellationToken) =>
            MapOperation(await service.ApplyRenewalAsync(
                masterUserId,
                request.PeriodStart,
                request.PeriodEnd,
                cancellationToken)));

        admin.MapPost("/subscriptions/{masterUserId:guid}/cancel", async (
            Guid masterUserId,
            AdminCancelSubscriptionRequest request,
            IBillingSubscriptionService service,
            CancellationToken cancellationToken) =>
            MapOperation(await service.CancelAsync(masterUserId, request.CanceledAt, cancellationToken)));

        return api;
    }

    private static IResult MapManagementResult(
        BillingSubscriptionManagementResult result)
    {
        if (result.Succeeded)
        {
            return Results.Ok(
                new
                {
                    planVersionId =
                        result.PlanVersionId,
                    requiresAuthorization =
                        result.RequiresAuthorization,
                    checkoutUrl =
                        result.CheckoutUrl,
                    effectiveAt =
                        result.EffectiveAt,
                    providerStatus =
                        result.ProviderStatus
                });
        }

        return result.ErrorCode switch
        {
            "invalid_input" =>
                Results.BadRequest(
                    new { error = result.ErrorCode }),

            "master_not_found"
                or "subscription_not_found"
                or "plan_version_not_found"
                or "provider_subscription_not_found" =>
                Results.NotFound(
                    new { error = result.ErrorCode }),

            "subscription_not_active"
                or "plan_version_not_current"
                or "plan_already_active"
                or "cancellation_pending"
                or "plan_change_authorization_pending"
                or "reactivation_not_required"
                or "reactivation_authorization_pending" =>
                Results.Conflict(
                    new { error = result.ErrorCode }),

            "provider_not_configured" =>
                Results.Json(
                    new { error = result.ErrorCode },
                    statusCode:
                        StatusCodes.Status503ServiceUnavailable),

            "provider_rejected"
                or "provider_unavailable"
                or "provider_invalid_response" =>
                Results.Json(
                    new { error = result.ErrorCode },
                    statusCode:
                        StatusCodes.Status502BadGateway),

            "billing_persistence_failed" =>
                Results.Json(
                    new { error = result.ErrorCode },
                    statusCode:
                        StatusCodes.Status500InternalServerError),

            _ =>
                Results.BadRequest(
                    new
                    {
                        error =
                            "billing_management_failed"
                    })
        };
    }

    private static IResult MapCheckoutError(
        BillingCheckoutResult result)
    {
        return result.ErrorCode switch
        {
            "invalid_input" =>
                Results.BadRequest(
                    new { error = result.ErrorCode }),

            "master_not_found"
                or "plan_version_not_found" =>
                Results.NotFound(
                    new { error = result.ErrorCode }),

            "plan_version_not_current"
                or "subscription_already_exists" =>
                Results.Conflict(
                    new { error = result.ErrorCode }),

            "provider_not_configured" =>
                Results.Json(
                    new { error = result.ErrorCode },
                    statusCode:
                        StatusCodes.Status503ServiceUnavailable),

            "provider_rejected"
                or "provider_unavailable"
                or "provider_invalid_response" =>
                Results.Json(
                    new { error = result.ErrorCode },
                    statusCode:
                        StatusCodes.Status502BadGateway),

            "billing_persistence_failed" =>
                Results.Json(
                    new { error = result.ErrorCode },
                    statusCode:
                        StatusCodes.Status500InternalServerError),

            _ =>
                Results.BadRequest(
                    new
                    {
                        error =
                            "billing_checkout_failed"
                    })
        };
    }

    private static IResult MapOperation(BillingSubscriptionOperationResult result, bool created = false)
    {
        if (result.Succeeded)
            return created ? Results.StatusCode(StatusCodes.Status201Created) : Results.Ok();

        return result.ErrorCode switch
        {
            "invalid_input" => Results.BadRequest(new { error = result.ErrorCode }),
            "master_not_found" or "plan_version_not_found" or "subscription_not_found" =>
                Results.NotFound(new { error = result.ErrorCode }),
            "subscription_already_exists" => Results.Conflict(new { error = result.ErrorCode }),
            _ => Results.BadRequest(new { error = "billing_operation_failed" })
        };
    }

    public sealed record BillingCheckoutRequest(
        Guid PlanVersionId);


    public sealed record BillingPlanChangeRequest(
        Guid PlanVersionId);

    public sealed record AdminCreateSubscriptionRequest(
        Guid MasterUserId,
        Guid PlanVersionId,
        DateTimeOffset StartedAt,
        DateTimeOffset CurrentPeriodEnd);

    public sealed record AdminActivateSubscriptionRequest(bool SignupFeePaid, DateTimeOffset ActivatedAt);
    public sealed record AdminMarkPastDueRequest(DateTimeOffset FailedAt);
    public sealed record AdminSchedulePlanChangeRequest(Guid PlanVersionId);
    public sealed record AdminRenewSubscriptionRequest(DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd);
    public sealed record AdminCancelSubscriptionRequest(DateTimeOffset CanceledAt);
}
