using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Safar.Application.Common;
using Safar.Application.Packages;

namespace Safar.Api.Controllers;

/// <summary>Public tour catalogue - no sign-in needed.</summary>
[ApiController]
[Route("api/packages")]
public class PackagesController(IPackageService packages) : ControllerBase
{
    /// <summary>Search published packages. Sort: popular | price-asc | price-desc | duration | newest.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PackageCardDto>>> Search([FromQuery] PackageQuery query, CancellationToken ct) =>
        Ok(await packages.SearchAsync(query, ct));

    /// <summary>Countries, categories and price range for the filter bar (cached for 5 minutes).</summary>
    [HttpGet("filters")]
    [OutputCache(Duration = 300)]
    public async Task<ActionResult<CatalogFiltersDto>> Filters(CancellationToken ct) => Ok(await packages.GetFiltersAsync(ct));

    /// <summary>Package details with upcoming departures and live seat availability.</summary>
    [HttpGet("{slug}")]
    public async Task<ActionResult<PackageDetailDto>> Get(string slug, CancellationToken ct) =>
        Ok(await packages.GetBySlugAsync(slug, ct));
}
