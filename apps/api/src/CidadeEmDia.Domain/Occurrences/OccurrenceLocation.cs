using CidadeEmDia.Domain.Common;

namespace CidadeEmDia.Domain.Occurrences;

public sealed record OccurrenceLocation
{
    public OccurrenceLocation(string address, decimal latitude, decimal longitude)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new DomainException("Occurrence address is required.");
        if (latitude is < -90m or > 90m)
            throw new DomainException("Occurrence latitude must be between -90 and 90.");
        if (longitude is < -180m or > 180m)
            throw new DomainException("Occurrence longitude must be between -180 and 180.");

        Address = address.Trim();
        Latitude = latitude;
        Longitude = longitude;
    }

    public string Address { get; }
    public decimal Latitude { get; }
    public decimal Longitude { get; }
}
