using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Safar.Application.Assistant;
using Safar.Application.Common;

namespace Safar.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AssistantTests(SafarApiFactory factory)
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = SafarApiFactory.Json;

    [Fact]
    public async Task Without_an_AI_key_the_rule_based_planner_answers()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/assistant/plan",
            new TripPlanRequest("A relaxing beach holiday in the Maldives"), Json);

        response.EnsureSuccessStatusCode();
        var plan = (await response.Content.ReadFromJsonAsync<TripPlanResponse>(Json))!;
        Assert.Equal("rules", plan.Source);
        Assert.Equal("maldives-island-escape", plan.Suggestions[0].Slug);
        Assert.NotNull(plan.Suggestions[0].SuggestedDeparture);
    }

    [Fact]
    public async Task AI_answer_is_used_but_invented_trips_are_removed()
    {
        var fake = new FakeModel("""
            {"reply":"Two great options for you.","suggestions":[
              {"slug":"atlantis-underwater-hotel","departureDate":"2027-01-01","reason":"Does not exist"},
              {"slug":"georgia-mountains-tbilisi","departureDate":"1999-01-01","reason":"Cool mountain air"}]}
            """);
        var client = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<ILanguageModel>(fake)))
            .CreateClient();

        var plan = (await (await client.PostAsJsonAsync("/api/assistant/plan",
            new TripPlanRequest("somewhere cool with mountains"), Json)).Content.ReadFromJsonAsync<TripPlanResponse>(Json))!;

        Assert.Equal("ai", plan.Source);
        Assert.Equal("Two great options for you.", plan.Reply);
        var only = Assert.Single(plan.Suggestions);
        Assert.Equal("georgia-mountains-tbilisi", only.Slug);
        Assert.True(only.SuggestedDeparture > DateOnly.FromDateTime(DateTime.UtcNow)); // bad date replaced with a real one
        Assert.Contains("georgia-mountains-tbilisi |", fake.LastSystemPrompt);
    }

    [Fact]
    public async Task Broken_AI_response_falls_back_to_rules()
    {
        var client = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
                s.AddSingleton<ILanguageModel>(new FakeModel("this is not json"))))
            .CreateClient();

        var plan = (await (await client.PostAsJsonAsync("/api/assistant/plan",
            new TripPlanRequest("desert safari"), Json)).Content.ReadFromJsonAsync<TripPlanResponse>(Json))!;

        Assert.Equal("rules", plan.Source);
        Assert.NotEmpty(plan.Suggestions);
    }

    [Fact]
    public async Task Empty_message_is_rejected()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/assistant/plan", new TripPlanRequest(""), Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class FakeModel(string reply) : ILanguageModel
    {
        public string? LastSystemPrompt { get; private set; }
        public bool IsConfigured => true;

        public Task<string?> CompleteJsonAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
        {
            LastSystemPrompt = systemPrompt;
            return Task.FromResult<string?>(reply);
        }
    }
}
