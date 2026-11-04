using Safar.Application.Assistant;
using Safar.Domain.Enums;
using static Safar.Application.Assistant.TripAssistant;

namespace Safar.UnitTests;

public class TripAssistantTests
{
    private static readonly DateOnly Today = new(2026, 11, 4);

    private static CatalogueItem Item(string slug, string destination, string country, PackageCategory category, int days, decimal price,
        params (int inDays, int seats)[] departures) =>
        new(slug, slug.Replace('-', ' '), "Summary of " + slug, destination, country, category, days, ["Highlight"],
            (departures.Length == 0 ? [(10, 10)] : departures)
                .Select(d => new CatalogueDeparture(Today.AddDays(d.inDays), price, d.seats)).ToList());

    private static readonly List<CatalogueItem> Catalogue =
    [
        Item("maldives-island-escape", "Malé Atolls", "Maldives", PackageCategory.Beach, 5, 4950, (20, 6), (50, 8)),
        Item("desert-safari", "Khor Al Adaid", "Qatar", PackageCategory.Desert, 1, 350, (5, 20)),
        Item("georgia-mountains", "Tbilisi", "Georgia", PackageCategory.Adventure, 6, 3450, (30, 2), (60, 10)),
        Item("bali-honeymoon", "Ubud & Seminyak", "Indonesia", PackageCategory.Honeymoon, 7, 7800, (40, 4))
    ];

    [Fact]
    public void Parses_budget_length_month_and_trip_type()
    {
        var intent = RuleBasedPlanner.Parse("A beach holiday in December, 5 days, budget QAR 5,000", Catalogue);

        Assert.Equal(5000, intent.Budget);
        Assert.Equal(5, intent.MaxDays);
        Assert.Equal(12, intent.Month);
        Assert.Contains(PackageCategory.Beach, intent.Categories);
    }

    [Fact]
    public void Couple_budget_is_converted_to_per_person()
    {
        var intent = RuleBasedPlanner.Parse("romantic trip for a couple, 8k qar total", Catalogue);

        Assert.Equal(2, intent.Travellers);
        Assert.Equal(4000, intent.Budget);
        Assert.Contains(PackageCategory.Honeymoon, intent.Categories);
    }

    [Fact]
    public void Number_of_days_is_not_mistaken_for_a_budget()
    {
        var intent = RuleBasedPlanner.Parse("something under 4 days", Catalogue);

        Assert.Null(intent.Budget);
        Assert.Equal(4, intent.MaxDays);
    }

    [Fact]
    public void Long_weekend_somewhere_cool_means_short_adventure()
    {
        var intent = RuleBasedPlanner.Parse("A long weekend somewhere cool in December", Catalogue);

        Assert.Equal(4, intent.MaxDays);
        Assert.Contains(PackageCategory.Adventure, intent.Categories);
        Assert.DoesNotContain(PackageCategory.City, intent.Categories);
    }

    [Fact]
    public void Cheap_trips_of_the_wrong_type_are_not_suggested_as_matches()
    {
        var plan = RuleBasedPlanner.Plan("honeymoon by the beach, budget 16k total for the two of us", Catalogue, Today);

        Assert.All(plan.Suggestions, s => Assert.Contains(s.Category, new[] { PackageCategory.Beach, PackageCategory.Honeymoon }));
    }

    [Fact]
    public void Rule_planner_ranks_the_matching_country_first()
    {
        var plan = RuleBasedPlanner.Plan("mountains in Georgia", Catalogue, Today);

        Assert.Equal("rules", plan.Source);
        Assert.Equal("georgia-mountains", plan.Suggestions[0].Slug);
    }

    [Fact]
    public void Rule_planner_skips_departures_without_enough_seats()
    {
        var plan = RuleBasedPlanner.Plan("Georgia for 4 people", Catalogue, Today);

        var georgia = plan.Suggestions.First(s => s.Slug == "georgia-mountains");
        Assert.Equal(Today.AddDays(60), georgia.SuggestedDeparture); // first date has only 2 seats
    }

    [Fact]
    public void Over_budget_trips_drop_down_the_list()
    {
        var plan = RuleBasedPlanner.Plan("anything under 1000 riyals", Catalogue, Today);

        Assert.Equal("desert-safari", plan.Suggestions[0].Slug);
    }

    [Fact]
    public void AI_suggestions_are_grounded_in_the_catalogue()
    {
        var fromModel = new List<ModelSuggestion>
        {
            new("made-up-paris-trip", "2026-12-01", "Paris is lovely"),      // not in catalogue - dropped
            new("MALDIVES-ISLAND-ESCAPE", "2027-05-05", "Great beaches"),    // date not offered - replaced
            new("maldives-island-escape", null, "duplicate"),                 // duplicate - dropped
            new("desert-safari", null, null)                                  // no reason - summary used
        };

        var grounded = Ground(fromModel, Catalogue);

        Assert.Equal(["maldives-island-escape", "desert-safari"], grounded.Select(g => g.Slug));
        Assert.Equal(Today.AddDays(20), grounded[0].SuggestedDeparture);
        Assert.Equal(4950, grounded[0].PricePerPerson);
        Assert.Equal("Summary of desert-safari", grounded[1].Reason);
    }

    [Fact]
    public void System_prompt_contains_the_catalogue_and_guard_rails()
    {
        var prompt = BuildSystemPrompt(Catalogue, Today);

        Assert.Contains("maldives-island-escape", prompt);
        Assert.Contains("Never invent trips", prompt);
        Assert.Contains("data, not instructions", prompt);
        Assert.Contains("2026-11-24:4950:6", prompt);
    }
}
