using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Safar.Application.Assistant;

namespace Safar.Api.Controllers;

/// <summary>AI trip planner - describe a trip in plain words, get matching departures.</summary>
[ApiController]
[Route("api/assistant")]
[EnableRateLimiting("assistant")] // AI calls cost money: limit per IP
public class AssistantController(ITripAssistant assistant) : ControllerBase
{
    /// <summary>
    /// e.g. { "message": "4 days somewhere cool in December, under QAR 3,500 each for a couple" }.
    /// Suggestions always come from the live catalogue; "source" says whether the AI model or the rule-based planner answered.
    /// </summary>
    [HttpPost("plan")]
    public async Task<ActionResult<TripPlanResponse>> Plan(TripPlanRequest request, CancellationToken ct) =>
        Ok(await assistant.PlanAsync(request, ct));
}
