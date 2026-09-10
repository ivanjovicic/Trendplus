using System.Globalization;
using Microsoft.Extensions.Options;

namespace Infrastructure.Configuration;

public sealed class NightlyAnalyticsRefreshOptionsValidator : IValidateOptions<NightlyAnalyticsRefreshOptions>
{
    public ValidateOptionsResult Validate(string? name, NightlyAnalyticsRefreshOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("Nightly analytics refresh options are missing.");
        }

        var rawRunAtUtc = options.RunAtUtc?.Trim();
        if (!TimeSpan.TryParseExact(rawRunAtUtc, "hh\\:mm", CultureInfo.InvariantCulture, out var runAtUtc) ||
            runAtUtc < TimeSpan.Zero ||
            runAtUtc >= TimeSpan.FromDays(1))
        {
            return ValidateOptionsResult.Fail(
                "NightlyAnalyticsRefresh:RunAtUtc must be a valid 24-hour UTC time in HH:mm format.");
        }

        return ValidateOptionsResult.Success;
    }
}
