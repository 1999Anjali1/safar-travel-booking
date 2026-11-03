using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Safar.Application.Admin;
using Safar.Application.Auth;
using Safar.Application.Bookings;
using Safar.Application.Common;
using Safar.Application.Packages;
using Safar.Domain.Enums;
using Safar.Infrastructure.BackgroundJobs;
using Safar.Infrastructure.Persistence;

namespace Safar.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CatalogueTests(SafarApiFactory factory)
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = SafarApiFactory.Json;

    [Fact]
    public async Task Health_endpoint_reports_database_is_reachable()
    {
        var response = await factory.CreateClient().GetStringAsync("/health");
        Assert.Equal("Healthy", response);
    }

    [Fact]
    public async Task Search_returns_only_published_packages()
    {
        var page = await factory.CreateClient()
            .GetFromJsonAsync<PagedResult<PackageCardDto>>("/api/packages?pageSize=48", Json);

        Assert.NotNull(page);
        Assert.Equal(8, page.TotalCount);
        Assert.DoesNotContain(page.Items, p => p.Slug == "salalah-khareef"); // draft package
    }

    [Fact]
    public async Task Search_filters_by_text_and_category()
    {
        var client = factory.CreateClient();

        var georgia = await client.GetFromJsonAsync<PagedResult<PackageCardDto>>("/api/packages?search=tbilisi", Json);
        var desert = await client.GetFromJsonAsync<PagedResult<PackageCardDto>>("/api/packages?category=Desert", Json);

        Assert.Equal("georgia-mountains-tbilisi", Assert.Single(georgia!.Items).Slug);
        Assert.All(desert!.Items, p => Assert.Equal(PackageCategory.Desert, p.Category));
        Assert.Equal(2, desert.TotalCount);
    }

    [Fact]
    public async Task Unpublished_package_detail_is_not_found()
    {
        var response = await factory.CreateClient().GetAsync("/api/packages/salalah-khareef");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Package_detail_lists_upcoming_departures_with_seats()
    {
        var detail = await factory.CreateClient().GetFromJsonAsync<PackageDetailDto>("/api/packages/maldives-island-escape", Json);

        Assert.NotNull(detail);
        Assert.NotEmpty(detail.Departures);
        Assert.All(detail.Departures, d => Assert.True(d.StartDate > DateOnly.FromDateTime(DateTime.UtcNow)));
        Assert.Contains(detail.Departures, d => d.IsBookable);
    }
}

