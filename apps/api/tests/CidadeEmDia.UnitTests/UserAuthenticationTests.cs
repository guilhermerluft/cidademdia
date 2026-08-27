using CidadeEmDia.Domain.Identity;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class UserAuthenticationTests
{
    [Fact]
    public void Blocked_user_cannot_authenticate()
    {
        var user = new User("pessoa@example.com", "hash");

        user.Block();

        Assert.False(user.CanAuthenticate);
    }

    [Fact]
    public void Active_user_can_authenticate()
    {
        var user = new User("pessoa@example.com", "hash");

        Assert.True(user.CanAuthenticate);
    }
}
