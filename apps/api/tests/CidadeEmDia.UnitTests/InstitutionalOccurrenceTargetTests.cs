using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Occurrences;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class InstitutionalOccurrenceTargetTests
{
    [Fact]
    public void Institutional_target_keeps_optional_addressee_without_requiring_master()
    {
        var occurrence = CreateOccurrence();
        var institutionId = Guid.NewGuid();
        var sentAt = occurrence.CreatedAt.AddMinutes(1);

        var target = occurrence.AddInstitutionTarget(
            institutionId,
            "  Vereador Exemplo / Partido Exemplo  ",
            sentAt);

        Assert.Equal(institutionId, target.InstitutionId);
        Assert.Null(target.MasterUserId);
        Assert.Equal("Vereador Exemplo / Partido Exemplo", target.Addressee);
        Assert.Equal(OccurrenceTargetStatus.Pending, target.Status);
    }

    [Fact]
    public void Institutional_target_can_be_claimed_and_accepted_by_master()
    {
        var occurrence = CreateOccurrence();
        var institutionId = Guid.NewGuid();
        var masterUserId = Guid.NewGuid();
        var sentAt = occurrence.CreatedAt.AddMinutes(1);
        var target = occurrence.AddInstitutionTarget(institutionId, null, sentAt);

        var accepted = occurrence.AcceptTarget(
            target.Id,
            masterUserId,
            sentAt.AddMinutes(1));

        Assert.Equal(masterUserId, accepted.MasterUserId);
        Assert.Equal(institutionId, accepted.InstitutionId);
        Assert.Equal(OccurrenceTargetStatus.Accepted, accepted.Status);
        Assert.Equal(OccurrenceStatus.Received, occurrence.Status);
    }

    [Fact]
    public void Institutional_target_cannot_be_claimed_by_another_master_after_decision()
    {
        var occurrence = CreateOccurrence();
        var sentAt = occurrence.CreatedAt.AddMinutes(1);
        var target = occurrence.AddInstitutionTarget(Guid.NewGuid(), null, sentAt);
        var firstMaster = Guid.NewGuid();

        occurrence.AcceptTarget(target.Id, firstMaster, sentAt.AddMinutes(1));

        Assert.Throws<DomainException>(() =>
            occurrence.RejectTarget(
                target.Id,
                Guid.NewGuid(),
                "Tentativa inválida.",
                sentAt.AddMinutes(2)));
    }

    [Fact]
    public void Duplicate_institutional_destination_is_blocked()
    {
        var occurrence = CreateOccurrence();
        var institutionId = Guid.NewGuid();
        var sentAt = occurrence.CreatedAt.AddMinutes(1);

        occurrence.AddInstitutionTarget(institutionId, null, sentAt);

        Assert.Throws<DomainException>(() =>
            occurrence.AddInstitutionTarget(
                institutionId,
                "Outro destinatário",
                sentAt.AddMinutes(1)));
    }

    [Fact]
    public void Institutional_addressee_has_a_bounded_length()
    {
        var occurrence = CreateOccurrence();

        Assert.Throws<DomainException>(() =>
            occurrence.AddInstitutionTarget(
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
