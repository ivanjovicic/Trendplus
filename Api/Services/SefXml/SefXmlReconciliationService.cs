namespace Api.Services.SefXml;

/// <summary>
/// Produces a request-scoped reconciliation preview. It deliberately has no inventory, price-master,
/// Access, or persistence dependency: only a unique supplier item ID can be an automatic match.
/// </summary>
public sealed class SefXmlReconciliationService
{
    private const string AccessReceiptUnavailableReason =
        "Nije moguće proveriti prema Access prijemu: originalna kalkulacija i prijemna veza nisu dokazani u ovom pilotu.";

    public SefXmlReconciliationPreview Reconcile(
        SefXmlInvoicePreview invoice,
        SefXmlCatalogReadResult catalogue,
        IReadOnlyList<SefXmlManualMatchSelection>? manualSelections)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(catalogue);

        var selections = manualSelections ?? [];
        var duplicatedManualOrders = selections
            .GroupBy(selection => selection.SourceOrder)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();
        var manualSelectionByOrder = selections
            .GroupBy(selection => selection.SourceOrder)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single().CatalogArticleId);
        var cataloguesById = catalogue.Articles
            .GroupBy(article => article.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var lines = invoice.Lines
            .OrderBy(line => line.SourceOrder)
            .Select(line => ReconcileLine(line, invoice, catalogue, cataloguesById, duplicatedManualOrders, manualSelectionByOrder))
            .ToArray();

        return new SefXmlReconciliationPreview(
            IsReadOnlyPreview: true,
            CanPostInventory: false,
            SourceDocumentNumber: invoice.InvoiceNumber,
            SourceDocumentCurrencyCode: invoice.DocumentCurrencyCode,
            Supplier: invoice.Supplier,
            EffectiveDataScope: SefXmlCatalogDataScope.All,
            AccessReceiptVerificationStatus: SefXmlAccessReceiptVerificationStatus.NotAvailable,
            AccessReceiptVerificationReason: AccessReceiptUnavailableReason,
            Lines: lines,
            Diagnostics: invoice.Diagnostics);
    }

    private static SefXmlReconciliationLine ReconcileLine(
        SefXmlInvoiceLine source,
        SefXmlInvoicePreview invoice,
        SefXmlCatalogReadResult catalogue,
        IReadOnlyDictionary<int, SefXmlCatalogArticle> cataloguesById,
        IReadOnlySet<int> duplicatedManualOrders,
        IReadOnlyDictionary<int, int> manualSelectionByOrder)
    {
        var candidates = FindCandidates(source, catalogue.Articles);
        var priceComparison = new SefXmlPriceComparison(
            SefXmlPriceComparisonStatus.Unknown,
            "Cena se ne poredi: valuta, jedinica mere, PDV, rabat, datum i originalna kalkulacija nisu dokazivo uporedivi.",
            Difference: null);

        if (duplicatedManualOrders.Contains(source.SourceOrder))
        {
            return CreateLine(source, invoice, SefXmlMatchStatus.Invalid, SefXmlMatchConfidence.Unknown,
                "DUPLICATE_MANUAL_SELECTION", "Za istu izvornu stavku postoji više ručnih izbora.", null, candidates, priceComparison);
        }

        if (manualSelectionByOrder.TryGetValue(source.SourceOrder, out var manuallySelectedCatalogId))
        {
            if (!cataloguesById.TryGetValue(manuallySelectedCatalogId, out var manuallySelected))
            {
                return CreateLine(source, invoice, SefXmlMatchStatus.Invalid, SefXmlMatchConfidence.Unknown,
                    "MANUAL_CATALOG_ARTICLE_NOT_AVAILABLE", "Ručno izabrani artikal nije dostupan u dozvoljenom katalogu.", null, candidates, priceComparison);
            }

            return CreateLine(source, invoice, SefXmlMatchStatus.ExplicitManualMatch, SefXmlMatchConfidence.OperatorSelected,
                "OPERATOR_MANUAL_SELECTION", "Operater je ručno izabrao artikal; izbor nije automatski dokazano poklapanje.", manuallySelected,
                candidates, priceComparison);
        }

        if (string.IsNullOrWhiteSpace(source.SupplierItemId) && string.IsNullOrWhiteSpace(source.Name))
        {
            return CreateLine(source, invoice, SefXmlMatchStatus.Invalid, SefXmlMatchConfidence.Unknown,
                "SOURCE_ID_AND_NAME_MISSING", "Izvorna stavka nema proverljivu šifru ni naziv za uparivanje.", null, candidates, priceComparison);
        }

        if (catalogue.IsTruncated)
        {
            return CreateLine(source, invoice, SefXmlMatchStatus.CandidateReview, SefXmlMatchConfidence.ReviewRequired,
                "CATALOGUE_CANDIDATE_LIMIT_REACHED", "Katalog je ograničen; automatsko poklapanje nije bezbedno dok pregled nije potpun.", null,
                candidates, priceComparison);
        }

        var exactExternalIdCandidates = string.IsNullOrWhiteSpace(source.SupplierItemId)
            ? []
            : catalogue.Articles
                .Where(article => string.Equals(article.ExternalArticleId, source.SupplierItemId, StringComparison.Ordinal))
                .OrderBy(article => article.Id)
                .ToArray();
        if (exactExternalIdCandidates.Length == 1)
        {
            return CreateLine(source, invoice, SefXmlMatchStatus.ExactIdMatch, SefXmlMatchConfidence.ProvenExact,
                "SUPPLIER_ITEM_ID_TO_UNIQUE_CATALOG_EXTERNAL_ID", "Jedinstvena šifra dobavljača odgovara katalogskoj eksternoj šifri.",
                exactExternalIdCandidates[0], candidates, priceComparison);
        }

        if (exactExternalIdCandidates.Length > 1)
        {
            return CreateLine(source, invoice, SefXmlMatchStatus.Ambiguous, SefXmlMatchConfidence.ReviewRequired,
                "DUPLICATE_CATALOG_EXTERNAL_ID", "Ista spoljna šifra postoji na više artikala; potreban je ručni izbor.", null,
                candidates, priceComparison);
        }

        var nameCandidates = string.IsNullOrWhiteSpace(source.Name)
            ? []
            : catalogue.Articles
                .Where(article => string.Equals(article.Name, source.Name, StringComparison.Ordinal))
                .OrderBy(article => article.Id)
                .ToArray();
        if (nameCandidates.Length > 1)
        {
            return CreateLine(source, invoice, SefXmlMatchStatus.Ambiguous, SefXmlMatchConfidence.ReviewRequired,
                "MULTIPLE_NAME_CANDIDATES", "Više artikala ima isti naziv; naziv nije dovoljan za automatsko poklapanje.", null,
                candidates, priceComparison);
        }

        if (nameCandidates.Length == 1)
        {
            return CreateLine(source, invoice, SefXmlMatchStatus.CandidateReview, SefXmlMatchConfidence.ReviewRequired,
                "NAME_ONLY_CANDIDATE", "Naziv daje kandidata, ali nije dokazano poklapanje bez jedinstvene spoljne šifre.", null,
                candidates, priceComparison);
        }

        return CreateLine(source, invoice, SefXmlMatchStatus.Unmatched, SefXmlMatchConfidence.Unknown,
            "NO_CATALOGUE_CANDIDATE", "Nije pronađen proverljiv kandidat u dozvoljenom katalogu.", null, candidates, priceComparison);
    }

    private static SefXmlCatalogCandidate[] FindCandidates(SefXmlInvoiceLine source, IReadOnlyList<SefXmlCatalogArticle> catalogue) =>
        catalogue
            .Where(article =>
                (!string.IsNullOrWhiteSpace(source.SupplierItemId) && string.Equals(article.ExternalArticleId, source.SupplierItemId, StringComparison.Ordinal)) ||
                (!string.IsNullOrWhiteSpace(source.Name) && string.Equals(article.Name, source.Name, StringComparison.Ordinal)))
            .GroupBy(article => article.Id)
            .Select(group => group.First())
            .OrderBy(article => article.Id)
            .Select(article => new SefXmlCatalogCandidate(article.Id, article.ExternalArticleId, article.Name))
            .ToArray();

    private static SefXmlReconciliationLine CreateLine(
        SefXmlInvoiceLine source,
        SefXmlInvoicePreview invoice,
        SefXmlMatchStatus status,
        SefXmlMatchConfidence confidence,
        string reasonCode,
        string reason,
        SefXmlCatalogArticle? selected,
        IReadOnlyList<SefXmlCatalogCandidate> candidates,
        SefXmlPriceComparison priceComparison) =>
        new(
            SourceOrder: source.SourceOrder,
            SourceXmlLineId: source.SourceLineId,
            SourceDocumentNumber: invoice.InvoiceNumber,
            SourceSupplierItemId: source.SupplierItemId,
            SourceBuyerItemId: source.BuyerItemId,
            SourceStandardItemId: source.StandardItemId,
            SourceName: source.Name,
            RawUnitCode: source.RawUnitCode,
            InvoicedQuantity: source.InvoicedQuantity,
            PhysicallyReceivedQuantity: null,
            PriceAmount: source.PriceAmount,
            PriceCurrencyCode: source.PriceCurrencyCode,
            NetLineAmount: source.NetLineAmount,
            NetLineCurrencyCode: source.NetLineCurrencyCode,
            Allowances: source.Allowances,
            SourcePriceBasisExplanation: source.PriceBasisExplanation,
            Status: status,
            Confidence: confidence,
            MatchReasonCode: reasonCode,
            MatchReason: reason,
            SelectedCatalogArticleId: selected?.Id,
            SelectedCatalogExternalArticleId: selected?.ExternalArticleId,
            SelectedCatalogName: selected?.Name,
            Candidates: candidates,
            PriceComparison: priceComparison);
}

