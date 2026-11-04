using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Safar.Application.Common;

namespace Safar.Infrastructure.Ai;

public class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Any OpenAI-compatible endpoint. Default: Groq.</summary>
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/";

    /// <summary>Leave empty to run without AI (the rule-based planner answers instead).</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "openai/gpt-oss-20b";

    public int TimeoutSeconds { get; set; } = 20;
}

/// <summary>Calls POST {BaseUrl}chat/completions with JSON mode on.</summary>
public class OpenAiCompatibleModel(HttpClient http, IOptions<AiOptions> options, ILogger<OpenAiCompatibleModel> logger)
    : ILanguageModel
{
    private readonly AiOptions _options = options.Value;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<string?> CompleteJsonAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(new ChatRequest(
                _options.Model,
                [new ChatMessage("system", systemPrompt), new ChatMessage("user", userMessage)],
                Temperature: 0.3,
                MaxTokens: 800,
                ResponseFormat: new ResponseFormat("json_object")), options: Json)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var started = DateTime.UtcNow;
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("AI provider returned {Status}: {Body}", (int)response.StatusCode,
                await response.Content.ReadAsStringAsync(ct));
            return null;
        }

        var body = await response.Content.ReadFromJsonAsync<ChatResponse>(Json, ct);
        logger.LogInformation("AI completion with {Model} in {Ms} ms, {Tokens} tokens",
            _options.Model, (int)(DateTime.UtcNow - started).TotalMilliseconds, body?.Usage?.TotalTokens);

        return body?.Choices?.FirstOrDefault()?.Message?.Content;
    }

    private record ChatRequest(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        double Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("response_format")] ResponseFormat ResponseFormat);

    private record ChatMessage(string Role, string Content);

    private record ResponseFormat(string Type);

    private record ChatResponse(List<Choice>? Choices, Usage? Usage);

    private record Choice(ChatMessage? Message);

    private record Usage([property: JsonPropertyName("total_tokens")] int TotalTokens);
}
