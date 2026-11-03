using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Safar.Application.Bookings;
using Safar.Application.Common;

namespace Safar.Api.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController(IBookingService bookings) : ControllerBase
{
    /// <summary>Price breakdown for a departure, seat count and optional promo code.</summary>
    [HttpPost("quote")]
    [AllowAnonymous]
    public async Task<ActionResult<QuoteDto>> Quote(QuoteRequest request, CancellationToken ct) =>
        Ok(await bookings.QuoteAsync(request, ct));

    /// <summary>Create a booking and hold the seats for 15 minutes while the customer pays.</summary>
    [HttpPost]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingRequest request, CancellationToken ct)
    {
        var booking = await bookings.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { reference = booking.Reference }, booking);
    }

    /// <summary>Bookings of the signed-in customer.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<BookingListItemDto>>> Mine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default) =>
        Ok(await bookings.GetMineAsync(page, Math.Clamp(pageSize, 1, 50), ct));

    [HttpGet("{reference}")]
    public async Task<ActionResult<BookingDto>> Get(string reference, CancellationToken ct) =>
        Ok(await bookings.GetAsync(reference, ct));

    /// <summary>Pay for a held booking (mock payment gateway).</summary>
    [HttpPost("{reference}/pay")]
    public async Task<ActionResult<BookingDto>> Pay(string reference, PayRequest request, CancellationToken ct) =>
        Ok(await bookings.PayAsync(reference, request, ct));

    /// <summary>How much would be refunded if the booking were cancelled today.</summary>
    [HttpGet("{reference}/cancellation-quote")]
    public async Task<ActionResult<CancellationQuoteDto>> CancellationQuote(string reference, CancellationToken ct) =>
        Ok(await bookings.QuoteCancellationAsync(reference, ct));

    [HttpPost("{reference}/cancel")]
    public async Task<ActionResult<BookingDto>> Cancel(string reference, CancelBookingRequest request, CancellationToken ct) =>
        Ok(await bookings.CancelAsync(reference, request, ct));
}
