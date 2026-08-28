using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed class OccurrenceCategory : BaseEntity
{
    private OccurrenceCategory()
    {
    }

    public OccurrenceCategory(string name, string slug, int displayOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Occurrence category name is required.");
        if (string.IsNullOrWhiteSpace(slug))
            throw new DomainException("Occurrence category slug is required.");
        if (displayOrder < 0)
            throw new DomainException("Occurrence category display order cannot be negative.");

        Name = name.Trim();
        Slug = NormalizeSlug(slug);
        Status = OccurrenceCategoryStatus.Active;
        DisplayOrder = displayOrder;
    }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public OccurrenceCategoryStatus Status { get; private set; } = OccurrenceCategoryStatus.Active;
    public int DisplayOrder { get; private set; }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Occurrence category name is required.");

        Name = name.Trim();
        Touch();
    }

    public void ChangeDisplayOrder(int displayOrder)
    {
        if (displayOrder < 0)
            throw new DomainException("Occurrence category display order cannot be negative.");

        DisplayOrder = displayOrder;
        Touch();
    }

    public void Deactivate()
    {
        Status = OccurrenceCategoryStatus.Inactive;
        Touch();
    }

    public void Activate()
    {
        Status = OccurrenceCategoryStatus.Active;
        Touch();
    }

    private static string NormalizeSlug(string slug)
    {
        var normalized = slug.Trim().ToLowerInvariant();

        if (normalized.Any(character =>
                !char.IsLetterOrDigit(character)
                && character != '-'))
        {
            throw new DomainException("Occurrence category slug may contain only letters, numbers and hyphens.");
        }

        return normalized;
    }
}
