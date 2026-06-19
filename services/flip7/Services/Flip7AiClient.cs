using System.Text;
using System.Text.Json;

namespace Flip7Server.Services;

/// <summary>
/// Calls the Python AI service's <c>/flip7/ai-move</c> for a Hit/Stay decision.
/// On any failure it falls back to a conservative local heuristic so the game
/// never stalls on the AI service being unavailable.
/// </summary>
public class Flip7AiClient : IFlip7AiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<Flip7AiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public Flip7AiClient(IHttpClientFactory factory, ILogger<Flip7AiClient> logger)
    {
        _http = factory.CreateClient("Flip7Ai");
        _logger = logger;
    }

    public async Task<string> DecideHitOrStayAsync(Flip7AiMoveRequest request, CancellationToken ct = default)
    {
        try
        {
            var body = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync("/flip7/ai-move", body, ct);
            resp.EnsureSuccessStatusCode();

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var result = await JsonSerializer.DeserializeAsync<AiMoveResult>(stream, JsonOptions, ct);
            var action = result?.Action?.ToLowerInvariant();
            if (action is "hit" or "stay")
                return action;

            _logger.LogWarning("Flip7 AI service returned an unusable action; using local fallback.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Flip7 AI service call failed; using local fallback.");
        }

        return LocalFallback(request);
    }

    /// <summary>
    /// Local EV-ish heuristic mirroring the Python fallback: a held Second Chance
    /// or an empty line means hit; otherwise stay once duplicate-draw risk crosses
    /// a style-tuned threshold.
    /// </summary>
    private static string LocalFallback(Flip7AiMoveRequest r)
    {
        bool canStay = r.MyNumbers.Count > 0 || r.MyModifiers.Count > 0;
        if (!canStay) return "hit";
        if (r.HasSecondChance) return "hit";
        if (r.DrawPileCount <= 0) return "stay";

        int duplicates = r.MyNumbers.Sum(v => r.DeckRemaining.TryGetValue(v, out var c) ? c : 0);
        double pBust = Math.Clamp((double)duplicates / r.DrawPileCount, 0, 1);

        if (r.MyNumbers.Count == 6) return pBust < 0.85 ? "hit" : "stay";

        double threshold = r.Style switch
        {
            "safe" => 0.28,
            "risky" => 0.58,
            _ => 0.42,
        };
        return pBust >= threshold ? "stay" : "hit";
    }

    private sealed record AiMoveResult
    {
        public string? Action { get; init; }
        public string? Source { get; init; }
        public double BustProbability { get; init; }
    }
}
