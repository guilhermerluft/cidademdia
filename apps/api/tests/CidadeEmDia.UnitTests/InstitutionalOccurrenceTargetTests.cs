using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Occurrences;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class InstitutionalOccurrenceTargetTests
{
    [Fact]
    public void Institutional_target_is_owned_by_master_and_keeps_optional_addressee()
    {
        var occurrence = CreateOccurrence();
        var masterUserId = Guid.NewGuid();
        var sentAt = occurrence.CreatedAt.AddMinutes(1);

        var target = occurrence.AddInstitutionalMasterTarget(
            masterUserId,
            "  Vereador Exemplo / Partido Exemplo  ",
            sentAt);

        Assert.Equal(masterUserId, target.MasterUserId);
        Assert.Equal("Vereador Exemplo / Partido Exemplo", target.Addressee);
        Assert.Equal(OccurrenceTargetStatus.Pending, target.Status);
    }

    [Fact]
    public void Institutional_target_allows_empty_addressee()
    {
        var occurrence = CreateOccurrence();

        var target = occurrence.AddInstitutionalMasterTarget(
            Guid.NewGuid(),
            "   ",
            occurrence.CreatedAt.AddMinutes(1));

        Assert.Null(target.Addressee);
    }

    [Fact]
    public void Institutional_target_can_only_be_decided_by_assigned_master()
    {
        var occurrence = CreateOccurrence();
        var masterUserId = Guid.NewGuid();
        var sentAt = occurrence.CreatedAt.AddMinutes(1);
        var target = occurrence.AddInstitutionalMasterTarget(masterUserId, null, sentAt);

        Assert.Throws<DomainException>(() =>
            occurrence.AcceptMasterTarget(
                target.Id,
                Guid.NewGuid(),
                sentAt.AddMinutes(1)));

        var accepted = occurrence.AcceptMasterTarget(
            target.Id,
            masterUserId,
            sentAt.AddMinutes(1));

        Assert.Equal(OccurrenceTargetStatus.Accepted, accepted.Status);
        Assert.Equal(OccurrenceStatus.Received, occurrence.Status);
    }

    [Fact]
    public void Duplicate_institutional_master_destination_is_blocked()
    {
        var occurrence = CreateOccurrence();
        var masterUserId = Guid.NewGuid();
        var sentAt = occurrence.CreatedAt.AddMinutes(1);

        occurrence.AddInstitutionalMasterTarget(masterUserId, null, sentAt);

        Assert.Throws<DomainException>(() =>
            occurrence.AddInstitutionalMasterTarget(
                masterUserId,
                "Outro destinatário",
                sentAt.AddMinutes(1)));
    }

    [Fact]
    public void Institutional_addressee_has_a_bounded_length()
    {
        var occurrence = CreateOccurrence();

        Assert.Throws<DomainException>(() =>
            occurrence.AddInstitutionalMasterTarget(
                Guid.NewGuid(),
                new string('a', OccurrenceTarget.MaxAddresseeLength + 1),
                occurrence.CreatedAt.AddMinutes(1)));
    }

    private static Occurrence CreateOccurrence() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Buraco na via",
            "Próximo ao cruzamento.",
            "Rua A, 100",
            new OccurrenceLocation(-30.0346m, -51.2177m),
            postalCode: "90010000",
            stateCode: "RS");
}
