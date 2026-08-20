namespace Application.Analytics.DecisionPulse;

/// <summary>
/// Builds a safe Decision Pulse email body: Why + deep link + freshness/DQ only.
/// Never includes secrets or raw customer row payloads.
/// </summary>
public static class DecisionPulseEmailComposer
{
    public static string BuildSubject(int itemCount, DateTime utcNow)
        => $"Trendplus Decision Pulse ({itemCount}) — {utcNow:yyyy-MM-dd} UTC";

    public static string BuildHtmlBody(IReadOnlyList<DecisionPulseItem> items, DateTime utcNow)
    {
        if (items.Count == 0)
        {
            return $"""
                <html><body>
                <p>Decision Pulse nema actionable stavki za {utcNow:yyyy-MM-dd} UTC.</p>
                <p>Prazan rezultat nije greška i nije alert.</p>
                </body></html>
                """;
        }

        var blocks = items.Select(item => $"""
            <li style="margin-bottom:16px;">
              <strong>{Html(item.Title)}</strong>
              <div>Status: {Html(item.RecommendationLabel)} ({Html(item.RecommendationStatus)})</div>
              <div>Zašto: {Html(item.WhySummary)}</div>
              <div>Svežina: {Html(item.InputFreshnessStatus)} · DQ: {Html(item.DataQualityStatus)}</div>
              <div><a href="{Html(item.DeepLink)}">Otvori odluku</a></div>
            </li>
            """);

        return $"""
            <html><body>
            <h2>Decision Pulse</h2>
            <p>Izuzeci iz Product Decision porodice · {utcNow:yyyy-MM-dd} UTC · tenant {Html(DecisionPulseProjector.DedicatedTenantScope)}</p>
            <ol>
            {string.Join(Environment.NewLine, blocks)}
            </ol>
            <p style="color:#666;font-size:12px;">Pulse ne šalje sirove redove, cene, zalihe ni tajne. Slack i DSL nisu u opsegu.</p>
            </body></html>
            """;
    }

    private static string Html(string? value)
        => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}
