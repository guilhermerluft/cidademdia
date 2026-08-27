using CidadeEmDia.Infrastructure.Identity;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_and_verify_round_trip_succeeds()
    {
        const string password = "UmaSenhaSegura!123";

        var hash = _hasher.Hash(password);

        Assert.NotEqual(password, hash);
        Assert.True(_hasher.Verify(password, hash));
        Assert.False(_hasher.Verify("senha-errada", hash));
    }

    [Fact]
    public void Verify_rejects_malformed_hash()
    {
        Assert.False(_hasher.Verify("qualquer", "hash-invalido"));
    }
}
