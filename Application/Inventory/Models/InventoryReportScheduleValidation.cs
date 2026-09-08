using System.Globalization;
using System.Net.Mail;

namespace Application.Inventory.Models;

public static class InventoryReportScheduleValidator
{
    private static readonly string[] SupportedFrequencies = ["daily", "weekly"];
    private static readonly string[] SupportedFormats = ["pdf", "xlsx", "csv"];
    private static readonly string[] SupportedOrientations = ["portrait", "landscape"];

    public static IReadOnlyDictionary<string, string[]> Validate(InventoryReportScheduleUpsertRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            Add(errors, nameof(request.Name), "Naziv rasporeda je obavezan.");
        }
        else if (name.Length > 200)
        {
            Add(errors, nameof(request.Name), "Naziv rasporeda ne sme biti duži od 200 karaktera.");
        }

        ValidateRecipients(request.RecipientsCsv, errors);
        ValidateTime(request.RunAtLocalTime, errors);
        ValidateTimeZone(request.TimeZoneId, errors);

        var frequency = request.Frequency?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!SupportedFrequencies.Contains(frequency, StringComparer.Ordinal))
        {
            Add(errors, nameof(request.Frequency), "Frekvencija mora biti daily ili weekly.");
        }

        if (request.DayOfWeek is < 1 or > 7)
        {
            Add(errors, nameof(request.DayOfWeek), "Dan u nedelji mora biti između 1 i 7.");
        }
        else if (frequency == "weekly" && request.DayOfWeek is null)
        {
            Add(errors, nameof(request.DayOfWeek), "Dan u nedelji je obavezan za nedeljni raspored.");
        }

        var format = request.Format?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!SupportedFormats.Contains(format, StringComparer.Ordinal))
        {
            Add(errors, nameof(request.Format), "Format mora biti pdf, xlsx ili csv.");
        }

        var orientation = request.Orientation?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!SupportedOrientations.Contains(orientation, StringComparer.Ordinal))
        {
            Add(errors, nameof(request.Orientation), "Orijentacija mora biti portrait ili landscape.");
        }

        ValidateLength(request.Subject, nameof(request.Subject), 250, errors);
        ValidateLength(request.Search, nameof(request.Search), 250, errors);
        ValidateLength(request.SortBy, nameof(request.SortBy), 50, errors);

        return errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    public static void EnsureValid(InventoryReportScheduleUpsertRequest request)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            throw new InventoryReportScheduleValidationException(errors);
        }
    }

    private static void ValidateRecipients(
        string? recipientsCsv,
        IDictionary<string, List<string>> errors)
    {
        var value = recipientsCsv?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            Add(errors, nameof(InventoryReportScheduleUpsertRequest.RecipientsCsv), "Unesite najmanje jednog primaoca.");
            return;
        }

        if (value.Length > 2_000)
        {
            Add(errors, nameof(InventoryReportScheduleUpsertRequest.RecipientsCsv), "Lista primalaca ne sme biti duža od 2000 karaktera.");
        }

        var recipients = value.Split([',', ';', '\r', '\n'], StringSplitOptions.None);
        if (recipients.Any(recipient => recipient.Trim().Length == 0))
        {
            Add(errors, nameof(InventoryReportScheduleUpsertRequest.RecipientsCsv), "Lista primalaca sadrži praznu stavku.");
        }

        foreach (var candidate in recipients.Select(recipient => recipient.Trim()).Where(recipient => recipient.Length > 0))
        {
            try
            {
                var address = new MailAddress(candidate);
                if (!string.Equals(address.Address, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    Add(errors, nameof(InventoryReportScheduleUpsertRequest.RecipientsCsv), $"Primalac '{candidate}' nije validna email adresa.");
                }
            }
            catch (FormatException)
            {
                Add(errors, nameof(InventoryReportScheduleUpsertRequest.RecipientsCsv), $"Primalac '{candidate}' nije validna email adresa.");
            }
        }
    }

    private static void ValidateTime(string? runAtLocalTime, IDictionary<string, List<string>> errors)
    {
        var value = runAtLocalTime?.Trim() ?? string.Empty;
        if (!TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out _))
        {
            Add(errors, nameof(InventoryReportScheduleUpsertRequest.RunAtLocalTime), "Vreme mora biti u formatu HH:mm.");
        }
    }

    private static void ValidateTimeZone(string? timeZoneId, IDictionary<string, List<string>> errors)
    {
        var value = timeZoneId?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            Add(errors, nameof(InventoryReportScheduleUpsertRequest.TimeZoneId), "Vremenska zona je obavezna.");
            return;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(value);
        }
        catch (TimeZoneNotFoundException)
        {
            Add(errors, nameof(InventoryReportScheduleUpsertRequest.TimeZoneId), "Vremenska zona nije pronađena.");
        }
        catch (InvalidTimeZoneException)
        {
            Add(errors, nameof(InventoryReportScheduleUpsertRequest.TimeZoneId), "Vremenska zona nije validna.");
        }
    }

    private static void ValidateLength(
        string? value,
        string field,
        int maxLength,
        IDictionary<string, List<string>> errors)
    {
        if (value is not null && value.Trim().Length > maxLength)
        {
            Add(errors, field, $"Vrednost ne sme biti duža od {maxLength} karaktera.");
        }
    }

    private static void Add(IDictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = [];
            errors[field] = messages;
        }

        messages.Add(message);
    }
}

public sealed class InventoryReportScheduleValidationException : Exception
{
    public InventoryReportScheduleValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Inventory report schedule validation failed.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
