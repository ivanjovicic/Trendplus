using System.Globalization;
using System.Text;

namespace Infrastructure.Analytics;

public static class ReturnFactStatusMapper
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static string Normalize(string? sourceStatus)
    {
        var normalized = NormalizeToken(sourceStatus);
        if (string.IsNullOrWhiteSpace(normalized))
            return Pending;

        return normalized switch
        {
            "pending" => Pending,
            "created" => Pending,
            "draft" => Pending,
            "sent" => Pending,
            "inreview" => Pending,
            "waiting" => Pending,
            "kreiran" => Pending,
            "poslat" => Pending,
            "uobradi" => Pending,
            "nacekanju" => Pending,
            "approved" => Approved,
            "accepted" => Approved,
            "confirmed" => Approved,
            "prihvacen" => Approved,
            "odobren" => Approved,
            "realizovan" => Approved,
            "rejected" => Rejected,
            "declined" => Rejected,
            "denied" => Rejected,
            "cancelled" => Rejected,
            "canceled" => Rejected,
            "storno" => Rejected,
            "storniran" => Rejected,
            "odbijen" => Rejected,
            _ => Pending
        };
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(ch))
                builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }
}
