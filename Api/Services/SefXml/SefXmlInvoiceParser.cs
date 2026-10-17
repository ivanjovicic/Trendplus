using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Api.Services.SefXml;

/// <summary>
/// Parses one locally supplied UBL invoice into a request-scoped, read-only preview.
/// The parser never stores the source document and never derives stock receipt facts.
/// </summary>
public interface ISefXmlInvoiceParser
{
    Task<SefXmlInvoicePreview> ParseAsync(Stream stream, CancellationToken ct = default);
}

public sealed class SefXmlInvoiceParser : ISefXmlInvoiceParser
{
    private static readonly XNamespace InvoiceNamespace = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private static readonly XNamespace CreditNoteNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    private readonly SefXmlInvoiceParseLimits _limits;

    public SefXmlInvoiceParser(SefXmlInvoiceParseLimits limits)
    {
        _limits = limits.Validate();
    }

    public async Task<SefXmlInvoicePreview> ParseAsync(Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw SefXmlPreviewException.MalformedXml();
        }

        try
        {
            await using var boundedDocument = await CopyBoundedDocumentAsync(stream, ct);
            await ValidateDocumentComplexityAsync(boundedDocument, ct);
            boundedDocument.Position = 0;

            using var reader = XmlReader.Create(boundedDocument, CreateSafeReaderSettings());
            var document = await XDocument.LoadAsync(reader, LoadOptions.None, ct);
            var invoice = document.Root ?? throw SefXmlPreviewException.MalformedXml();

            ValidateRoot(invoice);

            var documentCurrencyCode = Text(invoice.Element(Cbc + "DocumentCurrencyCode"));
            var diagnostics = new List<SefXmlDiagnostic>();
            var lines = ParseLines(invoice, documentCurrencyCode);
            if (lines.Length == 0)
            {
                diagnostics.Add(new SefXmlDiagnostic(
                    "NO_INVOICE_LINES",
                    "Dokument nema UBL stavke za pregled; ovo nije potvrda prijema robe."));
            }

            return new SefXmlInvoicePreview(
                IsReadOnlyPreview: true,
                DocumentKind: SefXmlDocumentKind.Invoice,
                InvoiceNumber: Text(invoice.Element(Cbc + "ID")),
                IssueDate: ParseDate(invoice.Element(Cbc + "IssueDate"), diagnostics, "INVALID_ISSUE_DATE"),
                InvoiceTypeCode: Text(invoice.Element(Cbc + "InvoiceTypeCode")),
                DocumentCurrencyCode: documentCurrencyCode,
                Supplier: ParseSupplier(invoice),
                Lines: lines,
                Totals: ParseTotals(invoice, documentCurrencyCode, diagnostics),
                TaxSubtotals: ParseTaxSubtotals(invoice, documentCurrencyCode, diagnostics),
                Diagnostics: diagnostics);
        }
        catch (SefXmlPreviewException)
        {
            throw;
        }
        catch (XmlException exception) when (exception.Message.Contains("DTD", StringComparison.OrdinalIgnoreCase))
        {
            throw SefXmlPreviewException.UnsafeXml();
        }
        catch (XmlException)
        {
            throw SefXmlPreviewException.MalformedXml();
        }
    }

    private static void ValidateRoot(XElement root)
    {
        if (root.Name.LocalName == "CreditNote" && root.Name.Namespace == CreditNoteNamespace)
        {
            throw new SefXmlPreviewException(
                SefXmlPreviewErrorCode.CreditNoteUnsupported,
                "Odobrenje (CreditNote) nije podržano u XML pilot pregledu.");
        }

        if (root.Name.LocalName != "Invoice")
        {
            throw new SefXmlPreviewException(
                SefXmlPreviewErrorCode.UnsupportedDocumentType,
                "Podržana je samo UBL ulazna faktura (Invoice) za read-only pregled.");
        }

        if (root.Name.Namespace != InvoiceNamespace)
        {
            throw new SefXmlPreviewException(
                SefXmlPreviewErrorCode.UnsupportedNamespace,
                "XML nije podržana UBL 2.x Invoice struktura za read-only pregled.");
        }

        var typeCode = Text(root.Element(Cbc + "InvoiceTypeCode"));
        if (string.Equals(typeCode, "381", StringComparison.Ordinal) ||
            string.Equals(typeCode, "384", StringComparison.Ordinal))
        {
            throw new SefXmlPreviewException(
                SefXmlPreviewErrorCode.UnsupportedDocumentType,
                "Tip dokumenta nije podržan za pregled ulazne fakture.");
        }
    }

    private XmlReaderSettings CreateSafeReaderSettings() => new()
    {
        Async = true,
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = _limits.MaxDocumentCharacters,
        MaxCharactersFromEntities = 0,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true,
        CloseInput = false
    };

    private async Task<MemoryStream> CopyBoundedDocumentAsync(Stream source, CancellationToken ct)
    {
        var copy = new MemoryStream(capacity: Math.Min(_limits.MaxDocumentCharacters, 128 * 1024));
        var buffer = new byte[81_920];
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(), ct);
            if (read == 0)
            {
                copy.Position = 0;
                return copy;
            }

            if (copy.Length > _limits.MaxDocumentCharacters - read)
            {
                await copy.DisposeAsync();
                throw DocumentTooComplex();
            }

            await copy.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }

    private async Task ValidateDocumentComplexityAsync(Stream document, CancellationToken ct)
    {
        using var reader = XmlReader.Create(document, CreateSafeReaderSettings());
        var elementCount = 0;
        while (await reader.ReadAsync())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            elementCount++;
            if (elementCount > _limits.MaxElements || reader.Depth > _limits.MaxDepth)
            {
                throw DocumentTooComplex();
            }
        }
    }

    private static SefXmlPreviewException DocumentTooComplex() => new(
        SefXmlPreviewErrorCode.DocumentTooComplex,
        "XML dokument prelazi dozvoljenu veličinu ili složenost za read-only pregled.");

    private SefXmlInvoiceLine[] ParseLines(XElement invoice, string? documentCurrencyCode)
    {
        var lineElements = invoice.Elements(Cac + "InvoiceLine").ToArray();
        if (lineElements.Length > _limits.MaxInvoiceLines)
        {
            throw new SefXmlPreviewException(
                SefXmlPreviewErrorCode.TooManyInvoiceLines,
                "XML dokument ima previše stavki za read-only pregled.");
        }

        return lineElements
            .Select((line, index) => ParseLine(line, index + 1, documentCurrencyCode))
            .ToArray();
    }

    private static SefXmlInvoiceLine ParseLine(XElement line, int sourceOrder, string? documentCurrencyCode)
    {
        var diagnostics = new List<SefXmlDiagnostic>();
        var quantityElement = line.Element(Cbc + "InvoicedQuantity");
        var quantity = ParseDecimal(quantityElement, diagnostics, "INVALID_QUANTITY");
        if (quantity is null)
        {
            diagnostics.Add(new SefXmlDiagnostic("MISSING_QUANTITY", "Fakturisana količina nije navedena ili nije čitljiva."));
        }
        else if (quantity <= 0)
        {
            diagnostics.Add(new SefXmlDiagnostic("UNUSUAL_QUANTITY", "Količina je nula ili negativna; nije tretirana kao fizički primljena roba."));
        }

        var item = line.Element(Cac + "Item");
        var price = line.Element(Cac + "Price");
        var priceAmountElement = price?.Element(Cbc + "PriceAmount");
        var priceAmount = ParseDecimal(priceAmountElement, diagnostics, "INVALID_PRICE");
        if (priceAmount is null)
        {
            diagnostics.Add(new SefXmlDiagnostic("MISSING_PRICE", "Cena stavke nije navedena ili njena osnova nije čitljiva."));
        }

        var netLineAmountElement = line.Element(Cbc + "LineExtensionAmount");
        var allowances = line.Elements(Cac + "AllowanceCharge")
            .Select((allowance, index) => ParseAllowance(allowance, index + 1, documentCurrencyCode, diagnostics))
            .ToArray();

        return new SefXmlInvoiceLine(
            SourceOrder: sourceOrder,
            SourceLineId: Text(line.Element(Cbc + "ID")),
            SupplierItemId: Identification(item, "SellersItemIdentification"),
            BuyerItemId: Identification(item, "BuyersItemIdentification"),
            StandardItemId: Identification(item, "StandardItemIdentification"),
            Name: Text(item?.Element(Cbc + "Name")),
            RawUnitCode: Attribute(quantityElement, "unitCode"),
            InvoicedQuantity: quantity,
            PhysicallyReceivedQuantity: null,
            PriceAmount: priceAmount,
            PriceCurrencyCode: Attribute(priceAmountElement, "currencyID"),
            PriceBaseQuantity: ParseDecimal(price?.Element(Cbc + "BaseQuantity"), diagnostics, "INVALID_PRICE_BASE_QUANTITY"),
            NetLineAmount: ParseDecimal(netLineAmountElement, diagnostics, "INVALID_NET_LINE_AMOUNT"),
            NetLineCurrencyCode: Attribute(netLineAmountElement, "currencyID"),
            Allowances: allowances,
            PriceBasisExplanation: "Cena je pročitana samo iz UBL PriceAmount/LineExtensionAmount; bruto, PDV i stvarni prijem nisu izvedeni.",
            Diagnostics: diagnostics);
    }

    private static SefXmlAllowanceCharge ParseAllowance(
        XElement allowance,
        int sourceOrder,
        string? documentCurrencyCode,
        List<SefXmlDiagnostic> diagnostics)
    {
        var amountElement = allowance.Element(Cbc + "Amount");
        return new SefXmlAllowanceCharge(
            SourceOrder: sourceOrder,
            IsCharge: bool.TryParse(Text(allowance.Element(Cbc + "ChargeIndicator")), out var isCharge) ? isCharge : null,
            Amount: ParseDecimal(amountElement, diagnostics, "INVALID_ALLOWANCE_CHARGE_AMOUNT"),
            CurrencyCode: Attribute(amountElement, "currencyID") ?? documentCurrencyCode,
            BaseAmount: ParseDecimal(allowance.Element(Cbc + "BaseAmount"), diagnostics, "INVALID_ALLOWANCE_CHARGE_BASE"),
            MultiplierFactor: ParseDecimal(allowance.Element(Cbc + "MultiplierFactorNumeric"), diagnostics, "INVALID_ALLOWANCE_CHARGE_FACTOR"));
    }

    private static SefXmlSupplier ParseSupplier(XElement invoice)
    {
        var party = invoice.Element(Cac + "AccountingSupplierParty")?.Element(Cac + "Party");
        return new SefXmlSupplier(
            Name: Text(party?.Element(Cac + "PartyName")?.Element(Cbc + "Name")) ??
                  Text(party?.Element(Cac + "PartyLegalEntity")?.Element(Cbc + "RegistrationName")),
            CompanyId: Text(party?.Element(Cac + "PartyLegalEntity")?.Element(Cbc + "CompanyID")) ??
                       Text(party?.Element(Cac + "PartyIdentification")?.Element(Cbc + "ID")));
    }

    private static SefXmlInvoiceTotals ParseTotals(
        XElement invoice,
        string? documentCurrencyCode,
        List<SefXmlDiagnostic> diagnostics)
    {
        var totals = invoice.Element(Cac + "LegalMonetaryTotal");
        return new SefXmlInvoiceTotals(
            LineExtensionAmount: Amount(totals?.Element(Cbc + "LineExtensionAmount"), documentCurrencyCode, diagnostics, "INVALID_TOTAL_LINE_EXTENSION"),
            TaxExclusiveAmount: Amount(totals?.Element(Cbc + "TaxExclusiveAmount"), documentCurrencyCode, diagnostics, "INVALID_TOTAL_TAX_EXCLUSIVE"),
            TaxInclusiveAmount: Amount(totals?.Element(Cbc + "TaxInclusiveAmount"), documentCurrencyCode, diagnostics, "INVALID_TOTAL_TAX_INCLUSIVE"),
            PayableAmount: Amount(totals?.Element(Cbc + "PayableAmount"), documentCurrencyCode, diagnostics, "INVALID_TOTAL_PAYABLE"));
    }

    private static SefXmlTaxSubtotal[] ParseTaxSubtotals(
        XElement invoice,
        string? documentCurrencyCode,
        List<SefXmlDiagnostic> diagnostics) =>
        invoice.Elements(Cac + "TaxTotal")
            .Elements(Cac + "TaxSubtotal")
            .Select((subtotal, index) => new SefXmlTaxSubtotal(
                SourceOrder: index + 1,
                TaxableAmount: Amount(subtotal.Element(Cbc + "TaxableAmount"), documentCurrencyCode, diagnostics, "INVALID_TAXABLE_AMOUNT"),
                TaxAmount: Amount(subtotal.Element(Cbc + "TaxAmount"), documentCurrencyCode, diagnostics, "INVALID_TAX_AMOUNT"),
                Percent: ParseDecimal(subtotal.Element(Cac + "TaxCategory")?.Element(Cbc + "Percent"), diagnostics, "INVALID_TAX_PERCENT"),
                TaxCategoryCode: Text(subtotal.Element(Cac + "TaxCategory")?.Element(Cbc + "ID"))))
            .ToArray();

    private static SefXmlAmount Amount(
        XElement? element,
        string? documentCurrencyCode,
        List<SefXmlDiagnostic> diagnostics,
        string invalidDiagnosticCode) =>
        new(
            Value: ParseDecimal(element, diagnostics, invalidDiagnosticCode),
            CurrencyCode: Attribute(element, "currencyID") ?? documentCurrencyCode);

    private static decimal? ParseDecimal(XElement? element, List<SefXmlDiagnostic> diagnostics, string invalidDiagnosticCode)
    {
        var raw = Text(element);
        if (raw is null)
        {
            return null;
        }

        if (raw.Contains(',', StringComparison.Ordinal))
        {
            diagnostics.Add(new SefXmlDiagnostic("NON_STANDARD_DECIMAL_FORMAT", "Decimalni format nije UBL kanonski i nije pretvoren u finansijsku vrednost."));
            return null;
        }

        if (decimal.TryParse(
                raw,
                NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var invariant))
        {
            return invariant;
        }

        diagnostics.Add(new SefXmlDiagnostic(invalidDiagnosticCode, "Numerička vrednost nije čitljiva i nije zamenjena nulom."));
        return null;
    }

    private static DateOnly? ParseDate(XElement? element, List<SefXmlDiagnostic> diagnostics, string invalidDiagnosticCode)
    {
        var raw = Text(element);
        if (raw is null)
        {
            return null;
        }

        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        diagnostics.Add(new SefXmlDiagnostic(invalidDiagnosticCode, "Datum nije čitljiv i nije zamenjen podrazumevanom vrednošću."));
        return null;
    }

    private static string? Identification(XElement? item, string elementName) =>
        Text(item?.Element(Cac + elementName)?.Element(Cbc + "ID"));

    private static string? Attribute(XElement? element, string name) =>
        Text(element?.Attribute(name));

    private static string? Text(XElement? element) => Text(element?.Value);

    private static string? Text(XAttribute? attribute) => Text(attribute?.Value);

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record SefXmlInvoiceParseLimits(int MaxDocumentCharacters, int MaxElements, int MaxDepth, int MaxInvoiceLines)
{
    public static SefXmlInvoiceParseLimits Default { get; } = new(
        MaxDocumentCharacters: 5 * 1024 * 1024,
        MaxElements: 20_000,
        MaxDepth: 32,
        MaxInvoiceLines: 500);

    public SefXmlInvoiceParseLimits Validate()
    {
        if (MaxDocumentCharacters is <= 0 or > 5 * 1024 * 1024 || MaxElements is <= 0 or > 20_000 || MaxDepth is <= 0 or > 64 || MaxInvoiceLines is <= 0 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(SefXmlInvoiceParseLimits), "SEF XML parser limits must remain bounded by the pilot safety caps.");
        }

        return this;
    }
}

