using Api.Services.SefXml;
using Xunit;

namespace Api.Tests;

public sealed class SefXmlReconciliationServiceTests
{
    [Fact]
    public void Reconcile_UsesOnlyUniqueSupplierItemIdAsProvenExact_AndPreservesNegativeCatalogId()
    {
        var service = new SefXmlReconciliationService();

        var result = service.Reconcile(
            Invoice(Line(sourceOrder: 1, sourceLineId: "L-1", supplierItemId: "SUP-42", name: "Čizma 42")),
            new SefXmlCatalogReadResult([new SefXmlCatalogArticle(-7, "SUP-42", "Čizma 42")], IsTruncated: false),
            manualSelections: []);

        var line = Assert.Single(result.Lines);
        Assert.Equal(SefXmlMatchStatus.ExactIdMatch, line.Status);
        Assert.Equal(SefXmlMatchConfidence.ProvenExact, line.Confidence);
        Assert.Equal(-7, line.SelectedCatalogArticleId);
        Assert.Equal("L-1", line.SourceXmlLineId);
        Assert.Equal(1, line.SourceOrder);
        Assert.Null(line.PhysicallyReceivedQuantity);
        Assert.Equal(SefXmlPriceComparisonStatus.Unknown, line.PriceComparison.Status);
    }

    [Fact]
    public void Reconcile_DuplicateCatalogExternalIdIsAmbiguousNotExact()
    {
        var service = new SefXmlReconciliationService();

        var result = service.Reconcile(
            Invoice(Line(sourceOrder: 1, sourceLineId: "L-1", supplierItemId: "SHARED", name: "Isti naziv")),
            new SefXmlCatalogReadResult(
                [
                    new SefXmlCatalogArticle(1, "SHARED", "Isti naziv"),
                    new SefXmlCatalogArticle(2, "SHARED", "Isti naziv")
                ],
                IsTruncated: false),
            manualSelections: []);

        var line = Assert.Single(result.Lines);
        Assert.Equal(SefXmlMatchStatus.Ambiguous, line.Status);
        Assert.Null(line.SelectedCatalogArticleId);
        Assert.Equal(2, line.Candidates.Count);
    }

    [Fact]
    public void Reconcile_NameOnlyCandidatesNeedReviewAndRepeatedSourceLineIdsRemainSeparate()
    {
        var service = new SefXmlReconciliationService();
        var invoice = Invoice(
            Line(sourceOrder: 1, sourceLineId: "REPEATED", supplierItemId: null, name: "Model 39"),
            Line(sourceOrder: 2, sourceLineId: "REPEATED", supplierItemId: null, name: "Model 39"));

        var result = service.Reconcile(
            invoice,
            new SefXmlCatalogReadResult([new SefXmlCatalogArticle(17, "CAT-17", "Model 39")], IsTruncated: false),
            manualSelections: []);

        Assert.Collection(
            result.Lines,
            first =>
            {
                Assert.Equal(1, first.SourceOrder);
                Assert.Equal("REPEATED", first.SourceXmlLineId);
                Assert.Equal(SefXmlMatchStatus.CandidateReview, first.Status);
            },
            second =>
            {
                Assert.Equal(2, second.SourceOrder);
                Assert.Equal("REPEATED", second.SourceXmlLineId);
                Assert.Equal(SefXmlMatchStatus.CandidateReview, second.Status);
            });
    }

    [Fact]
    public void Reconcile_TwoCatalogueArticlesWithSameNameRemainAmbiguous()
    {
        var service = new SefXmlReconciliationService();

        var result = service.Reconcile(
            Invoice(Line(sourceOrder: 1, sourceLineId: "L-1", supplierItemId: null, name: "Patika 39")),
            new SefXmlCatalogReadResult(
                [
                    new SefXmlCatalogArticle(11, "CAT-11", "Patika 39"),
                    new SefXmlCatalogArticle(12, "CAT-12", "Patika 39")
                ],
                IsTruncated: false),
            manualSelections: []);

        var line = Assert.Single(result.Lines);
        Assert.Equal(SefXmlMatchStatus.Ambiguous, line.Status);
        Assert.Equal(SefXmlMatchConfidence.ReviewRequired, line.Confidence);
        Assert.Null(line.SelectedCatalogArticleId);
    }

