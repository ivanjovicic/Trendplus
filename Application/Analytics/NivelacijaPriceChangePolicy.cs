namespace Application.Analytics;

public static class NivelacijaPriceChangePolicy
{
    public static decimal? CalculateMarkdownPercent(decimal? oldPrice, decimal newPrice)
    {
        if (!oldPrice.HasValue || oldPrice.Value <= 0m || newPrice >= oldPrice.Value)
        {
            return null;
        }

        return decimal.Round((oldPrice.Value - newPrice) / oldPrice.Value * 100m, 2);
    }

    public static string? Validate(
        decimal? oldPrice,
        decimal newPrice,
        decimal maximumMarkdownPercent,
        bool overrideMaximumMarkdown)
    {
        if (newPrice <= 0m)
        {
            return "nivelacija_new_price_must_be_positive";
        }

        if (oldPrice.HasValue && newPrice == oldPrice.Value)
        {
            return "nivelacija_price_unchanged";
        }

        var markdownPercent = CalculateMarkdownPercent(oldPrice, newPrice);
        if (markdownPercent > maximumMarkdownPercent && !overrideMaximumMarkdown)
        {
            return "nivelacija_markdown_limit_exceeded";
        }

        return null;
    }
}