public sealed class SefXmlPreviewException : Exception
{
    public SefXmlPreviewException(SefXmlPreviewErrorCode code, string safeMessage)
        : base(safeMessage)
    {
        Code = code;
        SafeMessage = safeMessage;
    }

    public SefXmlPreviewErrorCode Code { get; }
    public string SafeMessage { get; }

    public static SefXmlPreviewException UnsafeXml() => new(
        SefXmlPreviewErrorCode.UnsafeXml,
        "XML dokument sadrži nepodržanu ili nebezbednu strukturu.");

    public static SefXmlPreviewException MalformedXml() => new(
        SefXmlPreviewErrorCode.MalformedXml,
        "XML dokument nije ispravan ili nije moguće bezbedno ga pročitati.");
}

public enum SefXmlPreviewErrorCode
{
    UnsupportedDocumentType,
    UnsupportedNamespace,
    CreditNoteUnsupported,
    UnsafeXml,
    MalformedXml,
    DocumentTooComplex,
    TooManyInvoiceLines
}

public enum SefXmlDocumentKind
{
    Invoice
}

public sealed record SefXmlInvoicePreview(
    bool IsReadOnlyPreview,
    SefXmlDocumentKind DocumentKind,
    string? InvoiceNumber,
    DateOnly? IssueDate,
    string? InvoiceTypeCode,
    string? DocumentCurrencyCode,
    SefXmlSupplier Supplier,
    IReadOnlyList<SefXmlInvoiceLine> Lines,
    SefXmlInvoiceTotals Totals,
    IReadOnlyList<SefXmlTaxSubtotal> TaxSubtotals,
    IReadOnlyList<SefXmlDiagnostic> Diagnostics);

