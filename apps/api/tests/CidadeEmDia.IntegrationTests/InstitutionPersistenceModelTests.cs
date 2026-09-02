using CidadeEmDia.Domain.Institutions;
using CidadeEmDia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CidadeEmDia.IntegrationTests;

public sealed class InstitutionPersistenceModelTests
{
    [Fact]
    public void Institution_model_uses_expected_tables()
    {
        using var context = CreateContext();

        Assert.Equal("institutions", context.Model.FindEntityType(typeof(Institution))!.GetTableName());
        Assert.Equal("institution_jurisdictions", context.Model.FindEntityType(typeof(InstitutionJurisdiction))!.GetTableName());
        Assert.Equal("representatives", context.Model.FindEntityType(typeof(InstitutionRepresentative))!.GetTableName());
        Assert.Equal("institution_memberships", context.Model.FindEntityType(typeof(InstitutionMembership))!.GetTableName());
        Assert.Equal("institution_invites", context.Model.FindEntityType(typeof(InstitutionInvite))!.GetTableName());
    }

    [Fact]
    public void Institution_model_has_identity_and_claim_uniqueness()
    {
        using var context = CreateContext();

        var institution = context.Model.FindEntityType(typeof(Institution))!;
        var representative = context.Model.FindEntityType(typeof(InstitutionRepresentative))!;
        var membership = context.Model.FindEntityType(typeof(InstitutionMembership))!;
        var invite = context.Model.FindEntityType(typeof(InstitutionInvite))!;

        Assert.Contains(
            institution.GetIndexes(),
            index => index.IsUnique
                && index.GetDatabaseName() == "ux_institutions_slug"
                && HasProperties(index, nameof(Institution.Slug)));

        Assert.Contains(
            representative.GetIndexes(),
            index => index.IsUnique
                && index.GetDatabaseName() == "ux_representatives_account"
                && HasProperties(index, nameof(InstitutionRepresentative.AccountId)));

        Assert.Contains(
            membership.GetIndexes(),
            index => index.IsUnique
                && index.GetDatabaseName() == "ux_institution_membership_user_role"
                && HasProperties(
                    index,
                    nameof(InstitutionMembership.InstitutionId),
                    nameof(InstitutionMembership.UserId),
                    nameof(InstitutionMembership.MembershipRole)));

        Assert.Contains(
            invite.GetIndexes(),
            index => index.IsUnique
                && index.GetDatabaseName() == "ux_institution_invites_token_hash"
                && HasProperties(index, nameof(InstitutionInvite.TokenHash)));
    }

    [Fact]
    public void Institution_children_cascade_but_user_links_are_restricted()
    {
        using var context = CreateContext();

        var jurisdiction = context.Model.FindEntityType(typeof(InstitutionJurisdiction))!;
        var representative = context.Model.FindEntityType(typeof(InstitutionRepresentative))!;
        var membership = context.Model.FindEntityType(typeof(InstitutionMembership))!;
        var invite = context.Model.FindEntityType(typeof(InstitutionInvite))!;

        var jurisdictionInstitution = Assert.Single(
            jurisdiction.GetForeignKeys(),
            fk => fk.Properties.Any(p => p.Name == nameof(InstitutionJurisdiction.InstitutionId)));
        var representativeInstitution = Assert.Single(
            representative.GetForeignKeys(),
            fk => fk.Properties.Any(p => p.Name == nameof(InstitutionRepresentative.InstitutionId)));
        var representativeAccount = Assert.Single(
            representative.GetForeignKeys(),
            fk => fk.Properties.Any(p => p.Name == nameof(InstitutionRepresentative.AccountId)));
        var membershipUser = Assert.Single(
            membership.GetForeignKeys(),
            fk => fk.Properties.Any(p => p.Name == nameof(InstitutionMembership.UserId)));
        var inviteCreator = Assert.Single(
            invite.GetForeignKeys(),
            fk => fk.Properties.Any(p => p.Name == nameof(InstitutionInvite.CreatedByUserId)));

        Assert.Equal(DeleteBehavior.Cascade, jurisdictionInstitution.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, representativeInstitution.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, representativeAccount.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, membershipUser.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, inviteCreator.DeleteBehavior);
    }

    [Fact]
    public void Institution_directory_query_is_translatable_by_npgsql()
    {
        using var context = CreateContext();

        const string normalized = "prefeitura";
        const string state = "SP";

        var query = context.Institutions
            .AsNoTracking()
            .Where(x => x.Status == InstitutionStatusKeys.Active)
            .Where(x =>
                x.Name.ToLower().Contains(normalized)
                || x.Slug.Contains(normalized)
                || x.Representatives.Any(r => r.Name.ToLower().Contains(normalized)))
            .Where(x =>
                x.StateCode == state
                || x.Jurisdictions.Any(j => j.StateCode == state))
            .OrderBy(x => x.Name)
            .Take(20);

        var sql = query.ToQueryString();

        Assert.Contains("institutions", sql);
        Assert.Contains("representatives", sql);
        Assert.Contains("institution_jurisdictions", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("LIMIT", sql);
    }

    private static bool HasProperties(
        Microsoft.EntityFrameworkCore.Metadata.IReadOnlyIndex index,
        params string[] propertyNames) =>
        index.Properties.Select(property => property.Name)
            .SequenceEqual(propertyNames);

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=cidademdia_model_tests;Username=postgres;Password=postgres",
                npgsql => npgsql.UseNetTopologySuite())
            .Options;

        return new AppDbContext(options);
    }
}
