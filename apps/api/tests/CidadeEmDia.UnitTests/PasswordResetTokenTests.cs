using CidadeEmDia.Domain.Identity;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class PasswordResetTokenTests
{
    [Fact]
    public void Token_is_active_until_consumed()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new PasswordResetToken(Guid.NewGuid(), "ABC123", now.AddMinutes(30));

        Assert.True(token.IsActive(now));

        token.Consume(now.AddMinutes(1));

        Assert.False(token.IsActive(now.AddMinutes(1)));
        Assert.NotNull(token.ConsumedAt);
    }

    [Fact]
    public void Expired_token_is_not_active()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new PasswordResetToken(Guid.NewGuid(), "ABC123", now.AddMinutes(-1));

        Assert.False(token.IsActive(now));
    }
}