public sealed record SefXmlSupplier(string? Name, string? CompanyId);

public sealed record SefXmlInvoiceLine(
    int SourceOrder,
    string? SourceLineId,
    string? SupplierItemId,
    string? BuyerItemId,
    string? StandardItemId,
    string? Name,
    string? RawUnitCode,
    decimal? InvoicedQuantity,
    decimal? PhysicallyReceivedQuantity,
    decimal? PriceAmount,
    string? PriceCurrencyCode,
    decimal? PriceBaseQuantity,
    decimal? NetLineAmount,
    string? NetLineCurrencyCode,
    IReadOnlyList<SefXmlAllowanceCharge> Allowances,
    string PriceBasisExplanation,
    IReadOnlyList<SefXmlDiagnostic> Diagnostics);

public sealed record SefXmlAllowanceCharge(
    int SourceOrder,
    bool? IsCharge,
    decimal? Amount,
    string? CurrencyCode,
    decimal? BaseAmount,
    decimal? MultiplierFactor);

public sealed record SefXmlInvoiceTotals(
    SefXmlAmount LineExtensionAmount,
    SefXmlAmount TaxExclusiveAmount,
    SefXmlAmount TaxInclusiveAmount,
    SefXmlAmount PayableAmount);

public sealed record SefXmlAmount(decimal? Value, string? CurrencyCode);

public sealed record SefXmlTaxSubtotal(
    int SourceOrder,
    SefXmlAmount TaxableAmount,
    SefXmlAmount TaxAmount,
    decimal? Percent,
    string? TaxCategoryCode);

public sealed record SefXmlDiagnostic(string Code, string Message);
