using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Artikli.Common.Interfaces;
using Domain.Model;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using NpgsqlTypes;

namespace Api.Services;

public sealed record GoodsReceiptLineRequest(
    int? ArticleId,
    string? ArticleCode,
    string? ArticleName,
    int Quantity,
    decimal? PurchaseUnitPrice,
    decimal? SalesUnitPrice,
    int? ShoeTypeId,
    int? SeasonId,
    string? Comment);

public sealed record GoodsReceiptRequest(
    Guid RequestKey,
    int SupplierId,
    int StoreId,
    string DocumentType,
    string DocumentNumber,
    DateOnly DocumentDate,
    DateOnly ReceivedDate,
    string Currency,
    string? Comment,
    bool ConfirmSimilarDocument,
    bool ConfirmDuplicateLines,
    IReadOnlyList<GoodsReceiptLineRequest> Lines);

public sealed record GoodsReceiptLineResponse(
    long Id,
    int LineNumber,
    int ArticleId,
    string ArticleName,
    string? ArticleCode,
    int Quantity,
    decimal PurchaseUnitPrice,
    decimal SalesUnitPrice,
    decimal PurchaseLineTotal,
    string Currency,
    string? Comment,
    int? MovementId);

public sealed record GoodsReceiptArticleResponse(
    int Id,
    string Name,
    string? Code,
    int? Quantity,
    decimal? PurchasePrice,
    decimal? SalesPrice,
    int StoreId,
    string DataOrigin);

public sealed record GoodsReceiptResponse(
    long Id,
    string Status,
    Guid RequestKey,
    int SupplierId,
    string SupplierName,
    int StoreId,
    string DocumentType,
    string DocumentNumber,
    DateOnly DocumentDate,
    DateOnly ReceivedDate,
    string Currency,
    string? Comment,
    string CreatedBy,
    DateTime CreatedAtUtc,
    int LineCount,
    int TotalQuantity,
    decimal PurchaseTotal,
    IReadOnlyList<GoodsReceiptLineResponse> Lines);

public sealed record GoodsReceiptCreateResult(
    GoodsReceiptResponse Receipt,
    bool Replayed,
    long? SimilarReceiptId = null,
    bool RequiresSimilarConfirmation = false);