[Collection(ApiCollection.Name)]
public class AuthTests(SafarApiFactory factory)
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = SafarApiFactory.Json;

    [Fact]
    public async Task Register_then_access_profile()
    {
        var client = factory.CreateClient();
        var email = $"new{Guid.NewGuid():N}"[..12] + "@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("New Customer", email, "Travel2026!", "+974 5500 7777"), Json);
        register.EnsureSuccessStatusCode();
        var auth = (await register.Content.ReadFromJsonAsync<AuthResponse>(Json))!;

        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        var me = await client.GetFromJsonAsync<UserDto>("/api/auth/me", Json);

        Assert.Equal(email, me!.Email);
        Assert.Equal(UserRole.Customer, me.Role);
    }

    [Fact]
    public async Task Duplicate_email_returns_conflict()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Someone", "customer@safar.qa", "Travel2026!", null), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_returns_401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest("customer@safar.qa", "wrong-password"), Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_token_rotates_and_old_token_cannot_be_reused()
    {
        var client = factory.CreateClient();
        var login = await SafarApiFactory.LoginAsync(client, "agent@safar.qa");

        var first = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login.RefreshToken), Json);
        var reused = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login.RefreshToken), Json);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [Fact]
    public async Task Customers_cannot_open_the_admin_api()
    {
        var customer = await factory.ClientForAsync("customer@safar.qa");

        var response = await customer.GetAsync("/api/admin/dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

[Collection(ApiCollection.Name)]
public class BookingFlowTests(SafarApiFactory factory)
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = SafarApiFactory.Json;

    private async Task<int> CreateDepartureAsync(int capacity, int daysAhead = 45)
    {
        var admin = await factory.ClientForAsync("admin@safar.qa");
        var response = await admin.PostAsJsonAsync("/api/admin/packages/1/departures",
            new AddDepartureRequest(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(daysAhead + Random.Shared.Next(0, 200)), capacity, null), Json);
        response.EnsureSuccessStatusCode();
        var package = (await response.Content.ReadFromJsonAsync<AdminPackageDetailDto>(Json))!;
        return package.Departures.OrderByDescending(d => d.Id).First().Id;
    }

    private static CreateBookingRequest Request(int departureId, int seats, string? promo = null) => new(
        departureId,
        Enumerable.Range(1, seats).Select(i => new TravellerDto($"Traveller {i}", "Indian", $"P{i}000000", null)).ToList(),
        "customer@safar.qa",
        "+974 5512 3456",
        promo,
        null);

    [Fact]
    public async Task Quote_create_pay_and_cancel()
    {
        var departureId = await CreateDepartureAsync(capacity: 20);
        var customer = await factory.ClientForAsync("customer@safar.qa");

        // quote with promo
        var quote = await (await customer.PostAsJsonAsync("/api/bookings/quote", new QuoteRequest(departureId, 2, "welcome10"), Json))
            .Content.ReadFromJsonAsync<QuoteDto>(Json);
        Assert.Equal(360, quote!.Subtotal);   // Doha City Highlights: 180 x 2
        Assert.Equal(36, quote.Discount);
        Assert.Equal(324, quote.Total);

        // create - seats are held
        var created = await customer.PostAsJsonAsync("/api/bookings", Request(departureId, 2, "WELCOME10"), Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var booking = (await created.Content.ReadFromJsonAsync<BookingDto>(Json))!;
        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.True(booking.CanPay);
        Assert.StartsWith("•", booking.Travellers[0].PassportNumber); // passport masked in responses

        // pay
        var paid = await (await customer.PostAsJsonAsync($"/api/bookings/{booking.Reference}/pay", new PayRequest("Card"), Json))
            .Content.ReadFromJsonAsync<BookingDto>(Json);
        Assert.Equal(BookingStatus.Confirmed, paid!.Status);
        Assert.Single(paid.Payments, p => p.Status == PaymentStatus.Succeeded && p.Amount == 324);

        // appears in "my bookings"
        var mine = await customer.GetFromJsonAsync<PagedResult<BookingListItemDto>>("/api/bookings/mine?pageSize=50", Json);
        Assert.Contains(mine!.Items, b => b.Reference == booking.Reference);

        // 45+ days before departure → 90 % refund
        var cancelQuote = await customer.GetFromJsonAsync<CancellationQuoteDto>($"/api/bookings/{booking.Reference}/cancellation-quote", Json);
        Assert.Equal(90, cancelQuote!.RefundPercent);

        var cancelled = await (await customer.PostAsJsonAsync($"/api/bookings/{booking.Reference}/cancel", new CancelBookingRequest("Plans changed"), Json))
            .Content.ReadFromJsonAsync<BookingDto>(Json);
        Assert.Equal(BookingStatus.Cancelled, cancelled!.Status);
        Assert.Equal(291.6m, cancelled.RefundAmount);
    }

    [Fact]
    public async Task Concurrent_bookings_never_oversell_a_departure()
    {
        const int capacity = 3;
        var departureId = await CreateDepartureAsync(capacity);
        var customer = await factory.ClientForAsync("customer@safar.qa");

        // 8 customers try to grab 1 seat each at the same moment
        var attempts = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => customer.PostAsJsonAsync("/api/bookings", Request(departureId, 1), Json)));

        Assert.Equal(capacity, attempts.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(attempts.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var departure = await db.Departures.AsNoTracking().SingleAsync(d => d.Id == departureId);
        Assert.Equal(capacity, departure.SeatsBooked);
    }

    [Fact]
    public async Task Unpaid_booking_expires_and_releases_seats()
    {
        var departureId = await CreateDepartureAsync(capacity: 5);
        var customer = await factory.ClientForAsync("customer@safar.qa");
        var booking = (await (await customer.PostAsJsonAsync("/api/bookings", Request(departureId, 2), Json))
            .Content.ReadFromJsonAsync<BookingDto>(Json))!;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // pretend the 15-minute hold has passed, then run the background job once
        await db.Bookings.Where(b => b.Reference == booking.Reference)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        await factory.Services.GetRequiredService<ExpireUnpaidBookingsJob>().RunOnceAsync(CancellationToken.None);

        var after = await customer.GetFromJsonAsync<BookingDto>($"/api/bookings/{booking.Reference}", Json);
        Assert.Equal(BookingStatus.Expired, after!.Status);
        Assert.Equal(0, (await db.Departures.AsNoTracking().SingleAsync(d => d.Id == departureId)).SeatsBooked);

        var pay = await customer.PostAsJsonAsync($"/api/bookings/{booking.Reference}/pay", new PayRequest("Card"), Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, pay.StatusCode);
    }

    [Fact]
    public async Task Customers_cannot_see_each_others_bookings()
    {
        var departureId = await CreateDepartureAsync(capacity: 5);
        var owner = await factory.ClientForAsync("customer@safar.qa");
        var booking = (await (await owner.PostAsJsonAsync("/api/bookings", Request(departureId, 1), Json))
            .Content.ReadFromJsonAsync<BookingDto>(Json))!;

        var other = factory.CreateClient();
        var auth = await (await other.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Other Person", $"o{Guid.NewGuid():N}"[..12] + "@example.com", "Travel2026!", null), Json))
            .Content.ReadFromJsonAsync<AuthResponse>(Json);
        other.DefaultRequestHeaders.Authorization = new("Bearer", auth!.AccessToken);

        var response = await other.GetAsync($"/api/bookings/{booking.Reference}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_cancelling_a_departure_fully_refunds_every_booking()
    {
        var departureId = await CreateDepartureAsync(capacity: 10, daysAhead: 4); // inside the "no refund" window
        var customer = await factory.ClientForAsync("customer@safar.qa");
        var booking = (await (await customer.PostAsJsonAsync("/api/bookings", Request(departureId, 2), Json))
            .Content.ReadFromJsonAsync<BookingDto>(Json))!;
        await customer.PostAsJsonAsync($"/api/bookings/{booking.Reference}/pay", new PayRequest("ApplePay"), Json);

        var admin = await factory.ClientForAsync("admin@safar.qa");
        var response = await admin.PostAsJsonAsync($"/api/admin/departures/{departureId}/cancel",
            new CancelDepartureRequest("Sandstorm warning"), Json);
        response.EnsureSuccessStatusCode();

        var after = await customer.GetFromJsonAsync<BookingDto>($"/api/bookings/{booking.Reference}", Json);
        Assert.Equal(BookingStatus.Cancelled, after!.Status);
        Assert.Equal(after.Total, after.RefundAmount);
    }

    [Fact]
    public async Task Invalid_booking_request_returns_field_errors()
    {
        var customer = await factory.ClientForAsync("customer@safar.qa");

        var response = await customer.PostAsJsonAsync("/api/bookings",
            new CreateBookingRequest(0, [], "not-an-email", "x", null, null), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ContactEmail", body);
        Assert.Contains("Travellers", body);
    }

    [Fact]
    public async Task Dashboard_returns_six_months_of_revenue()
    {
        var admin = await factory.ClientForAsync("agent@safar.qa");

        var dashboard = await admin.GetFromJsonAsync<DashboardDto>("/api/admin/dashboard", Json);

        Assert.Equal(6, dashboard!.RevenueByMonth.Count);
        Assert.True(dashboard.RevenueByMonth.Sum(m => m.Revenue) > 0);
        Assert.NotEmpty(dashboard.TopPackages);
        Assert.NotEmpty(dashboard.RecentBookings);
    }
}