public static class SefXmlCatalogDataScope
{
    public const string All = "all";
    public const string Imported = "imported";
    public const string Existing = "existing";

    public static bool TryNormalize(string? requested, out string effective)
    {
        effective = string.IsNullOrWhiteSpace(requested) ? All : requested.Trim().ToLowerInvariant();
        return effective is All or Imported or Existing;
    }
}

public sealed record SefXmlReconciliationRequest(
    SefXmlInvoicePreview? Invoice,
    IReadOnlyList<SefXmlManualMatchSelection>? ManualSelections,
    string? DataScope);

public sealed record SefXmlManualMatchSelection(int SourceOrder, int CatalogArticleId);

public sealed record SefXmlCatalogArticle(int Id, string? ExternalArticleId, string? Name);

public sealed record SefXmlCatalogReadResult(IReadOnlyList<SefXmlCatalogArticle> Articles, bool IsTruncated);

public sealed record SefXmlReconciliationPreview(
    bool IsReadOnlyPreview,
    bool CanPostInventory,
    string? SourceDocumentNumber,
    string? SourceDocumentCurrencyCode,
    SefXmlSupplier Supplier,
    string EffectiveDataScope,
    SefXmlAccessReceiptVerificationStatus AccessReceiptVerificationStatus,
    string AccessReceiptVerificationReason,
    IReadOnlyList<SefXmlReconciliationLine> Lines,
    IReadOnlyList<SefXmlDiagnostic> Diagnostics);