    [Fact]
    public void Reconcile_ManualSelectionIsExplicitlyManualAndNeverPromotedToExact()
    {
        var service = new SefXmlReconciliationService();

        var result = service.Reconcile(
            Invoice(Line(sourceOrder: 4, sourceLineId: "L-4", supplierItemId: "SUP-44", name: "Sandala")),
            new SefXmlCatalogReadResult([new SefXmlCatalogArticle(8, "SUP-44", "Sandala")], IsTruncated: false),
            manualSelections: [new SefXmlManualMatchSelection(4, 8)]);

        var line = Assert.Single(result.Lines);
        Assert.Equal(SefXmlMatchStatus.ExplicitManualMatch, line.Status);
        Assert.Equal(SefXmlMatchConfidence.OperatorSelected, line.Confidence);
        Assert.Equal(8, line.SelectedCatalogArticleId);
    }

    [Fact]
    public void Reconcile_CatalogueLimitOrMissingExternalIdNeverCreatesAnExactMatch()
    {
        var service = new SefXmlReconciliationService();
        SefXmlCatalogArticle[] catalog = [new SefXmlCatalogArticle(9, "SUP-9", "Cipela")];

        var truncated = service.Reconcile(
            Invoice(Line(sourceOrder: 1, sourceLineId: "L-1", supplierItemId: "SUP-9", name: "Cipela")),
            new SefXmlCatalogReadResult(catalog, IsTruncated: true),
            manualSelections: []);
        Assert.Equal(SefXmlMatchStatus.CandidateReview, Assert.Single(truncated.Lines).Status);

        var nameOnly = service.Reconcile(
            Invoice(Line(sourceOrder: 2, sourceLineId: "L-2", supplierItemId: null, name: "Cipela")),
            new SefXmlCatalogReadResult(catalog, IsTruncated: false),
            manualSelections: []);
        Assert.Equal(SefXmlMatchStatus.CandidateReview, Assert.Single(nameOnly.Lines).Status);
    }

    [Fact]
    public void Reconcile_DifferentCurrencyUnitAndDiscountRemainUnknownPriceComparison()
    {
        var service = new SefXmlReconciliationService();
        var foreignLine = Line(sourceOrder: 1, sourceLineId: "L-1", supplierItemId: "SUP-88", name: "Čizma") with
        {
            RawUnitCode = "KGM",
            PriceAmount = 14.5m,
            PriceCurrencyCode = "EUR",
            NetLineAmount = 28m,
            NetLineCurrencyCode = "EUR",
            Allowances = [new SefXmlAllowanceCharge(1, IsCharge: false, Amount: 1m, CurrencyCode: "EUR", BaseAmount: 29m, MultiplierFactor: null)]
        };

        var result = service.Reconcile(
            Invoice(foreignLine),
            new SefXmlCatalogReadResult([new SefXmlCatalogArticle(88, "SUP-88", "Čizma")], IsTruncated: false),
            manualSelections: []);

        var line = Assert.Single(result.Lines);
        Assert.Equal(SefXmlMatchStatus.ExactIdMatch, line.Status);
        Assert.Equal(SefXmlPriceComparisonStatus.Unknown, line.PriceComparison.Status);
        Assert.Null(line.PriceComparison.Difference);
        Assert.DoesNotContain("dobavljač", line.PriceComparison.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private static SefXmlInvoicePreview Invoice(params SefXmlInvoiceLine[] lines) => new(
        IsReadOnlyPreview: true,
        DocumentKind: SefXmlDocumentKind.Invoice,
        InvoiceNumber: "SYNTHETIC-QDB12",
        IssueDate: new DateOnly(2026, 10, 16),
        InvoiceTypeCode: "380",
        DocumentCurrencyCode: "RSD",
        Supplier: new SefXmlSupplier("Sintetički dobavljač", "109999999"),
        Lines: lines,
        Totals: new SefXmlInvoiceTotals(new SefXmlAmount(null, null), new SefXmlAmount(null, null), new SefXmlAmount(null, null), new SefXmlAmount(null, null)),
        TaxSubtotals: [],
        Diagnostics: []);

    private static SefXmlInvoiceLine Line(int sourceOrder, string sourceLineId, string? supplierItemId, string name) => new(
        SourceOrder: sourceOrder,
        SourceLineId: sourceLineId,
        SupplierItemId: supplierItemId,
        BuyerItemId: null,
        StandardItemId: null,
        Name: name,
        RawUnitCode: "H87",
        InvoicedQuantity: 2m,
        PhysicallyReceivedQuantity: null,
        PriceAmount: 100m,
        PriceCurrencyCode: "RSD",
        PriceBaseQuantity: null,
        NetLineAmount: 200m,
        NetLineCurrencyCode: "RSD",
        Allowances: [],
        PriceBasisExplanation: "Synthetic price basis.",
        Diagnostics: []);
}
