using CidadeEmDia.Domain.Identity;
using CidadeEmDia.Domain.Occurrences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace CidadeEmDia.Infrastructure.Persistence.Configurations;

internal sealed class OccurrenceConfiguration : IEntityTypeConfiguration<Occurrence>
{
    private static readonly GeometryFactory GeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public void Configure(EntityTypeBuilder<Occurrence> builder)
    {
        var statusConverter = new ValueConverter<OccurrenceStatus, string>(
            status => status.Value,
            value => OccurrenceStatus.From(value));
        var publicCodeConverter = new ValueConverter<OccurrencePublicCode, string>(
            publicCode => publicCode.Value,
            value => OccurrencePublicCode.From(value));
        var locationConverter = new ValueConverter<OccurrenceLocation, Point>(
            location => ToPoint(location),
            point => FromPoint(point));

        builder.ToTable("occurrences");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.PublicCode)
            .HasColumnName("public_code")
            .HasConversion(publicCodeConverter)
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.AuthorUserId)
            .HasColumnName("author_user_id")
            .IsRequired();
        builder.Property(x => x.CategoryId)
            .HasColumnName("category_id")
            .IsRequired();
        builder.Property(x => x.ExternalProtocolNumber)
            .HasColumnName("external_protocol_number")
            .HasMaxLength(160);
        builder.Property(x => x.ExternalProtocolAgency)
            .HasColumnName("external_protocol_agency")
            .HasMaxLength(200);
        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(240)
            .IsRequired();
        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion(statusConverter)
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.PostalCode)
            .HasColumnName("postal_code")
            .HasMaxLength(8);
        builder.Property(x => x.AddressText)
            .HasColumnName("address_text")
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(x => x.CityId)
            .HasColumnName("city_id");
        builder.Property(x => x.StateCode)
            .HasColumnName("state_code")
            .HasMaxLength(2);
        builder.Property(x => x.Location)
            .HasColumnName("location")
            .HasConversion(locationConverter)
            .HasColumnType("geography (point, 4326)")
            .IsRequired();
        builder.Property(x => x.ClosedAt)
            .HasColumnName("closed_at");
        builder.Property(x => x.CancelledAt)
            .HasColumnName("cancelled_at");
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => x.PublicCode).IsUnique();
        builder.HasIndex(x => x.AuthorUserId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CategoryId);
        builder.HasIndex(x => x.CityId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.Location)
            .HasDatabaseName("ix_occurrences_location_gist")
            .HasMethod("gist");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<OccurrenceCategory>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        ConfigureStatusHistory(builder, statusConverter);
        ConfigureComplements(builder);
        ConfigureServiceForecastHistory(builder);
    }

    private static void ConfigureStatusHistory(
        EntityTypeBuilder<Occurrence> builder,
        ValueConverter<OccurrenceStatus, string> statusConverter)
    {
        builder.OwnsMany(x => x.StatusHistory, history =>
        {
            history.ToTable("occurrence_status_history");
            history.WithOwner().HasForeignKey("OccurrenceId");
            history.HasKey(x => x.Id);

            history.Property(x => x.Id).HasColumnName("id");
            history.Property<Guid>("OccurrenceId").HasColumnName("occurrence_id");
            history.Property(x => x.FromStatus)
                .HasColumnName("from_status")
                .HasConversion(statusConverter)
                .HasMaxLength(32);
            history.Property(x => x.ToStatus)
                .HasColumnName("to_status")
                .HasConversion(statusConverter)
                .HasMaxLength(32)
                .IsRequired();
            history.Property(x => x.ChangedByUserId)
                .HasColumnName("changed_by_user_id")
                .IsRequired();
            history.Property(x => x.Reason)
                .HasColumnName("reason")
                .HasMaxLength(1000);
            history.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            history.HasIndex("OccurrenceId", nameof(OccurrenceStatusChange.CreatedAt));
            history.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Navigation(x => x.StatusHistory)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureComplements(EntityTypeBuilder<Occurrence> builder)
    {
        builder.OwnsMany(x => x.Complements, complement =>
        {
            complement.ToTable("occurrence_complements");
            complement.WithOwner().HasForeignKey("OccurrenceId");
            complement.HasKey(x => x.Id);

            complement.Property(x => x.Id).HasColumnName("id");
            complement.Property<Guid>("OccurrenceId").HasColumnName("occurrence_id");
            complement.Property(x => x.AuthorUserId)
                .HasColumnName("author_user_id")
                .IsRequired();
            complement.Property(x => x.Content)
                .HasColumnName("content")
                .HasColumnType("text")
                .IsRequired();
            complement.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            complement.HasIndex("OccurrenceId", nameof(OccurrenceComplement.CreatedAt));
            complement.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Navigation(x => x.Complements)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureServiceForecastHistory(EntityTypeBuilder<Occurrence> builder)
    {
        builder.OwnsMany(x => x.ServiceForecastHistory, forecast =>
        {
            forecast.ToTable("occurrence_service_forecasts");
            forecast.WithOwner().HasForeignKey("OccurrenceId");
            forecast.HasKey(x => x.Id);

            forecast.Property(x => x.Id).HasColumnName("id");
            forecast.Property<Guid>("OccurrenceId").HasColumnName("occurrence_id");
            forecast.Property(x => x.EstimatedFor)
                .HasColumnName("estimated_for")
                .IsRequired();
            forecast.Property(x => x.DefinedByUserId)
                .HasColumnName("defined_by_user_id")
                .IsRequired();
            forecast.Property(x => x.DefinedAt)
                .HasColumnName("defined_at")
                .IsRequired();
            forecast.Property(x => x.Note)
                .HasColumnName("note")
                .HasMaxLength(1000);

            forecast.HasIndex("OccurrenceId", nameof(OccurrenceServiceForecast.DefinedAt));
            forecast.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.DefinedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Navigation(x => x.ServiceForecastHistory)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static Point ToPoint(OccurrenceLocation location) =>
        GeometryFactory.CreatePoint(new Coordinate(
            (double)location.Longitude,
            (double)location.Latitude));

    private static OccurrenceLocation FromPoint(Point point) =>
        new((decimal)point.Y, (decimal)point.X);
}
