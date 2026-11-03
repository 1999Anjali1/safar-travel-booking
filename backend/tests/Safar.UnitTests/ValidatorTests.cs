using Safar.Application.Admin;
using Safar.Application.Auth;
using Safar.Application.Bookings;

namespace Safar.UnitTests;

public class ValidatorTests
{
    private static CreateBookingRequest ValidBooking(params TravellerDto[] travellers) => new(
        DepartureId: 1,
        Travellers: travellers.Length > 0 ? travellers : [new TravellerDto("Anjali Nair", "Indian", "N1234567", null)],
        ContactEmail: "anjali@example.com",
        ContactPhone: "+974 5512 3456",
        PromoCode: null,
        SpecialRequests: null);

    [Fact]
    public void Valid_booking_request_passes()
    {
        var result = new CreateBookingRequestValidator().Validate(ValidBooking());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Booking_requires_contact_details_and_travellers()
    {
        var request = ValidBooking() with { ContactEmail = "not-an-email", ContactPhone = "12", Travellers = [] };

        var result = new CreateBookingRequestValidator().Validate(request);

        Assert.Contains(result.Errors, e => e.PropertyName == "ContactEmail");
        Assert.Contains(result.Errors, e => e.PropertyName == "ContactPhone");
        Assert.Contains(result.Errors, e => e.PropertyName == "Travellers");
    }

    [Fact]
    public void Traveller_passport_must_be_alphanumeric()
    {
        var request = ValidBooking(new TravellerDto("Anjali Nair", "Indian", "N12-345!", null));

        var result = new CreateBookingRequestValidator().Validate(request);

        Assert.Contains(result.Errors, e => e.PropertyName.Contains("PassportNumber"));
    }

    [Fact]
    public void Traveller_birth_date_cannot_be_in_the_future()
    {
        var request = ValidBooking(new TravellerDto("Baby Nair", "Indian", null, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5)));

        var result = new CreateBookingRequestValidator().Validate(request);

        Assert.Contains(result.Errors, e => e.PropertyName.Contains("DateOfBirth"));
    }

    [Theory]
    [InlineData("short1A")]
    [InlineData("alllowercase1")]
    [InlineData("NoNumbersHere")]
    public void Weak_passwords_are_rejected(string password)
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest("Test User", "t@example.com", password, null));

        Assert.Contains(result.Errors, e => e.PropertyName == "Password");
    }

    [Fact]
    public void Package_image_url_must_be_https()
    {
        var request = new UpsertPackageRequest("Doha by night", "Summary", "Description", "Doha", "Qatar",
            Domain.Enums.PackageCategory.City, 1, 150, "http://insecure.example.com/a.jpg", [], []);

        var result = new UpsertPackageRequestValidator().Validate(request);

        Assert.Contains(result.Errors, e => e.PropertyName == "ImageUrl");
    }

    [Theory]
    [InlineData("Doha City Highlights", "doha-city-highlights")]
    [InlineData("  Georgia: Tbilisi & the Caucasus ", "georgia-tbilisi-the-caucasus")]
    [InlineData("Maldives -- 5 Days!!", "maldives-5-days")]
    public void Slugs_are_url_friendly(string title, string expected)
    {
        Assert.Equal(expected, AdminCatalogService.Slugify(title));
    }
}
