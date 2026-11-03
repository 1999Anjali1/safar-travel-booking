using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Safar.Application.Admin;
using Safar.Application.Bookings;
using Safar.Application.Common;

namespace Safar.Api.Controllers;

/// <summary>Back-office endpoints for travel agents and admins.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = "Staff")]
public class AdminController(
    IAdminDashboardService dashboard,
    IAdminCatalogService catalog,
    IAdminBookingService adminBookings) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> Dashboard(CancellationToken ct) => Ok(await dashboard.GetAsync(ct));

    // ---------- packages ----------

    [HttpGet("packages")]
    public async Task<ActionResult<IReadOnlyList<AdminPackageListItemDto>>> Packages(CancellationToken ct) =>
        Ok(await catalog.GetPackagesAsync(ct));

    [HttpGet("packages/{id:int}")]
    public async Task<ActionResult<AdminPackageDetailDto>> Package(int id, CancellationToken ct) =>
        Ok(await catalog.GetPackageAsync(id, ct));

    [HttpPost("packages")]
    public async Task<ActionResult<AdminPackageDetailDto>> CreatePackage(UpsertPackageRequest request, CancellationToken ct)
    {
        var created = await catalog.CreatePackageAsync(request, ct);
        return CreatedAtAction(nameof(Package), new { id = created.Id }, created);
    }

    [HttpPut("packages/{id:int}")]
    public async Task<ActionResult<AdminPackageDetailDto>> UpdatePackage(int id, UpsertPackageRequest request, CancellationToken ct) =>
        Ok(await catalog.UpdatePackageAsync(id, request, ct));

    [HttpPost("packages/{id:int}/publish")]
    public async Task<ActionResult<AdminPackageDetailDto>> Publish(int id, CancellationToken ct) =>
        Ok(await catalog.SetPublishedAsync(id, true, ct));

    [HttpPost("packages/{id:int}/unpublish")]
    public async Task<ActionResult<AdminPackageDetailDto>> Unpublish(int id, CancellationToken ct) =>
        Ok(await catalog.SetPublishedAsync(id, false, ct));

    // ---------- departures ----------

    [HttpPost("packages/{id:int}/departures")]
    public async Task<ActionResult<AdminPackageDetailDto>> AddDeparture(int id, AddDepartureRequest request, CancellationToken ct) =>
        Ok(await catalog.AddDepartureAsync(id, request, ct));

    /// <summary>Cancel a departure - every booking on it is cancelled with a full refund.</summary>
    [HttpPost("departures/{id:int}/cancel")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminPackageDetailDto>> CancelDeparture(int id, CancelDepartureRequest request, CancellationToken ct) =>
        Ok(await catalog.CancelDepartureAsync(id, request, ct));

    /// <summary>Passenger manifest (confirmed travellers) for a departure.</summary>
    [HttpGet("departures/{id:int}/manifest")]
    public async Task<ActionResult<DepartureManifestDto>> Manifest(int id, CancellationToken ct) =>
        Ok(await catalog.GetManifestAsync(id, ct));

    // ---------- bookings ----------

    [HttpGet("bookings")]
    public async Task<ActionResult<PagedResult<BookingListItemDto>>> Bookings([FromQuery] AdminBookingQuery query, CancellationToken ct) =>
        Ok(await adminBookings.SearchAsync(query, ct));

    // ---------- promo codes (admin only) ----------

    [HttpGet("promo-codes")]
    public async Task<ActionResult<IReadOnlyList<PromoCodeDto>>> PromoCodes(CancellationToken ct) =>
        Ok(await adminBookings.GetPromoCodesAsync(ct));

    [HttpPost("promo-codes")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PromoCodeDto>> CreatePromoCode(CreatePromoCodeRequest request, CancellationToken ct) =>
        Ok(await adminBookings.CreatePromoCodeAsync(request, ct));

    [HttpPost("promo-codes/{id:int}/activate")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PromoCodeDto>> ActivatePromo(int id, CancellationToken ct) =>
        Ok(await adminBookings.SetPromoActiveAsync(id, true, ct));

    [HttpPost("promo-codes/{id:int}/deactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PromoCodeDto>> DeactivatePromo(int id, CancellationToken ct) =>
        Ok(await adminBookings.SetPromoActiveAsync(id, false, ct));
}
