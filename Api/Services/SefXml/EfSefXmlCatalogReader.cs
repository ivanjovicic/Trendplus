using Application.Artikli.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.SefXml;

public interface ISefXmlCatalogReader
{
    Task<SefXmlCatalogReadResult> ReadCandidatesAsync(
        SefXmlInvoicePreview invoice,
        IReadOnlyList<SefXmlManualMatchSelection>? manualSelections,
        string dataScope,
        CancellationToken ct = default);
}

/// <summary>
/// Bounded, no-tracking projection of the authorized dedicated-deployment catalogue.
/// It is intentionally unable to mutate the catalogue or load cost, stock, or formula fields.
/// </summary>
public sealed class EfSefXmlCatalogReader : ISefXmlCatalogReader
{
    internal const int MaxCatalogCandidates = 2_000;

    private readonly ITrendplusDbContext _db;

    public EfSefXmlCatalogReader(ITrendplusDbContext db)
    {
        _db = db;
    }

    public async Task<SefXmlCatalogReadResult> ReadCandidatesAsync(
        SefXmlInvoicePreview invoice,
        IReadOnlyList<SefXmlManualMatchSelection>? manualSelections,
        string dataScope,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        if (!SefXmlCatalogDataScope.TryNormalize(dataScope, out var effectiveDataScope))
        {
            throw new ArgumentOutOfRangeException(nameof(dataScope), "Unsupported catalogue data scope.");
        }

        var externalIds = invoice.Lines
            .Select(line => line.SupplierItemId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var names = invoice.Lines
            .Select(line => line.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var manualIds = (manualSelections ?? [])
            .Select(selection => selection.CatalogArticleId)
            .Distinct()
            .ToArray();

        if (externalIds.Length == 0 && names.Length == 0 && manualIds.Length == 0)
        {
            return new SefXmlCatalogReadResult([], IsTruncated: false);
        }

        IQueryable<Domain.Model.Artikli> query = _db.Artikli.AsNoTracking();
        query = effectiveDataScope switch
        {
            SefXmlCatalogDataScope.Imported => query.Where(article => article.DataOrigin == "access"),
            SefXmlCatalogDataScope.Existing => query.Where(article => article.DataOrigin == "existing" || article.DataOrigin == null || article.DataOrigin == ""),
            _ => query
        };

        var bounded = await query
            .Where(article =>
                manualIds.Contains(article.Id) ||
                (article.PLU != null && externalIds.Contains(article.PLU)) ||
                (article.Naziv != null && names.Contains(article.Naziv)))
            .OrderBy(article => article.Id)
            .Select(article => new SefXmlCatalogArticle(article.Id, article.PLU, article.Naziv))
            .Take(MaxCatalogCandidates + 1)
            .ToListAsync(ct);

        var isTruncated = bounded.Count > MaxCatalogCandidates;
        return new SefXmlCatalogReadResult(
            isTruncated ? bounded.Take(MaxCatalogCandidates).ToArray() : bounded,
            isTruncated);
    }
}
