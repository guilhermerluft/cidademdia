using CidadeEmDia.Application.Occurrences;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class OccurrenceApplicationContractsTests
{
    [Fact]
    public void Create_input_does_not_accept_author_identity_from_the_client()
    {
        var propertyNames = typeof(CreateOccurrenceInput)
            .GetProperties()
            .Select(x => x.Name)
            .ToArray();

        Assert.DoesNotContain("AuthorUserId", propertyNames);
        Assert.DoesNotContain("UserId", propertyNames);
    }

    [Fact]
    public void Create_input_keeps_external_protocol_separate_from_public_tracking_code()
    {
        var propertyNames = typeof(CreateOccurrenceInput)
            .GetProperties()
            .Select(x => x.Name)
            .ToArray();

        Assert.Contains("ExternalProtocolNumber", propertyNames);
        Assert.Contains("ExternalProtocolAgency", propertyNames);
        Assert.DoesNotContain("PublicCode", propertyNames);
    }
}
