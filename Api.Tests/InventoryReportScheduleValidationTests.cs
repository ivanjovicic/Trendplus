using Application.Inventory.Models;
using Xunit;

namespace Api.Tests;

public sealed class InventoryReportScheduleValidationTests
{
    [Fact]
    public void Accepts_a_valid_daily_schedule()
    {
        var errors = InventoryReportScheduleValidator.Validate(Request());

        Assert.Empty(errors);
    }

    [Fact]
    public void Rejects_empty_and_invalid_recipients()
    {
        var errors = InventoryReportScheduleValidator.Validate(Request(recipientsCsv: "valid@example.com;;not-an-email"));

        var recipientErrors = Assert.Contains(nameof(InventoryReportScheduleUpsertRequest.RecipientsCsv), errors);
        Assert.Contains(recipientErrors, message => message.Contains("praznu stavku", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(recipientErrors, message => message.Contains("not-an-email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_invalid_time_zone_time_and_weekly_day()
    {
        var errors = InventoryReportScheduleValidator.Validate(Request(
            frequency: "weekly",
            dayOfWeek: null,
            runAtLocalTime: "25:99",
            timeZoneId: "Not/AZone"));

        Assert.Contains(nameof(InventoryReportScheduleUpsertRequest.DayOfWeek), errors);
        Assert.Contains(nameof(InventoryReportScheduleUpsertRequest.RunAtLocalTime), errors);
        Assert.Contains(nameof(InventoryReportScheduleUpsertRequest.TimeZoneId), errors);
    }

    [Fact]
    public void EnsureValid_throws_a_validation_exception_with_field_errors()
    {
        var exception = Assert.Throws<InventoryReportScheduleValidationException>(() =>
            InventoryReportScheduleValidator.EnsureValid(Request(name: "", recipientsCsv: "bad")));

        Assert.Contains(nameof(InventoryReportScheduleUpsertRequest.Name), exception.Errors.Keys);
        Assert.Contains(nameof(InventoryReportScheduleUpsertRequest.RecipientsCsv), exception.Errors.Keys);
    }

    private static InventoryReportScheduleUpsertRequest Request(
        string name = "Daily report",
        string recipientsCsv = "manager@example.com;retail@example.com",
        string frequency = "daily",
        int? dayOfWeek = 1,
        string runAtLocalTime = "08:00",
        string timeZoneId = "Europe/Belgrade") => new(
        name,
        true,
        frequency,
        dayOfWeek,
        runAtLocalTime,
        timeZoneId,
        "pdf",
        "landscape",
        true,
        recipientsCsv,
        "Inventory report",
        null,
        null,
        null,
        "kolicina",
        "user-1",
        "User");
}
