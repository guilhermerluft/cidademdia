using CidadeEmDia.Domain.Administration;
using CidadeEmDia.Domain.Common;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class AdministrationDomainTests
{
    [Fact]
    public void Admin_audit_log_normalizes_action_entity_and_reason()
    {
        var actorId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        var log = new AdminAuditLog(
            actorId,
            " user_status_changed ",
            " user ",
            entityId,
            "Active",
            "Suspended",
            "  Solicitação operacional aprovada.  ",
            occurredAt);

        Assert.Equal(actorId, log.ActorUserId);
        Assert.Equal(AdminAuditActionKeys.UserStatusChanged, log.Action);
        Assert.Equal(AdminAuditEntityKeys.User, log.EntityType);
        Assert.Equal(entityId, log.EntityId);
        Assert.Equal("Active", log.PreviousValue);
        Assert.Equal("Suspended", log.NewValue);
        Assert.Equal("Solicitação operacional aprovada.", log.Reason);
        Assert.Equal(occurredAt, log.OccurredAt);
    }

    [Fact]
    public void Admin_audit_log_requires_reason()
    {
        var exception = Assert.Throws<DomainException>(() => new AdminAuditLog(
            Guid.NewGuid(),
            AdminAuditActionKeys.UserStatusChanged,
            AdminAuditEntityKeys.User,
            Guid.NewGuid(),
            "Active",
            "Blocked",
            " ",
            DateTimeOffset.UtcNow));

        Assert.Equal("admin_audit_reason_required", exception.Message);
    }

    [Fact]
    public void Admin_audit_log_limits_reason_length()
    {
        var exception = Assert.Throws<DomainException>(() => new AdminAuditLog(
            Guid.NewGuid(),
            AdminAuditActionKeys.UserStatusChanged,
            AdminAuditEntityKeys.User,
            Guid.NewGuid(),
            "Active",
            "Blocked",
            new string('x', 501),
            DateTimeOffset.UtcNow));

        Assert.Equal("admin_audit_reason_too_long", exception.Message);
    }
}
