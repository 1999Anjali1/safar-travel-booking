using Safar.Domain.Enums;

namespace Safar.Domain.Entities;

public class TourPackage
{
    public int Id { get; set; }

    /// <summary>URL-friendly id, e.g. "doha-desert-safari".</summary>
    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public PackageCategory Category { get; set; }
    public int DurationDays { get; set; }

    /// <summary>Price per traveller in QAR.</summary>
    public decimal BasePrice { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>Stored as PostgreSQL text[] columns.</summary>
    public List<string> Highlights { get; set; } = [];
    public List<string> Inclusions { get; set; } = [];

    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<Departure> Departures { get; set; } = [];
}
