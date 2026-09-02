using CidadeEmDia.Domain.Administration;
using CidadeEmDia.Domain.Billing;
using CidadeEmDia.Domain.Chat;
using CidadeEmDia.Domain.Content;
using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Domain.Institutions;
using CidadeEmDia.Domain.Occurrences;
using Microsoft.EntityFrameworkCore;

namespace CidadeEmDia.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<MasterSubaccount> MasterSubaccounts => Set<MasterSubaccount>();
    public DbSet<MasterSubaccountPermission> MasterSubaccountPermissions => Set<MasterSubaccountPermission>();
    public DbSet<SubaccountInvitation> SubaccountInvitations => Set<SubaccountInvitation>();
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<InstitutionJurisdiction> InstitutionJurisdictions => Set<InstitutionJurisdiction>();
    public DbSet<InstitutionRepresentative> InstitutionRepresentatives => Set<InstitutionRepresentative>();
    public DbSet<InstitutionMembership> InstitutionMemberships => Set<InstitutionMembership>();
    public DbSet<InstitutionInvite> InstitutionInvites => Set<InstitutionInvite>();
    public DbSet<OccurrenceCategory> OccurrenceCategories => Set<OccurrenceCategory>();
    public DbSet<Occurrence> Occurrences => Set<Occurrence>();
    public DbSet<OccurrenceTarget> OccurrenceTargets => Set<OccurrenceTarget>();
    public DbSet<OccurrenceTargetAssignment> OccurrenceTargetAssignments => Set<OccurrenceTargetAssignment>();
    public DbSet<OccurrenceSupport> OccurrenceSupports => Set<OccurrenceSupport>();
    public DbSet<OccurrenceMedia> OccurrenceMedia => Set<OccurrenceMedia>();
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatParticipant> ChatParticipants => Set<ChatParticipant>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostMedia> PostMedia => Set<PostMedia>();
    public DbSet<PostPlacement> PostPlacements => Set<PostPlacement>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanCategory> PlanCategories => Set<PlanCategory>();
    public DbSet<PlanOffer> PlanOffers => Set<PlanOffer>();
    public DbSet<PlanVersion> PlanVersions => Set<PlanVersion>();
    public DbSet<BillingCustomer> BillingCustomers => Set<BillingCustomer>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<UsageCounter> UsageCounters => Set<UsageCounter>();
    public DbSet<BillingProviderSubscription> BillingProviderSubscriptions => Set<BillingProviderSubscription>();
    public DbSet<BillingPayment> BillingPayments => Set<BillingPayment>();
    public DbSet<BillingPaymentEvent> BillingPaymentEvents => Set<BillingPaymentEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
