using System.Globalization;
using Microsoft.Extensions.Options;

namespace Infrastructure.Configuration;

public sealed class ObservedInventoryDailyCaptureOptions
{
    public const string Section = "ObservedInventoryDailyCapture";

    // The scheduler stays inert unless an operator explicitly opts in.
    public bool Enabled { get; set; }
    public string RunAtLocalTime { get; set; } = "23:55";
    public string TimeZoneId { get; set; } = "Europe/Belgrade";
}

public sealed class ObservedInventoryDailyCaptureOptionsValidator : IValidateOptions<ObservedInventoryDailyCaptureOptions>
{
    public ValidateOptionsResult Validate(string? name, ObservedInventoryDailyCaptureOptions options)
    {
        if (!TimeSpan.TryParseExact(options.RunAtLocalTime?.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var runAt) ||
            runAt < TimeSpan.Zero || runAt >= TimeSpan.FromDays(1))
        {
            return ValidateOptionsResult.Fail("ObservedInventoryDailyCapture:RunAtLocalTime must use HH:mm format.");
        }

        if (string.IsNullOrWhiteSpace(options.TimeZoneId))
        {
            return ValidateOptionsResult.Fail("ObservedInventoryDailyCapture:TimeZoneId is required.");
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return ValidateOptionsResult.Fail("ObservedInventoryDailyCapture:TimeZoneId must name an installed time zone.");
        }
        catch (InvalidTimeZoneException)
        {
            return ValidateOptionsResult.Fail("ObservedInventoryDailyCapture:TimeZoneId is invalid.");
        }

        return ValidateOptionsResult.Success;
    }
}
