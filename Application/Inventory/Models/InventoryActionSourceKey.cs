using System.Globalization;

namespace Application.Inventory.Models;

/// <summary>
/// Versioned identity for an Inventory action. The dataset context is part of
/// the identity so an open action cannot be reused for a different scope,
/// signal window or snapshot generation. Legacy opaque keys remain readable
/// and writable by the action ledger, but are never normalized into v2 or
/// used as a fallback for a v2 lookup.
/// </summary>
public static class InventoryActionSourceKey
{
    public const string SchemaVersion = "v2";
    public const string UnknownContext = "unknown";

    public static bool IsV2(string? sourceKey)
        => sourceKey?.StartsWith($"inventory|{SchemaVersion}|", StringComparison.Ordinal) == true;

    public static string Build(
        string actionKind,
        int articleId,
        int? storeId,
        string? sizeCode,
        string? dataScope,
        string? periodFrom,
        string? periodTo,
        string? snapshotGeneration,
        int? fromStoreId = null,
        int? toStoreId = null)
    {
        if (articleId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(articleId));
        }

        return string.Join("|",
            "inventory",
            SchemaVersion,
            $"kind={Encode(actionKind)}",
            $"article={articleId.ToString(CultureInfo.InvariantCulture)}",
            $"store={FormatStore(storeId)}",
            $"size={EncodeOrAll(sizeCode)}",
            $"fromStore={FormatStore(fromStoreId)}",
            $"toStore={FormatStore(toStoreId)}",
            $"scope={EncodeOrUnknown(dataScope)}",
            $"periodFrom={EncodeOrUnknown(periodFrom)}",
            $"periodTo={EncodeOrUnknown(periodTo)}",
            $"snapshot={EncodeOrUnknown(snapshotGeneration)}");
    }

    private static string FormatStore(int? storeId)
        => storeId is > 0
            ? storeId.Value.ToString(CultureInfo.InvariantCulture)
            : "all";

    private static string EncodeOrAll(string? value)
        => string.IsNullOrWhiteSpace(value) ? "all" : Encode(value);

    private static string EncodeOrUnknown(string? value)
        => string.IsNullOrWhiteSpace(value) ? UnknownContext : Encode(value);

    private static string Encode(string value)
        => Uri.EscapeDataString(value.Trim().ToLowerInvariant());
}
