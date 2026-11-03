using Safar.Domain.Common;
using Safar.Domain.Enums;

namespace Safar.Domain.Entities;

/// <summary>A dated trip of a package with limited seats.</summary>
public class Departure
{
    /// <summary>Bookings close this many days before the trip starts.</summary>
    public const int BookingCutOffDays = 2;

    public int Id { get; set; }
    public int TourPackageId { get; set; }
    public TourPackage TourPackage { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public int Capacity { get; set; }
    public int SeatsBooked { get; set; }

    /// <summary>Optional seasonal price; falls back to the package base price.</summary>
    public decimal? PriceOverride { get; set; }

    public DepartureStatus Status { get; set; } = DepartureStatus.Scheduled;

    /// <summary>
    /// Concurrency token (mapped to PostgreSQL's xmin system column).
    /// Stops two customers from booking the last seat at the same time.
    /// </summary>
    public uint Version { get; set; }

    public List<Booking> Bookings { get; set; } = [];

    public int SeatsAvailable => Capacity - SeatsBooked;

    public decimal PricePerPerson => PriceOverride ?? TourPackage.BasePrice;

    public bool IsBookable(DateOnly today) =>
        Status == DepartureStatus.Scheduled
        && StartDate.DayNumber - today.DayNumber >= BookingCutOffDays
        && SeatsAvailable > 0;

    public void ReserveSeats(int seats, DateOnly today)
    {
        if (Status != DepartureStatus.Scheduled)
            throw new DomainException("This departure has been cancelled.");
        if (StartDate.DayNumber - today.DayNumber < BookingCutOffDays)
            throw new DomainException($"Online booking closes {BookingCutOffDays} days before departure.");
        if (seats > SeatsAvailable)
            throw new DomainException(SeatsAvailable == 0
                ? "Sorry, this departure is sold out."
                : $"Only {SeatsAvailable} seat(s) left on this departure.");

        SeatsBooked += seats;
    }

    public void ReleaseSeats(int seats) => SeatsBooked = Math.Max(0, SeatsBooked - seats);
}