public sealed class GoodsReceiptException(string code, string message, int statusCode = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public sealed class GoodsReceiptService(
    ITrendplusDbContext operationalDb,
    IAnalyticsDbContext analyticsDb,
    ILogger<GoodsReceiptService> logger)
{
    private static readonly HashSet<string> DocumentTypes = ["invoice", "delivery-note"];
    private const decimal MaxNumeric18Scale2 = 9999999999999999.99m;

    public async Task<GoodsReceiptCreateResult> CreateAsync(
        GoodsReceiptRequest request,
        string createdBy,
        CancellationToken ct)
    {
        ValidateRequest(request);
        var requestHash = CalculateRequestHash(request);
        try
        {
            await using var efTransaction = await operationalDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            var connection = (NpgsqlConnection)operationalDb.GetDbConnection();
            var transaction = (NpgsqlTransaction)efTransaction.GetDbTransaction();

            var replay = await FindReceiptByRequestKeyAsync(connection, transaction, request.RequestKey, ct);
            if (replay is not null)
            {
                if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(replay.RequestHash), Encoding.ASCII.GetBytes(requestHash)))
                    throw new GoodsReceiptException("idempotency_payload_mismatch", "Ključ zahteva je već upotrebljen za drugačiji prijem. Otvorite novi prijem.", 409);

                await efTransaction.CommitAsync(ct);
                return new GoodsReceiptCreateResult(replay.Receipt, Replayed: true);
            }

            var storeExists = await analyticsDb.StoresDim.AsNoTracking()
                .AnyAsync(x => x.StoreId == request.StoreId, ct);
            if (!storeExists)
                throw new GoodsReceiptException("unknown_store", "Izabrana prodavnica/skladište nije pronađeno. Osvežite spisak lokacija.", 422);

            var supplierExists = await ScalarBoolAsync(connection, transaction,
                "SELECT EXISTS (SELECT 1 FROM \"Dobavljaci\" WHERE \"Id\" = @id)", ct,
                ("id", request.SupplierId));
            if (!supplierExists)
                throw new GoodsReceiptException("unknown_supplier", "Izabrani dobavljač više ne postoji. Osvežite spisak dobavljača.", 422);

            await AcquireDocumentLockAsync(connection, transaction, request, ct);
            // A concurrent request with the same key may have committed while this transaction
            // waited for the document lock. Recheck before duplicate-document handling so an
            // exact retry returns the committed receipt instead of a false similar-document 409.
            replay = await FindReceiptByRequestKeyAsync(connection, transaction, request.RequestKey, ct);
            if (replay is not null)
            {
                if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(replay.RequestHash), Encoding.ASCII.GetBytes(requestHash)))
                    throw new GoodsReceiptException("idempotency_payload_mismatch", "Ključ zahteva je već upotrebljen za drugačiji prijem. Otvorite novi prijem.", 409);

                await efTransaction.CommitAsync(ct);
                return new GoodsReceiptCreateResult(replay.Receipt, Replayed: true);
            }

            var similarId = await FindSimilarReceiptIdAsync(connection, transaction, request, ct);
            if (similarId.HasValue && !request.ConfirmSimilarDocument)
            {
                await efTransaction.RollbackAsync(ct);
                return new GoodsReceiptCreateResult(
                    Receipt: null!, Replayed: false, SimilarReceiptId: similarId, RequiresSimilarConfirmation: true);
            }

            if (request.Lines.Any(x => !x.ArticleId.HasValue))
                await AccessArticleStockOverlay.AcquireArticleIdentityLockAsync(operationalDb.Database, ct);

            var existingArticles = await LockAndValidateExistingArticlesAsync(connection, transaction, request, ct);
            await ValidateNewArticleReferencesAsync(connection, transaction, request, ct);

            var supplierName = await ScalarStringAsync(connection, transaction,
                "SELECT \"Naziv\" FROM \"Dobavljaci\" WHERE \"Id\" = @id", ct,
                ("id", request.SupplierId)) ?? "";
            var insertedHeader = await InsertReceiptHeaderAsync(connection, transaction, request, requestHash, createdBy, ct);
            var headerId = insertedHeader.Id;

            foreach (var group in request.Lines.Where(x => x.ArticleId.HasValue).GroupBy(x => x.ArticleId!.Value))
            {
                var quantity = group.Sum(x => x.Quantity);
                var updated = await ScalarIntAsync(connection, transaction, """
                    UPDATE "Artikli"
                    SET "Kolicina" = "Kolicina" + @quantity, "UpdatedAt" = NOW()
                    WHERE "Id" = @id AND "IDObjekat" = @store AND "Kolicina" IS NOT NULL
                    RETURNING "Id"
                    """, ct, ("quantity", quantity), ("id", group.Key), ("store", request.StoreId));
                if (updated != group.Key)
                    throw new GoodsReceiptException("stock_update_conflict", $"Zaliha artikla {group.Key} više nije raspoloživa u izabranoj lokaciji. Osvežite artikle i proverite prijem.", 409);
            }

            var lineResponses = new List<GoodsReceiptLineResponse>(request.Lines.Count);
            for (var index = 0; index < request.Lines.Count; index++)
            {
                var line = request.Lines[index];
                var article = line.ArticleId.HasValue
                    ? existingArticles[line.ArticleId.Value]
                    : await InsertNewArticleAsync(connection, transaction, request, line, ct);

                var lineId = await InsertReceiptLineAsync(connection, transaction, headerId, index + 1, article,
                    line, request.Currency, ct);
                var movementId = await InsertReceiptMovementAsync(connection, transaction, request, lineId,
                    article.Id, line, createdBy, ct);
                lineResponses.Add(new GoodsReceiptLineResponse(
                    lineId, index + 1, article.Id, article.Name, article.Code, line.Quantity,
                    line.PurchaseUnitPrice!.Value, line.SalesUnitPrice!.Value,
                    decimal.Round(line.Quantity * line.PurchaseUnitPrice.Value, 2), request.Currency,
                    line.Comment, movementId));
            }

            await efTransaction.CommitAsync(ct);
            var receipt = new GoodsReceiptResponse(
                headerId, "confirmed", request.RequestKey, request.SupplierId, supplierName,
                request.StoreId, request.DocumentType, request.DocumentNumber.Trim(),
                request.DocumentDate, request.ReceivedDate, request.Currency, request.Comment?.Trim(),
                createdBy, insertedHeader.CreatedAtUtc, lineResponses.Count, lineResponses.Sum(x => x.Quantity),
                lineResponses.Sum(x => x.PurchaseLineTotal), lineResponses);

            logger.LogInformation(
                "Goods receipt committed. ReceiptId={ReceiptId} RequestKey={RequestKey} SupplierId={SupplierId} StoreId={StoreId} LineCount={LineCount} TotalQuantity={TotalQuantity} Actor={Actor}",
                receipt.Id, request.RequestKey, request.SupplierId, request.StoreId,
                receipt.LineCount, receipt.TotalQuantity, createdBy);
            return new GoodsReceiptCreateResult(receipt, Replayed: false);
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UniqueViolation &&
            string.Equals(ex.ConstraintName, "UX_GoodsReceipts_RequestKey", StringComparison.Ordinal))
        {
            var replay = await FindReceiptByRequestKeyAsync(
                (NpgsqlConnection)operationalDb.GetDbConnection(), null, request.RequestKey, ct);
            if (replay is null)
                throw;
            if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(replay.RequestHash), Encoding.ASCII.GetBytes(requestHash)))
                throw new GoodsReceiptException("idempotency_payload_mismatch", "Ključ zahteva je već upotrebljen za drugačiji prijem. Otvorite novi prijem.", 409);
            return new GoodsReceiptCreateResult(replay.Receipt, Replayed: true);
        }
    }

    public async Task<IReadOnlyList<GoodsReceiptResponse>> SearchAsync(
        string? query, int? supplierId, DateOnly? fromDate, DateOnly? toDate, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Clamp(page, 1, 10000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var connection = (NpgsqlConnection)operationalDb.GetDbConnection();
        await operationalDb.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT r."Id", r."Status", r."RequestKey", r."SupplierId", COALESCE(s."Naziv", ''),
                       r."StoreId", r."DocumentType", r."DocumentNumber", r."DocumentDate", r."ReceivedDate",
                       r."Currency", r."Comment", r."CreatedBy", r."CreatedAtUtc",
                       COUNT(l."Id")::integer, COALESCE(SUM(l."Quantity"), 0)::integer,
                       COALESCE(SUM(l."Quantity" * l."PurchaseUnitPrice"), 0)::numeric
                FROM "GoodsReceipts" r
                JOIN "Dobavljaci" s ON s."Id" = r."SupplierId"
                LEFT JOIN "GoodsReceiptLines" l ON l."GoodsReceiptId" = r."Id"
                WHERE (@supplierId IS NULL OR r."SupplierId" = @supplierId)
                  AND (@fromDate IS NULL OR r."ReceivedDate" >= @fromDate)
                  AND (@toDate IS NULL OR r."ReceivedDate" <= @toDate)
                  AND (@query IS NULL OR r."DocumentNumber" ILIKE '%' || @query || '%'
                       OR s."Naziv" ILIKE '%' || @query || '%'
                       OR r."Id"::text = @query)
                GROUP BY r."Id", s."Naziv"
                ORDER BY r."CreatedAtUtc" DESC, r."Id" DESC
                LIMIT @limit OFFSET @offset
                """;
            Add(command, "supplierId", supplierId, NpgsqlDbType.Integer);
            Add(command, "fromDate", fromDate, NpgsqlDbType.Date);
            Add(command, "toDate", toDate, NpgsqlDbType.Date);
            Add(command, "query", string.IsNullOrWhiteSpace(query) ? null : query.Trim(), NpgsqlDbType.Text);
            Add(command, "limit", pageSize, NpgsqlDbType.Integer);
            Add(command, "offset", (page - 1) * pageSize, NpgsqlDbType.Integer);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var receipts = new List<GoodsReceiptResponse>();
            while (await reader.ReadAsync(ct))
                receipts.Add(ReadReceiptSummary(reader));
            return receipts;
        }
        finally
        {
            await operationalDb.Database.CloseConnectionAsync();
        }
    }

    public async Task<IReadOnlyList<GoodsReceiptArticleResponse>> SearchArticlesAsync(
        int storeId, string? query, CancellationToken ct)
    {
        var connection = (NpgsqlConnection)operationalDb.GetDbConnection();
        await operationalDb.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT "Id", "Naziv", "PLU", "Kolicina", "NabavnaCena", "ProdajnaCena", "IDObjekat", "DataOrigin"
                FROM "Artikli"
                WHERE "IDObjekat" = @store
                  AND (@query IS NULL OR "Naziv" ILIKE '%' || @query || '%' OR "PLU" ILIKE '%' || @query || '%'
                       OR "Id"::text = @query)
                ORDER BY "Naziv", "Id"
                LIMIT 30
                """;
            Add(command, "store", storeId, NpgsqlDbType.Integer);
            Add(command, "query", string.IsNullOrWhiteSpace(query) ? null : query.Trim(), NpgsqlDbType.Text);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var articles = new List<GoodsReceiptArticleResponse>();
            while (await reader.ReadAsync(ct))
                articles.Add(new GoodsReceiptArticleResponse(
                    reader.GetInt32(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                    reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                    reader.GetInt32(6), reader.GetString(7)));
            return articles;
        }
        finally
        {
            await operationalDb.Database.CloseConnectionAsync();
        }
    }

    public async Task<GoodsReceiptResponse?> GetAsync(long id, CancellationToken ct)
    {
        var connection = (NpgsqlConnection)operationalDb.GetDbConnection();
        await operationalDb.Database.OpenConnectionAsync(ct);
        try
        {
            var receipt = await FindReceiptByIdAsync(connection, null, id, ct);
            if (receipt is null) return null;
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT l."Id", l."LineNumber", l."ArticleId", l."ArticleNameSnapshot", l."ArticleCodeSnapshot",
                       l."Quantity", l."PurchaseUnitPrice", l."SalesUnitPrice", l."Quantity" * l."PurchaseUnitPrice",
                       l."Currency", l."Comment", d."Id"
                FROM "GoodsReceiptLines" l
                LEFT JOIN "DnevnikPromena" d ON d."GoodsReceiptLineId" = l."Id"
                WHERE l."GoodsReceiptId" = @id ORDER BY l."LineNumber"
                """;
            Add(command, "id", id, NpgsqlDbType.Bigint);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var lines = new List<GoodsReceiptLineResponse>();
            while (await reader.ReadAsync(ct))
                lines.Add(new GoodsReceiptLineResponse(
                    reader.GetInt64(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetInt32(5), reader.GetDecimal(6),
                    reader.GetDecimal(7), reader.GetDecimal(8), reader.GetString(9),
                    reader.IsDBNull(10) ? null : reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetInt32(11)));
            return receipt with { Lines = lines };
        }
        finally
        {
            await operationalDb.Database.CloseConnectionAsync();
        }
    }

    public async Task<GoodsReceiptResponse?> GetByRequestKeyAsync(Guid requestKey, CancellationToken ct)
    {
        var connection = (NpgsqlConnection)operationalDb.GetDbConnection();
        await operationalDb.Database.OpenConnectionAsync(ct);
        StoredReceipt? stored;
        try
        {
            stored = await FindReceiptByRequestKeyAsync(connection, null, requestKey, ct);
        }
        finally
        {
            await operationalDb.Database.CloseConnectionAsync();
        }

        return stored is null ? null : await GetAsync(stored.Receipt.Id, ct);
    }

    private static void ValidateRequest(GoodsReceiptRequest request)
    {
        if (request.RequestKey == Guid.Empty)
            throw new GoodsReceiptException("missing_request_key", "Nedostaje ključ prijema. Osvežite stranicu i pokušajte ponovo.");
        if (!DocumentTypes.Contains(request.DocumentType))
            throw new GoodsReceiptException("invalid_document_type", "Vrsta dokumenta mora biti račun ili otpremnica.");
        if (string.IsNullOrWhiteSpace(request.DocumentNumber) || request.DocumentNumber.Trim().Length > 100)
            throw new GoodsReceiptException("invalid_document_number", "Broj dokumenta je obavezan i ne sme biti duži od 100 znakova.");
        if (request.Currency != "RSD")
            throw new GoodsReceiptException("unsupported_currency", "Trenutno je podržan samo prijem u RSD valuti.");
        if (request.Comment?.Length > 500)
            throw new GoodsReceiptException("comment_too_long", "Komentar ne sme biti duži od 500 znakova.");
        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.DocumentDate == default || request.ReceivedDate == default ||
            request.DocumentDate > todayUtc.AddDays(1) || request.ReceivedDate > todayUtc.AddDays(1))
            throw new GoodsReceiptException("invalid_receipt_date", "Datumi dokumenta i fizičkog prijema moraju biti važeći i ne mogu biti u budućnosti.");
        if (request.Lines is null || request.Lines.Count is < 1 or > 500 || request.Lines.Any(x => x is null))
            throw new GoodsReceiptException("invalid_line_count", "Prijem mora imati od 1 do 500 stavki.");
        if (request.Lines.Sum(x => (long)x.Quantity) > int.MaxValue)
            throw new GoodsReceiptException("quantity_overflow", "Ukupna količina prijema prelazi podržani opseg.");
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            if (line.Quantity <= 0)
                throw new GoodsReceiptException("invalid_quantity", $"Količina u stavci {i + 1} mora biti ceo broj veći od nule.");
            if (!ValidMoney(line.PurchaseUnitPrice) || !ValidMoney(line.SalesUnitPrice))
                throw new GoodsReceiptException("invalid_price", $"Nabavna i prodajna cena u stavci {i + 1} moraju biti poznate, nenegativne i imati najviše dve decimale.");
            if (line.ArticleId.HasValue && (!string.IsNullOrWhiteSpace(line.ArticleName) || !string.IsNullOrWhiteSpace(line.ArticleCode)))
                throw new GoodsReceiptException("mixed_article_identity", $"Stavka {i + 1} istovremeno navodi postojeći ID i novi artikal. Izaberite jedan identitet.");
            if (!line.ArticleId.HasValue && string.IsNullOrWhiteSpace(line.ArticleName))
                throw new GoodsReceiptException("missing_article", $"Unesite naziv novog artikla u stavci {i + 1}.");
            if (!line.ArticleId.HasValue && (!line.ShoeTypeId.HasValue || !line.SeasonId.HasValue))
                throw new GoodsReceiptException("missing_article_classification", $"Izaberite tip obuće i sezonu za novi artikal u stavci {i + 1}.");
            if (line.ArticleName?.Length > 500 || line.ArticleCode?.Length > 100 || line.Comment?.Length > 500)
                throw new GoodsReceiptException("article_field_too_long", $"Podaci u stavci {i + 1} su predugački.");
            if (line.ArticleId is null && line.ArticleName!.Length > 500)
                throw new GoodsReceiptException("invalid_article_name", $"Naziv artikla u stavci {i + 1} je predugačak.");
            if ((decimal)line.Quantity * line.PurchaseUnitPrice!.Value > MaxNumeric18Scale2)
                throw new GoodsReceiptException("line_total_overflow", $"Nabavna vrednost stavke {i + 1} prelazi podržani iznos.");
        }

        var duplicateNewItems = request.Lines.Where(x => !x.ArticleId.HasValue)
            .GroupBy(x => $"{x.ArticleName?.Trim().ToUpperInvariant()}|{x.ArticleCode?.Trim().ToUpperInvariant()}|{x.ShoeTypeId}|{x.SeasonId}")
            .Any(g => g.Count() > 1);
        if (duplicateNewItems)
            throw new GoodsReceiptException("duplicate_new_article", "Isti novi artikal je unet više puta. Spojite količinu u jednu stavku ili izaberite postojeći artikal.");
        if (request.Lines.Where(x => !x.ArticleId.HasValue && !string.IsNullOrWhiteSpace(x.ArticleCode))
            .GroupBy(x => x.ArticleCode!.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new GoodsReceiptException("duplicate_new_article_code", "Ista šifra je uneta više puta. Izaberite postojeći artikal ili ispravite šifre stavki.");
        if (!request.ConfirmDuplicateLines && request.Lines.Where(x => x.ArticleId.HasValue)
            .GroupBy(x => x.ArticleId!.Value).Any(g => g.Count() > 1))
            throw new GoodsReceiptException("duplicate_existing_article", "Potvrdite da su odvojene stavke istog artikla namerne.");
        if (request.Lines.Where(x => x.ArticleId.HasValue).GroupBy(x => x.ArticleId!.Value)
            .Any(g => g.Sum(x => (long)x.Quantity) > int.MaxValue))
            throw new GoodsReceiptException("quantity_overflow", "Zbir količina za isti artikal prelazi dozvoljeni opseg.");
    }

    private static bool ValidMoney(decimal? value) => value.HasValue && value.Value >= 0 &&
        value.Value <= MaxNumeric18Scale2 && decimal.Round(value.Value, 2) == value.Value;

    private static string CalculateRequestHash(GoodsReceiptRequest request)
    {
        var canonical = new
        {
            supplierId = request.SupplierId,
            storeId = request.StoreId,
            documentType = request.DocumentType.Trim(),
            documentNumber = request.DocumentNumber.Trim(),
            documentDate = request.DocumentDate.ToString("yyyy-MM-dd"),
            receivedDate = request.ReceivedDate.ToString("yyyy-MM-dd"),
            currency = request.Currency,
            comment = request.Comment?.Trim(),
            lines = request.Lines.Select(x => new
            {
                articleId = x.ArticleId,
                code = x.ArticleCode?.Trim(),
                name = x.ArticleName?.Trim(),
                quantity = x.Quantity,
                purchaseUnitPrice = x.PurchaseUnitPrice,
                salesUnitPrice = x.SalesUnitPrice,
                shoeTypeId = x.ShoeTypeId,
                seasonId = x.SeasonId,
                comment = x.Comment?.Trim()
            })
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical))));
    }

    private static async Task<Dictionary<int, ExistingArticle>> LockAndValidateExistingArticlesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, GoodsReceiptRequest request, CancellationToken ct)
    {
        var articles = new Dictionary<int, ExistingArticle>();
        foreach (var articleId in request.Lines.Where(x => x.ArticleId.HasValue)
                     .Select(x => x.ArticleId!.Value).Distinct().Order())
        {
            await using var command = new NpgsqlCommand("""
                SELECT "Id", "Naziv", "PLU", "DataOrigin", "IDObjekat", "Kolicina"
                FROM "Artikli" WHERE "Id" = @id FOR UPDATE
                """, connection, transaction);
            Add(command, "id", articleId, NpgsqlDbType.Integer);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                throw new GoodsReceiptException("unknown_article", $"Artikal sa ID {articleId} nije pronađen.", 422);
            var article = new ExistingArticle(reader.GetInt32(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4), reader.IsDBNull(5) ? null : reader.GetInt32(5));
            await reader.DisposeAsync();
            if (article.StoreId != request.StoreId)
                throw new GoodsReceiptException("article_store_mismatch", $"Artikal {articleId} nije vezan za izabranu lokaciju. Izaberite artikal iz te prodavnice ili skladišta.", 422);
            if (!article.Quantity.HasValue)
                throw new GoodsReceiptException("unknown_stock_balance", $"Početna zaliha artikla {articleId} nije poznata; prijem je blokiran da se ne bi proizvela netačna količina.", 422);
            articles[articleId] = article;
        }
        return articles;
    }

    private static async Task ValidateNewArticleReferencesAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, GoodsReceiptRequest request, CancellationToken ct)
    {
        foreach (var id in request.Lines.Where(x => !x.ArticleId.HasValue).Select(x => x.ShoeTypeId).Where(x => x.HasValue).Select(x => x!.Value).Distinct())
        {
            if (!await ScalarBoolAsync(connection, transaction, "SELECT EXISTS (SELECT 1 FROM \"TipoviObuce\" WHERE \"Id\" = @id)", ct, ("id", id)))
                throw new GoodsReceiptException("unknown_shoe_type", $"Tip obuće {id} ne postoji.", 422);
        }
        foreach (var id in request.Lines.Where(x => !x.ArticleId.HasValue).Select(x => x.SeasonId).Where(x => x.HasValue).Select(x => x!.Value).Distinct())
        {
            if (!await ScalarBoolAsync(connection, transaction, "SELECT EXISTS (SELECT 1 FROM \"Sezone\" WHERE \"Id\" = @id)", ct, ("id", id)))
                throw new GoodsReceiptException("unknown_season", $"Sezona {id} ne postoji.", 422);
        }

        var newLines = request.Lines.Where(x => !x.ArticleId.HasValue)
            .OrderBy(x => string.IsNullOrWhiteSpace(x.ArticleCode)
                ? $"name:{request.StoreId}:{request.SupplierId}:{x.ShoeTypeId}:{x.SeasonId}:{x.ArticleName?.Trim().ToUpperInvariant()}"
                : $"code:{request.StoreId}:{x.ArticleCode!.Trim().ToUpperInvariant()}", StringComparer.Ordinal);
        foreach (var line in newLines)
        {
            var hasCode = !string.IsNullOrWhiteSpace(line.ArticleCode);
            var normalizedCode = line.ArticleCode?.Trim().ToUpperInvariant();
            var normalizedName = line.ArticleName!.Trim().ToUpperInvariant();
            var identityKey = hasCode
                ? $"goods-receipt-article-code|{request.StoreId}|{normalizedCode}"
                : $"goods-receipt-article-name|{request.StoreId}|{request.SupplierId}|{line.ShoeTypeId}|{line.SeasonId}|{normalizedName}";
            await using (var identityLock = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtextextended(@identityKey, 0))", connection, transaction))
            {
                Add(identityLock, "identityKey", identityKey, NpgsqlDbType.Text);
                await identityLock.ExecuteScalarAsync(ct);
            }

            var duplicate = hasCode
                ? await ScalarBoolAsync(connection, transaction, """
                    SELECT EXISTS (
                        SELECT 1 FROM "Artikli"
                        WHERE "IDObjekat" = @store AND upper(btrim(COALESCE("PLU", ''))) = @code)
                    """, ct, ("store", request.StoreId), ("code", normalizedCode))
                : await ScalarBoolAsync(connection, transaction, """
                    SELECT EXISTS (
                        SELECT 1 FROM "Artikli"
                        WHERE "IDObjekat" = @store AND "IDDobavljac" = @supplier
                          AND "IDTipObuce" = @shoeType AND "IDSezona" = @season
                          AND upper(btrim("Naziv")) = @name)
                    """, ct, ("store", request.StoreId), ("supplier", request.SupplierId),
                    ("shoeType", line.ShoeTypeId), ("season", line.SeasonId), ("name", normalizedName));
            if (duplicate)
                throw new GoodsReceiptException("article_already_exists", hasCode
                    ? $"Šifra artikla {line.ArticleCode!.Trim()} već postoji na izabranoj lokaciji. Izaberite postojeći artikal umesto novog unosa."
                    : $"Artikal „{line.ArticleName!.Trim()}” već postoji za dobavljača i lokaciju. Izaberite postojeći artikal ili proverite šifru.", 409);
        }
    }

    private static async Task<ExistingArticle> InsertNewArticleAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, GoodsReceiptRequest request,
        GoodsReceiptLineRequest line, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO "Artikli" ("PLU", "Naziv", "IDDobavljac", "IDTipObuce", "IDObjekat", "IDSezona",
                "Kolicina", "NabavnaCena", "ProdajnaCena", "Komentar", "DataOrigin", "UpdatedAt")
            VALUES (@code, @name, @supplier, @shoeType, @store, @season, @quantity, NULL, NULL, @comment, 'app', NOW())
            RETURNING "Id", "Naziv", "PLU", "DataOrigin", "IDObjekat", "Kolicina"
            """, connection, transaction);
        Add(command, "code", line.ArticleCode?.Trim(), NpgsqlDbType.Varchar);
        Add(command, "name", line.ArticleName!.Trim(), NpgsqlDbType.Varchar);
        Add(command, "supplier", request.SupplierId, NpgsqlDbType.Integer);
        Add(command, "shoeType", line.ShoeTypeId, NpgsqlDbType.Integer);
        Add(command, "store", request.StoreId, NpgsqlDbType.Integer);
        Add(command, "season", line.SeasonId, NpgsqlDbType.Integer);
        Add(command, "quantity", line.Quantity, NpgsqlDbType.Integer);
        Add(command, "comment", string.IsNullOrWhiteSpace(line.Comment) ? null : line.Comment.Trim(), NpgsqlDbType.Varchar);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new ExistingArticle(reader.GetInt32(0), reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetInt32(4), reader.IsDBNull(5) ? null : reader.GetInt32(5));
    }

    private static async Task<(long Id, DateTime CreatedAtUtc)> InsertReceiptHeaderAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, GoodsReceiptRequest request,
        string requestHash, string createdBy, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO "GoodsReceipts" ("RequestKey", "RequestHash", "SupplierId", "DocumentType", "DocumentNumber",
                "DocumentDate", "ReceivedDate", "StoreId", "Currency", "Comment", "CreatedBy")
            VALUES (@requestKey, @requestHash, @supplier, @type, @number, @documentDate, @receivedDate,
                @store, @currency, @comment, @createdBy)
            RETURNING "Id", "CreatedAtUtc"
            """, connection, transaction);
        Add(command, "requestKey", request.RequestKey, NpgsqlDbType.Uuid);
        Add(command, "requestHash", requestHash, NpgsqlDbType.Varchar);
        Add(command, "supplier", request.SupplierId, NpgsqlDbType.Integer);
        Add(command, "type", request.DocumentType, NpgsqlDbType.Varchar);
        Add(command, "number", request.DocumentNumber.Trim(), NpgsqlDbType.Varchar);
        Add(command, "documentDate", request.DocumentDate, NpgsqlDbType.Date);
        Add(command, "receivedDate", request.ReceivedDate, NpgsqlDbType.Date);
        Add(command, "store", request.StoreId, NpgsqlDbType.Integer);
        Add(command, "currency", request.Currency, NpgsqlDbType.Char);
        Add(command, "comment", request.Comment?.Trim(), NpgsqlDbType.Varchar);
        Add(command, "createdBy", createdBy, NpgsqlDbType.Varchar);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return (reader.GetInt64(0), reader.GetDateTime(1));
    }

    private static async Task<long> InsertReceiptLineAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, long headerId, int lineNumber,
        ExistingArticle article, GoodsReceiptLineRequest line, string currency, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO "GoodsReceiptLines" ("GoodsReceiptId", "LineNumber", "ArticleId", "ArticleNameSnapshot",
                "ArticleCodeSnapshot", "ArticleOriginSnapshot", "Quantity", "PurchaseUnitPrice", "SalesUnitPrice", "Currency", "Comment")
            VALUES (@header, @lineNumber, @article, @name, @code, @origin, @quantity, @purchase, @sales, @currency, @comment)
            RETURNING "Id"
            """, connection, transaction);
        Add(command, "header", headerId, NpgsqlDbType.Bigint);
        Add(command, "lineNumber", lineNumber, NpgsqlDbType.Integer);
        Add(command, "article", article.Id, NpgsqlDbType.Integer);
        Add(command, "name", article.Name, NpgsqlDbType.Varchar);
        Add(command, "code", article.Code, NpgsqlDbType.Varchar);
        Add(command, "origin", article.DataOrigin, NpgsqlDbType.Varchar);
        Add(command, "quantity", line.Quantity, NpgsqlDbType.Integer);
        Add(command, "purchase", line.PurchaseUnitPrice, NpgsqlDbType.Numeric);
        Add(command, "sales", line.SalesUnitPrice, NpgsqlDbType.Numeric);
        Add(command, "currency", currency, NpgsqlDbType.Char);
        Add(command, "comment", string.IsNullOrWhiteSpace(line.Comment) ? null : line.Comment.Trim(), NpgsqlDbType.Varchar);
        return (long)(await command.ExecuteScalarAsync(ct))!;
    }

    private static async Task<int> InsertReceiptMovementAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, GoodsReceiptRequest request, long lineId,
        int articleId, GoodsReceiptLineRequest line, string createdBy, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO "DnevnikPromena" ("TipPromene", "Datum", "Iznos", "BrojRacuna", "DobavljacId", "ArtikalId",
                "Kolicina", "IDObjekat", "Komentar", "KorisnikIme", "DataOrigin", "GoodsReceiptLineId")
            VALUES (@type, (@receivedDate::date::timestamp AT TIME ZONE 'UTC'), @amount, @number, @supplier, @article,
                @quantity, @store, @comment, @actor, 'app', @lineId)
            RETURNING "Id"
            """, connection, transaction);
        Add(command, "type", TipPromeneConstants.UlazRobe, NpgsqlDbType.Varchar);
        Add(command, "receivedDate", request.ReceivedDate, NpgsqlDbType.Date);
        Add(command, "amount", decimal.Round(line.Quantity * line.PurchaseUnitPrice!.Value, 2), NpgsqlDbType.Numeric);
        Add(command, "number", request.DocumentNumber.Trim(), NpgsqlDbType.Varchar);
        Add(command, "supplier", request.SupplierId, NpgsqlDbType.Integer);
        Add(command, "article", articleId, NpgsqlDbType.Integer);
        Add(command, "quantity", line.Quantity, NpgsqlDbType.Integer);
        Add(command, "store", request.StoreId, NpgsqlDbType.Integer);
        Add(command, "comment", string.IsNullOrWhiteSpace(line.Comment) ? request.Comment?.Trim() : line.Comment.Trim(), NpgsqlDbType.Varchar);
        Add(command, "actor", createdBy, NpgsqlDbType.Varchar);
        Add(command, "lineId", lineId, NpgsqlDbType.Bigint);
        return (int)(await command.ExecuteScalarAsync(ct))!;
    }

    private async Task<StoredReceipt?> FindReceiptByRequestKeyAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid requestKey, CancellationToken ct)
    {
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT r."Id", r."RequestHash", r."Status", r."RequestKey", r."SupplierId", COALESCE(s."Naziv", ''),
                   r."StoreId", r."DocumentType", r."DocumentNumber", r."DocumentDate", r."ReceivedDate",
                   r."Currency", r."Comment", r."CreatedBy", r."CreatedAtUtc",
                   COUNT(l."Id")::integer, COALESCE(SUM(l."Quantity"), 0)::integer,
                   COALESCE(SUM(l."Quantity" * l."PurchaseUnitPrice"), 0)::numeric
            FROM "GoodsReceipts" r JOIN "Dobavljaci" s ON s."Id" = r."SupplierId"
            LEFT JOIN "GoodsReceiptLines" l ON l."GoodsReceiptId" = r."Id"
            WHERE r."RequestKey" = @key GROUP BY r."Id", s."Naziv"
            """, connection, transaction);
        Add(command, "key", requestKey, NpgsqlDbType.Uuid);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new StoredReceipt(reader.GetString(1), ReadReceiptSummary(reader, hasHash: true));
    }

    private static async Task<GoodsReceiptResponse?> FindReceiptByIdAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, long id, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT r."Id", r."Status", r."RequestKey", r."SupplierId", COALESCE(s."Naziv", ''),
                   r."StoreId", r."DocumentType", r."DocumentNumber", r."DocumentDate", r."ReceivedDate",
                   r."Currency", r."Comment", r."CreatedBy", r."CreatedAtUtc",
                   COUNT(l."Id")::integer, COALESCE(SUM(l."Quantity"), 0)::integer,
                   COALESCE(SUM(l."Quantity" * l."PurchaseUnitPrice"), 0)::numeric
            FROM "GoodsReceipts" r JOIN "Dobavljaci" s ON s."Id" = r."SupplierId"
            LEFT JOIN "GoodsReceiptLines" l ON l."GoodsReceiptId" = r."Id"
            WHERE r."Id" = @id GROUP BY r."Id", s."Naziv"
            """, connection, transaction);
        Add(command, "id", id, NpgsqlDbType.Bigint);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadReceiptSummary(reader) : null;
    }

    private static async Task<long?> FindSimilarReceiptIdAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, GoodsReceiptRequest request, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT "Id" FROM "GoodsReceipts"
            WHERE "SupplierId" = @supplier AND "DocumentType" = @type
              AND lower(btrim("DocumentNumber")) = lower(btrim(@number))
              AND "StoreId" = @store AND "Id" <> COALESCE((
                  SELECT "Id" FROM "GoodsReceipts" WHERE "RequestKey" = @requestKey), -1)
            ORDER BY "CreatedAtUtc" DESC LIMIT 1
            """, connection, transaction);
        Add(command, "supplier", request.SupplierId, NpgsqlDbType.Integer);
        Add(command, "type", request.DocumentType, NpgsqlDbType.Varchar);
        Add(command, "number", request.DocumentNumber.Trim(), NpgsqlDbType.Varchar);
        Add(command, "store", request.StoreId, NpgsqlDbType.Integer);
        Add(command, "requestKey", request.RequestKey, NpgsqlDbType.Uuid);
        var result = await command.ExecuteScalarAsync(ct);
        return result is null or DBNull ? null : (long)result;
    }

    private static async Task AcquireDocumentLockAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, GoodsReceiptRequest request, CancellationToken ct)
    {
        var lockKey = string.Join('|', request.SupplierId, request.StoreId, request.DocumentType.Trim().ToLowerInvariant(),
            request.DocumentNumber.Trim().ToUpperInvariant());
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@documentKey, 0))", connection, transaction);
        Add(command, "documentKey", lockKey, NpgsqlDbType.Text);
        await command.ExecuteScalarAsync(ct);
    }

    private static GoodsReceiptResponse ReadReceiptSummary(NpgsqlDataReader reader, bool hasHash = false)
    {
        var offset = hasHash ? 2 : 1;
        var result = new GoodsReceiptResponse(
            reader.GetInt64(0), reader.GetString(offset), reader.GetGuid(offset + 1), reader.GetInt32(offset + 2),
            reader.GetString(offset + 3), reader.GetInt32(offset + 4), reader.GetString(offset + 5), reader.GetString(offset + 6),
            DateOnly.FromDateTime(reader.GetDateTime(offset + 7)), DateOnly.FromDateTime(reader.GetDateTime(offset + 8)),
            reader.GetString(offset + 9), reader.IsDBNull(offset + 10) ? null : reader.GetString(offset + 10),
            reader.GetString(offset + 11), reader.GetDateTime(offset + 12), reader.GetInt32(offset + 13),
            reader.GetInt32(offset + 14), reader.GetDecimal(offset + 15), []);
        return result;
    }

    private static async Task<bool> ScalarBoolAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string sql, CancellationToken ct, params (string Name, object? Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        AddParameters(command, parameters);
        return (bool)(await command.ExecuteScalarAsync(ct))!;
    }

    private static async Task<string?> ScalarStringAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string sql, CancellationToken ct, params (string Name, object? Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        AddParameters(command, parameters);
        return await command.ExecuteScalarAsync(ct) as string;
    }

    private static async Task<int> ScalarIntAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string sql, CancellationToken ct, params (string Name, object? Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        AddParameters(command, parameters);
        var value = await command.ExecuteScalarAsync(ct);
        return value is null or DBNull ? 0 : (int)value;
    }

    private static void AddParameters(NpgsqlCommand command, params (string Name, object? Value)[] parameters)
    {
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private static void Add<T>(NpgsqlCommand command, string name, T? value, NpgsqlDbType type)
    {
        command.Parameters.Add(new NpgsqlParameter(name, type) { Value = value is null ? DBNull.Value : value });
    }

    private sealed record ExistingArticle(int Id, string Name, string? Code, string DataOrigin, int? StoreId, int? Quantity);
    private sealed record StoredReceipt(string RequestHash, GoodsReceiptResponse Receipt);
}
