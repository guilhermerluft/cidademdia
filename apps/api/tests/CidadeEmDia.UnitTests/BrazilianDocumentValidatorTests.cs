using CidadeEmDia.Application.Profiles;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class BrazilianDocumentValidatorTests
{
    [Theory]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData("04.252.011/0001-10", "04252011000110")]
    public void TryNormalize_AcceptsValidCpfOrCnpj(string value, string expected)
    {
        var valid = BrazilianDocumentValidator.TryNormalize(value, out var normalized);

        Assert.True(valid);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("111.111.111-11")]
    [InlineData("12.345.678/0001-00")]
    [InlineData("123")]
    public void TryNormalize_RejectsInvalidDocument(string value)
    {
        Assert.False(BrazilianDocumentValidator.TryNormalize(value, out _));
    }

    [Fact]
    public void TryNormalize_AllowsEmptyDocument()
    {
        Assert.True(BrazilianDocumentValidator.TryNormalize(null, out var normalized));
        Assert.Null(normalized);
    }
}
