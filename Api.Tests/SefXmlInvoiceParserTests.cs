using System.Text;
using Api.Services.SefXml;
using Xunit;

namespace Api.Tests;

public sealed class SefXmlInvoiceParserTests
{
    [Fact]
    public async Task ParseAsync_InvoicePreservesSourceFieldsAndMissingValuesWithoutFabricatingZeros()
    {
        var parser = new SefXmlInvoiceParser(SefXmlInvoiceParseLimits.Default);

        var preview = await parser.ParseAsync(Utf8Stream(SyntheticInvoice));

        Assert.True(preview.IsReadOnlyPreview);
        Assert.Equal(SefXmlDocumentKind.Invoice, preview.DocumentKind);
        Assert.Equal("INV-2026-001", preview.InvoiceNumber);
        Assert.Equal(new DateOnly(2026, 10, 16), preview.IssueDate);
        Assert.Equal("RSD", preview.DocumentCurrencyCode);
        Assert.Equal("Primer dobavljač d.o.o.", preview.Supplier.Name);
        Assert.Equal("109999999", preview.Supplier.CompanyId);
        Assert.Collection(
            preview.Lines,
            line =>
            {
                Assert.Equal(1, line.SourceOrder);
                Assert.Equal("1", line.SourceLineId);
                Assert.Equal("SKU-ČIZMA-39", line.SupplierItemId);
                Assert.Equal("Čizma 39", line.Name);
                Assert.Equal("H87", line.RawUnitCode);
                Assert.Equal(2.5m, line.InvoicedQuantity);
                Assert.Equal(1200.50m, line.PriceAmount);
                Assert.Equal("RSD", line.PriceCurrencyCode);
                Assert.Equal(3001.25m, line.NetLineAmount);
                Assert.Equal(100m, line.Allowances.Single().Amount);
            },
            line =>
            {
                Assert.Equal(2, line.SourceOrder);
                Assert.Equal("2", line.SourceLineId);
                Assert.Null(line.SupplierItemId);
                Assert.Null(line.InvoicedQuantity);
                Assert.Null(line.PriceAmount);
                Assert.Null(line.NetLineAmount);
                Assert.Contains(line.Diagnostics, diagnostic => diagnostic.Code == "MISSING_QUANTITY");
                Assert.Contains(line.Diagnostics, diagnostic => diagnostic.Code == "MISSING_PRICE");
            });
    }

