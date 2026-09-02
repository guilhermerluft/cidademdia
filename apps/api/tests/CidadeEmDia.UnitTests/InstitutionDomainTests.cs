using CidadeEmDia.Domain.Common;
using CidadeEmDia.Domain.Institutions;
using Xunit;

namespace CidadeEmDia.UnitTests;

public sealed class InstitutionDomainTests
{
    [Fact]
    public void Representative_can_exist_without_account()
    {
        var representative = new InstitutionRepresentative(
            Guid.NewGuid(),
            "Maria da Silva",
            "maria-da-silva",
            "Vereadora",
            "maria@camara.gov.br",
            null,
            null,
            null,
            0);

        Assert.Null(representative.AccountId);
        Assert.Equal(RepresentativeProfileStatusKeys.NotRegistered, representative.ProfileStatus);
    }

    [Fact]
    public void Claim_links_existing_representative_without_recreating_profile()
    {
        var representative = new InstitutionRepresentative(
            Guid.NewGuid(),
            "João Souza",
            "joao-souza",
            "Deputado",
            null,
            null,
            null,
            null,
            1);
        var originalId = representative.Id;
        var accountId = Guid.NewGuid();

        representative.MarkInvited();
        representative.Claim(accountId);

        Assert.Equal(originalId, representative.Id);
        Assert.Equal(accountId, representative.AccountId);
        Assert.Equal(RepresentativeProfileStatusKeys.Active, representative.ProfileStatus);
    }

    [Fact]
    public void Claim_cannot_move_representative_to_another_account()
    {
        var representative = new InstitutionRepresentative(
            Guid.NewGuid(),
            "João Souza",
            "joao-souza",
            "Deputado",
            null,
            null,
            null,
            null,
            1);

        representative.Claim(Guid.NewGuid());

        var exception = Assert.Throws<DomainException>(() =>
            representative.Claim(Guid.NewGuid()));

        Assert.Equal("representative_already_claimed", exception.Message);
    }

    [Theory]
    [InlineData(InstitutionTypeKeys.CityHall)]
    [InlineData(InstitutionTypeKeys.CityCouncil)]
    [InlineData(InstitutionTypeKeys.Assembly)]
    [InlineData(InstitutionTypeKeys.PublicAgency)]
    [InlineData(InstitutionTypeKeys.PublicService)]
    [InlineData(InstitutionTypeKeys.Other)]
    public void Institution_accepts_supported_types(string type)
    {
        var institution = new Institution(
            "Instituição Teste",
            $"inst-{type.ToLowerInvariant().Replace('_', '-')}",
            type,
            InstitutionScopeLevelKeys.Municipal,
            null,
            null,
            null,
            null,
            null,
            "SP");

        Assert.Equal(type, institution.Type);
        Assert.Equal(InstitutionStatusKeys.Active, institution.Status);
    }

    [Fact]
    public void Institution_normalizes_cnpj_email_and_state()
    {
        var institution = new Institution(
            "Prefeitura Teste",
            "prefeitura-teste",
            InstitutionTypeKeys.CityHall,
            InstitutionScopeLevelKeys.Municipal,
            "12.345.678/0001-90",
            " CONTATO@EXAMPLE.GOV.BR ",
            "EXAMPLE.GOV.BR",
            null,
            null,
            "sp");

        Assert.Equal("12345678000190", institution.Cnpj);
        Assert.Equal("contato@example.gov.br", institution.OfficialEmail);
        Assert.Equal("example.gov.br", institution.OfficialDomain);
        Assert.Equal("SP", institution.StateCode);
    }

    [Fact]
    public void City_jurisdiction_requires_city_id()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new InstitutionJurisdiction(
                Guid.NewGuid(),
                InstitutionJurisdictionTypeKeys.City,
                null,
                "SP",
                null));

        Assert.Equal("institution_jurisdiction_city_required", exception.Message);
    }

    [Fact]
    public void Representative_membership_requires_representative_id()
    {
        var exception = Assert.Throws<DomainException>(() =>
            new InstitutionMembership(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                InstitutionMembershipRoleKeys.Representative,
                DateTimeOffset.UtcNow));

        Assert.Equal("membership_representative_required", exception.Message);
    }

    [Fact]
    public void Invite_expires_and_cannot_be_used_after_expiration()
    {
        var now = new DateTimeOffset(2026, 9, 2, 2, 30, 0, TimeSpan.Zero);
        var invite = new InstitutionInvite(
            Guid.NewGuid(),
            null,
            "representante@example.gov.br",
            new string('a', 64),
            Guid.NewGuid(),
            now.AddHours(1),
            now);

        invite.MarkExpired(now.AddHours(2));

        Assert.Equal(InstitutionInviteStatusKeys.Expired, invite.Status);
        Assert.False(invite.IsUsable(now.AddHours(2)));
    }
}
