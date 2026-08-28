using CidadeEmDia.Domain.Identity;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class SubaccountInvitationTests
{
    [Fact]
    public void Invitation_is_active_until_accepted()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = new SubaccountInvitation(
            Guid.NewGuid(),
            "sub@example.com",
            "TOKENHASH",
            "[]",
            now.AddHours(48));

        Assert.True(invitation.IsActive(now));

        invitation.Accept(now.AddMinutes(1));

        Assert.False(invitation.IsActive(now.AddMinutes(1)));
        Assert.NotNull(invitation.AcceptedAt);
    }

    [Fact]
    public void Revoked_invitation_is_not_active()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = new SubaccountInvitation(
            Guid.NewGuid(),
            "sub@example.com",
            "TOKENHASH",
            "[]",
            now.AddHours(48));

        invitation.Revoke(now.AddMinutes(1));

        Assert.False(invitation.IsActive(now.AddMinutes(1)));
        Assert.NotNull(invitation.RevokedAt);
    }

    [Fact]
    public void Expired_invitation_is_not_active()
    {
        var now = DateTimeOffset.UtcNow;
        var invitation = new SubaccountInvitation(
            Guid.NewGuid(),
            "sub@example.com",
            "TOKENHASH",
            "[]",
            now.AddMinutes(-1));

        Assert.False(invitation.IsActive(now));
    }
}