    [Fact]
    public async Task ParseAsync_CreditNoteIsExplicitlyUnsupported()
    {
        var parser = new SefXmlInvoiceParser(SefXmlInvoiceParseLimits.Default);

        var error = await Assert.ThrowsAsync<SefXmlPreviewException>(() => parser.ParseAsync(Utf8Stream(SyntheticCreditNote)));

        Assert.Equal(SefXmlPreviewErrorCode.CreditNoteUnsupported, error.Code);
        Assert.DoesNotContain("CN-2026-001", error.SafeMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParseAsync_RejectsDtdAndExternalEntitiesWithoutResolvingThem()
    {
        var parser = new SefXmlInvoiceParser(SefXmlInvoiceParseLimits.Default);
        const string maliciousXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE Invoice [ <!ENTITY xxe SYSTEM "file:///not-allowed"> ]>
            <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2">
              <ID>&xxe;</ID>
            </Invoice>
            """;

        var error = await Assert.ThrowsAsync<SefXmlPreviewException>(() => parser.ParseAsync(Utf8Stream(maliciousXml)));

        Assert.Equal(SefXmlPreviewErrorCode.UnsafeXml, error.Code);
        Assert.DoesNotContain("file:///not-allowed", error.SafeMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParseAsync_ReportsUnusualQuantityWithoutTreatingItAsReceivedStock()
    {
        var parser = new SefXmlInvoiceParser(SefXmlInvoiceParseLimits.Default);

        var preview = await parser.ParseAsync(Utf8Stream(SyntheticNegativeQuantityInvoice));

        var line = Assert.Single(preview.Lines);
        Assert.Equal(-1m, line.InvoicedQuantity);
        Assert.Null(line.PhysicallyReceivedQuantity);
        Assert.Contains(line.Diagnostics, diagnostic => diagnostic.Code == "UNUSUAL_QUANTITY");
    }

    [Fact]
    public async Task ParseAsync_PreservesRepeatedIdentifiersTaxCurrencyAndRawUnitsWithoutConversion()
    {
        var parser = new SefXmlInvoiceParser(SefXmlInvoiceParseLimits.Default);

        var preview = await parser.ParseAsync(Utf8Stream(SyntheticInvoiceWithTaxAndRepeatedIds));

        Assert.Equal("EUR", preview.DocumentCurrencyCode);
        Assert.Equal(2, preview.Lines.Count);
        Assert.All(preview.Lines, line => Assert.Equal("DUP-42", line.SupplierItemId));
        Assert.Equal("H87", preview.Lines[0].RawUnitCode);
        Assert.Equal("KGM", preview.Lines[1].RawUnitCode);
        Assert.Null(preview.Lines[0].InvoicedQuantity);
        Assert.Contains(preview.Lines[0].Diagnostics, diagnostic => diagnostic.Code == "NON_STANDARD_DECIMAL_FORMAT");
        Assert.Equal(0m, preview.Lines[1].InvoicedQuantity);
        Assert.Contains(preview.Lines[1].Diagnostics, diagnostic => diagnostic.Code == "UNUSUAL_QUANTITY");

        var tax = Assert.Single(preview.TaxSubtotals);
        Assert.Equal(20m, tax.Percent);
        Assert.Equal("S", tax.TaxCategoryCode);
        Assert.Equal(10m, tax.TaxableAmount.Value);
        Assert.Equal("EUR", tax.TaxableAmount.CurrencyCode);
        Assert.Equal(12m, preview.Totals.PayableAmount.Value);
        Assert.Equal("EUR", preview.Totals.PayableAmount.CurrencyCode);
    }

    [Theory]
    [InlineData("<Invoice xmlns=\"https://example.invalid/not-ubl\" />", SefXmlPreviewErrorCode.UnsupportedNamespace)]
    [InlineData("<NotAnInvoice />", SefXmlPreviewErrorCode.UnsupportedDocumentType)]
    public async Task ParseAsync_RejectsUnsupportedNamespacesAndDocumentTypes(string xml, SefXmlPreviewErrorCode expectedCode)
    {
        var parser = new SefXmlInvoiceParser(SefXmlInvoiceParseLimits.Default);

        var error = await Assert.ThrowsAsync<SefXmlPreviewException>(() => parser.ParseAsync(Utf8Stream(xml)));

        Assert.Equal(expectedCode, error.Code);
    }

    [Fact]
    public async Task ParseAsync_RejectsMalformedXmlAndExcessiveDepth()
    {
        var parser = new SefXmlInvoiceParser(SefXmlInvoiceParseLimits.Default);
        var malformed = await Assert.ThrowsAsync<SefXmlPreviewException>(() => parser.ParseAsync(Utf8Stream("<Invoice>")));
        Assert.Equal(SefXmlPreviewErrorCode.MalformedXml, malformed.Code);

        var depthLimitedParser = new SefXmlInvoiceParser(SefXmlInvoiceParseLimits.Default with { MaxDepth = 1 });
        var tooComplex = await Assert.ThrowsAsync<SefXmlPreviewException>(() => depthLimitedParser.ParseAsync(Utf8Stream(SyntheticInvoice)));
        Assert.Equal(SefXmlPreviewErrorCode.DocumentTooComplex, tooComplex.Code);
    }

    [Fact]
    public async Task ParseAsync_RejectsDocumentOverConfiguredLineLimit()
    {
        var limits = SefXmlInvoiceParseLimits.Default with { MaxInvoiceLines = 1 };
        var parser = new SefXmlInvoiceParser(limits);

        var error = await Assert.ThrowsAsync<SefXmlPreviewException>(() => parser.ParseAsync(Utf8Stream(SyntheticInvoice)));

        Assert.Equal(SefXmlPreviewErrorCode.TooManyInvoiceLines, error.Code);
    }

    [Fact]
    public async Task ParseAsync_LeavesNonCanonicalDateUnknownInsteadOfGuessingIt()
    {
        var parser = new SefXmlInvoiceParser(SefXmlInvoiceParseLimits.Default);
        var nonCanonicalDate = SyntheticInvoice.Replace("2026-10-16", "16.10.2026", StringComparison.Ordinal);

        var preview = await parser.ParseAsync(Utf8Stream(nonCanonicalDate));

        Assert.Null(preview.IssueDate);
        Assert.Contains(preview.Diagnostics, diagnostic => diagnostic.Code == "INVALID_ISSUE_DATE");
    }

    [Fact]
    public async Task ParseAsync_RejectsInputThatExceedsTheReusableParserSizeCap()
    {
        var parser = new SefXmlInvoiceParser(SefXmlInvoiceParseLimits.Default with { MaxDocumentCharacters = 32 });

        var error = await Assert.ThrowsAsync<SefXmlPreviewException>(() => parser.ParseAsync(Utf8Stream(new string(' ', 33))));

        Assert.Equal(SefXmlPreviewErrorCode.DocumentTooComplex, error.Code);
    }

    [Theory]
    [InlineData("%PDF-1.7")]
    [InlineData("PK\u0003\u0004")]
    public async Task ParseAsync_RejectsMislabeledPdfAndZipContent(string nonXmlContent)
    {
        var parser = new SefXmlInvoiceParser(SefXmlInvoiceParseLimits.Default);

        var error = await Assert.ThrowsAsync<SefXmlPreviewException>(() => parser.ParseAsync(Utf8Stream(nonXmlContent)));

        Assert.Equal(SefXmlPreviewErrorCode.MalformedXml, error.Code);
    }

    private static MemoryStream Utf8Stream(string value) => new(Encoding.UTF8.GetBytes(value));

    private const string SyntheticInvoice = """
        <?xml version="1.0" encoding="utf-8"?>
        <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                 xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
                 xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2">
          <cbc:ID>INV-2026-001</cbc:ID>
          <cbc:IssueDate>2026-10-16</cbc:IssueDate>
          <cbc:InvoiceTypeCode>380</cbc:InvoiceTypeCode>
          <cbc:DocumentCurrencyCode>RSD</cbc:DocumentCurrencyCode>
          <cac:AccountingSupplierParty>
            <cac:Party>
              <cac:PartyName><cbc:Name>Primer dobavljač d.o.o.</cbc:Name></cac:PartyName>
              <cac:PartyLegalEntity><cbc:CompanyID>109999999</cbc:CompanyID></cac:PartyLegalEntity>
            </cac:Party>
          </cac:AccountingSupplierParty>
          <cac:InvoiceLine>
            <cbc:ID>1</cbc:ID>
            <cbc:InvoicedQuantity unitCode="H87">2.50</cbc:InvoicedQuantity>
            <cbc:LineExtensionAmount currencyID="RSD">3001.25</cbc:LineExtensionAmount>
            <cac:AllowanceCharge>
              <cbc:ChargeIndicator>false</cbc:ChargeIndicator>
              <cbc:Amount currencyID="RSD">100.00</cbc:Amount>
            </cac:AllowanceCharge>
            <cac:Item>
              <cbc:Name>Čizma 39</cbc:Name>
              <cac:SellersItemIdentification><cbc:ID>SKU-ČIZMA-39</cbc:ID></cac:SellersItemIdentification>
            </cac:Item>
            <cac:Price><cbc:PriceAmount currencyID="RSD">1200.50</cbc:PriceAmount></cac:Price>
          </cac:InvoiceLine>
          <cac:InvoiceLine>
            <cbc:ID>2</cbc:ID>
            <cac:Item><cbc:Name>Artikal bez potvrđenih vrednosti</cbc:Name></cac:Item>
          </cac:InvoiceLine>
        </Invoice>
        """;

    private const string SyntheticCreditNote = """
        <?xml version="1.0" encoding="utf-8"?>
        <CreditNote xmlns="urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2"
                    xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
          <cbc:ID>CN-2026-001</cbc:ID>
        </CreditNote>
        """;

    private const string SyntheticNegativeQuantityInvoice = """
        <?xml version="1.0" encoding="utf-8"?>
        <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                 xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
                 xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2">
          <cbc:ID>INV-NEGATIVE-001</cbc:ID>
          <cac:InvoiceLine>
            <cbc:ID>1</cbc:ID>
            <cbc:InvoicedQuantity unitCode="H87">-1</cbc:InvoicedQuantity>
            <cac:Item><cbc:Name>Sintetički artikal</cbc:Name></cac:Item>
          </cac:InvoiceLine>
        </Invoice>
        """;

    private const string SyntheticInvoiceWithTaxAndRepeatedIds = """
        <?xml version="1.0" encoding="utf-8"?>
        <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                 xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
                 xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2">
          <cbc:ID>INV-EUR-001</cbc:ID>
          <cbc:DocumentCurrencyCode>EUR</cbc:DocumentCurrencyCode>
          <cac:InvoiceLine>
            <cbc:ID>1</cbc:ID>
            <cbc:InvoicedQuantity unitCode="H87">1,5</cbc:InvoicedQuantity>
            <cac:Item><cbc:Name>Prva stavka</cbc:Name><cac:SellersItemIdentification><cbc:ID>DUP-42</cbc:ID></cac:SellersItemIdentification></cac:Item>
          </cac:InvoiceLine>
          <cac:InvoiceLine>
            <cbc:ID>2</cbc:ID>
            <cbc:InvoicedQuantity unitCode="KGM">0</cbc:InvoicedQuantity>
            <cac:Item><cbc:Name>Druga stavka</cbc:Name><cac:SellersItemIdentification><cbc:ID>DUP-42</cbc:ID></cac:SellersItemIdentification></cac:Item>
          </cac:InvoiceLine>
          <cac:TaxTotal><cac:TaxSubtotal>
            <cbc:TaxableAmount currencyID="EUR">10.00</cbc:TaxableAmount>
            <cbc:TaxAmount currencyID="EUR">2.00</cbc:TaxAmount>
            <cac:TaxCategory><cbc:ID>S</cbc:ID><cbc:Percent>20</cbc:Percent></cac:TaxCategory>
          </cac:TaxSubtotal></cac:TaxTotal>
          <cac:LegalMonetaryTotal><cbc:PayableAmount currencyID="EUR">12.00</cbc:PayableAmount></cac:LegalMonetaryTotal>
        </Invoice>
        """;
}
