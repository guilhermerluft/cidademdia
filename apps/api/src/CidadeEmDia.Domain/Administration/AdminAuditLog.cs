using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Administration;

public static class AdminAuditActionKeys
{
    public const string UserStatusChanged = "USER_STATUS_CHANGED";
}

public static class AdminAuditEntityKeys
{
    public const string User = "USER";
}

public sealed class AdminAuditLog : BaseEntity
{
    private AdminAuditLog() { }

    public AdminAuditLog(
        Guid actorUserId,
        string action,
        string entityType,
        Guid? entityId,
        string? previousValue,
        string? newValue,
        string reason,
        DateTimeOffset occurredAt)
    {
        if (actorUserId == Guid.Empty)
            throw new DomainException("admin_audit_actor_required");

        ActorUserId = actorUserId;
        Action = NormalizeRequired(action, 80, "admin_audit_action_required", "admin_audit_action_too_long")
            .ToUpperInvariant();
        EntityType = NormalizeRequired(entityType, 80, "admin_audit_entity_type_required", "admin_audit_entity_type_too_long")
            .ToUpperInvariant();
        EntityId = entityId;
        PreviousValue = NormalizeOptional(previousValue, 120, "admin_audit_previous_value_too_long");
        NewValue = NormalizeOptional(newValue, 120, "admin_audit_new_value_too_long");
        Reason = NormalizeRequired(reason, 500, "admin_audit_reason_required", "admin_audit_reason_too_long");
        OccurredAt = occurredAt;
    }

    public Guid ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public string? PreviousValue { get; private set; }
    public string? NewValue { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }

    private static string NormalizeRequired(
        string? value,
        int maxLength,
        string requiredCode,
        string tooLongCode)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new DomainException(requiredCode);
        if (normalized.Length > maxLength)
            throw new DomainException(tooLongCode);
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string tooLongCode)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        if (normalized.Length > maxLength)
            throw new DomainException(tooLongCode);
        return normalized;
    }
}