public sealed record SefXmlReconciliationLine(
    int SourceOrder,
    string? SourceXmlLineId,
    string? SourceDocumentNumber,
    string? SourceSupplierItemId,
    string? SourceBuyerItemId,
    string? SourceStandardItemId,
    string? SourceName,
    string? RawUnitCode,
    decimal? InvoicedQuantity,
    decimal? PhysicallyReceivedQuantity,
    decimal? PriceAmount,
    string? PriceCurrencyCode,
    decimal? NetLineAmount,
    string? NetLineCurrencyCode,
    IReadOnlyList<SefXmlAllowanceCharge> Allowances,
    string SourcePriceBasisExplanation,
    SefXmlMatchStatus Status,
    SefXmlMatchConfidence Confidence,
    string MatchReasonCode,
    string MatchReason,
    int? SelectedCatalogArticleId,
    string? SelectedCatalogExternalArticleId,
    string? SelectedCatalogName,
    IReadOnlyList<SefXmlCatalogCandidate> Candidates,
    SefXmlPriceComparison PriceComparison);

public sealed record SefXmlCatalogCandidate(int CatalogArticleId, string? ExternalArticleId, string? Name);

public sealed record SefXmlPriceComparison(SefXmlPriceComparisonStatus Status, string Reason, decimal? Difference);

public enum SefXmlMatchStatus
{
    ExactIdMatch,
    ExplicitManualMatch,
    CandidateReview,
    Unmatched,
    Ambiguous,
    Invalid
}

public enum SefXmlMatchConfidence
{
    ProvenExact,
    OperatorSelected,
    ReviewRequired,
    Unknown
}

public enum SefXmlPriceComparisonStatus
{
    Unknown
}

public enum SefXmlAccessReceiptVerificationStatus
{
    NotAvailable
}
