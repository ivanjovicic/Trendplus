using System;
using System.Data;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.Dtos;
using Domain.Model;
using Domain.Transfers;
using Infrastructure.DbContexts;
using Infrastructure.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
using Infrastructure.Logging;

namespace Api.Services
{
    public interface ITransferService
    {
        Task<TransferResponse> CreateAsync(TransferCreateRequest req, string userId, CancellationToken ct = default);
        Task<TransferResponse> CreateDraftAsync(TransferCreateRequest req, string userId, CancellationToken ct = default);
        Task<TransferResponse> UpdateDraftAsync(long id, TransferUpdateRequest req, string userId, CancellationToken ct = default);
        Task<TransferResponse> ConfirmAsync(long id, string userId, CancellationToken ct = default);
        Task<TransferResponse> CompleteAsync(long id, string userId, CancellationToken ct = default);
        Task<TransferResponse> CancelAsync(long id, string userId, CancellationToken ct = default);
        Task<TransferResponse?> GetAsync(long id, CancellationToken ct = default);
        Task<TransferListResponse> ListAsync(
            int pageNumber,
            int pageSize,
            string? status,
            string? actor,
            string? createdBy,
            string? updatedBy,
            CancellationToken ct = default);
    }

    public sealed class TransferService : ITransferService
    {
        private static readonly SemaphoreSlim TransferSchemaBootstrapLock = new(1, 1);
        private static volatile bool _transferSchemaBootstrapCompleted;
        private readonly TrendplusDbContext _db;
        private readonly ILogger<TransferService> _logger;

        public TransferService(TrendplusDbContext db, ILogger<TransferService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public Task<TransferResponse> CreateAsync(TransferCreateRequest req, string userId, CancellationToken ct = default)
            => CreateDraftAsync(req, userId, ct);

        public async Task<TransferResponse> CreateDraftAsync(TransferCreateRequest req, string userId, CancellationToken ct = default)
        {
            await EnsureTransferSchemaIfNeededAsync(ct);

            ValidateCreateOrUpdateRequest(req.SourceId, req.DestinationId, req.Items);
            var normalizedLines = NormalizeLines(req.Items);
            var now = DateTimeOffset.UtcNow;

            await using var tx = await BeginTransactionAsync(ct);

            var transfer = new Transfer
            {
                SourceId = req.SourceId,
                DestinationId = req.DestinationId,
                Reserve = req.Reserve,
                Notes = req.Notes?.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = userId,
                UpdatedBy = userId,
                Status = TransferStatuses.Draft,
                Items = normalizedLines.Select(x => new TransferItem
                {
                    SkuId = x.SkuId,
                    Quantity = x.Quantity,
                    ReservedQuantity = 0m,
                    ProcessedQuantity = 0m,
                    Unit = x.Unit
                }).ToList()
            };

            _db.Transfers.Add(transfer);
            await _db.SaveChangesAsync(ct);
            if (tx is not null)
                await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Transfer draft created. TransferId={TransferId} SourceId={SourceId} DestinationId={DestinationId} Lines={LineCount} TotalQty={TotalQty} User={UserId}",
                transfer.Id,
                transfer.SourceId,
                transfer.DestinationId,
                transfer.Items.Count,
                transfer.Items.Sum(x => x.Quantity),
                userId);

            return await BuildResponseAsync(transfer, ct);
        }

        public async Task<TransferResponse> UpdateDraftAsync(long id, TransferUpdateRequest req, string userId, CancellationToken ct = default)
        {
            await EnsureTransferSchemaIfNeededAsync(ct);

            ArgumentNullException.ThrowIfNull(req);
            if (req.Items == null || req.Items.Count == 0)
                throw new ArgumentException("Transfer mora da ima bar jednu stavku.", nameof(req));

            var normalizedLines = NormalizeLines(req.Items);
            var now = DateTimeOffset.UtcNow;

            await using var tx = await BeginTransactionAsync(ct);
            await LockTransferRowAsync(id, ct);

            var transfer = await _db.Transfers
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == id, ct)
                ?? throw new InvalidOperationException($"Transfer {id} nije pronadjen.");

            if (!string.Equals(transfer.Status, TransferStatuses.Draft, StringComparison.Ordinal))
                throw new InvalidOperationException($"Transfer {id} nije u draft statusu i ne moze se menjati.");

            transfer.Reserve = req.Reserve;
            transfer.Notes = req.Notes?.Trim();
            transfer.UpdatedAt = now;
            transfer.UpdatedBy = userId;

            var existing = transfer.Items.ToDictionary(x => x.SkuId);
            var incomingSkuIds = normalizedLines.Select(x => x.SkuId).ToHashSet();

            foreach (var toRemove in transfer.Items.Where(x => !incomingSkuIds.Contains(x.SkuId)).ToList())
            {
                _db.TransferItems.Remove(toRemove);
            }

            foreach (var line in normalizedLines)
            {
                if (existing.TryGetValue(line.SkuId, out var item))
                {
                    item.Quantity = line.Quantity;
                    item.Unit = line.Unit;
                    item.ReservedQuantity = 0m;
                    item.ProcessedQuantity = 0m;
                }
                else
                {
                    transfer.Items.Add(new TransferItem
                    {
                        SkuId = line.SkuId,
                        Quantity = line.Quantity,
                        ReservedQuantity = 0m,
                        ProcessedQuantity = 0m,
                        Unit = line.Unit
                    });
                }
            }

            var existingReservations = await _db.StockReservations
                .Where(x => x.TransferId == transfer.Id)
                .ToListAsync(ct);
            if (existingReservations.Count > 0)
                _db.StockReservations.RemoveRange(existingReservations);

            await _db.SaveChangesAsync(ct);
            if (tx is not null)
                await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Transfer draft updated. TransferId={TransferId} Lines={LineCount} TotalQty={TotalQty} User={UserId}",
                transfer.Id,
                transfer.Items.Count,
                transfer.Items.Sum(x => x.Quantity),
                userId);

            return await BuildResponseAsync(transfer, ct);
        }

        public async Task<TransferResponse> ConfirmAsync(long id, string userId, CancellationToken ct = default)
        {
            await EnsureTransferSchemaIfNeededAsync(ct);

            var sw = Stopwatch.StartNew();
            var now = DateTimeOffset.UtcNow;

            await using var tx = await BeginTransactionAsync(ct);
            await LockTransferRowAsync(id, ct);

            var transfer = await _db.Transfers
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == id, ct)
                ?? throw new InvalidOperationException($"Transfer {id} nije pronadjen.");

            if (string.Equals(transfer.Status, TransferStatuses.Completed, StringComparison.Ordinal) ||
                string.Equals(transfer.Status, TransferStatuses.Confirmed, StringComparison.Ordinal))
            {
                if (tx is not null)
                    await tx.CommitAsync(ct);
                return await BuildResponseAsync(transfer, ct);
            }

            if (string.Equals(transfer.Status, TransferStatuses.Cancelled, StringComparison.Ordinal))
                throw new InvalidOperationException($"Transfer {id} je otkazan i ne moze se potvrditi.");

            ValidateTransferHasItems(transfer);

            var stockBySku = await LoadAndLockSourceStockAsync(transfer, ct);
            foreach (var item in transfer.Items)
            {
                if (!stockBySku.TryGetValue(item.SkuId, out var src))
                    throw new InvalidOperationException($"Artikal {item.SkuId} ne postoji.");
                if ((src.IDObjekat ?? 0) != transfer.SourceId)
                    throw new InvalidOperationException($"Artikal {item.SkuId} nije vezan za source radnju {transfer.SourceId}.");

                var available = Convert.ToDecimal(src.Kolicina ?? 0);
                if (available < item.Quantity)
                    throw new InvalidOperationException($"Nedovoljna kolicina za artikal {item.SkuId}. Dostupno={available}, trazeno={item.Quantity}.");
            }

            var existingReservations = await _db.StockReservations
                .Where(x => x.TransferId == transfer.Id)
                .ToListAsync(ct);
            if (existingReservations.Count > 0)
                _db.StockReservations.RemoveRange(existingReservations);

            if (transfer.Reserve)
            {
                var reservations = transfer.Items.Select(i => new StockReservation
                {
                    TransferId = transfer.Id,
                    SkuId = i.SkuId,
                    Quantity = i.Quantity,
                    CreatedAt = now
                }).ToList();
                _db.StockReservations.AddRange(reservations);
            }

            foreach (var item in transfer.Items)
                item.ReservedQuantity = transfer.Reserve ? item.Quantity : 0m;

            transfer.Status = TransferStatuses.Confirmed;
            transfer.ConfirmedAt = now;
            transfer.UpdatedAt = now;
            transfer.UpdatedBy = userId;

            await _db.SaveChangesAsync(ct);
            if (tx is not null)
                await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Transfer confirmed. TransferId={TransferId} Lines={LineCount} Reserve={Reserve} DurationMs={DurationMs}",
                transfer.Id,
                transfer.Items.Count,
                transfer.Reserve,
                sw.ElapsedMilliseconds);

            return await BuildResponseAsync(transfer, ct);
        }

        public async Task<TransferResponse> CompleteAsync(long id, string userId, CancellationToken ct = default)
        {
            await EnsureTransferSchemaIfNeededAsync(ct);

            var sw = Stopwatch.StartNew();
            var now = DateTimeOffset.UtcNow;

            await using var tx = await BeginTransactionAsync(ct);
            await LockTransferRowAsync(id, ct);

            var transfer = await _db.Transfers
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == id, ct)
                ?? throw new InvalidOperationException($"Transfer {id} nije pronadjen.");

            if (string.Equals(transfer.Status, TransferStatuses.Completed, StringComparison.Ordinal))
            {
                if (tx is not null)
                    await tx.CommitAsync(ct);
                return await BuildResponseAsync(transfer, ct);
            }

            if (string.Equals(transfer.Status, TransferStatuses.Cancelled, StringComparison.Ordinal))
                throw new InvalidOperationException($"Transfer {id} je otkazan i ne moze se zavrsiti.");

            if (!string.Equals(transfer.Status, TransferStatuses.Confirmed, StringComparison.Ordinal))
                throw new InvalidOperationException($"Transfer {id} mora prvo da bude potvrden pre zavrsetka.");

            ValidateTransferHasItems(transfer);

            var sourceStockBySku = await LoadAndLockSourceStockAsync(transfer, ct);
            var destinationByKey = await LoadDestinationStockByKeyAsync(transfer, sourceStockBySku, ct);
            var movementEntries = new List<DnevnikPromena>(transfer.Items.Count * 2);
            var transferDocument = BuildTransferDocumentCode(transfer.Id);

            foreach (var item in transfer.Items)
            {
                if (!sourceStockBySku.TryGetValue(item.SkuId, out var sourceArticle))
                    throw new InvalidOperationException($"Artikal {item.SkuId} ne postoji.");

                var sourceQty = Convert.ToDecimal(sourceArticle.Kolicina ?? 0);
                if (sourceQty < item.Quantity)
                    throw new InvalidOperationException($"Nedovoljna kolicina za artikal {item.SkuId}. Dostupno={sourceQty}, trazeno={item.Quantity}.");

                var destinationArticle = await GetOrCreateDestinationArticleAsync(
                    transfer,
                    sourceArticle,
                    destinationByKey,
                    userId,
                    ct);

                sourceArticle.Kolicina = Convert.ToInt32(sourceQty - item.Quantity);
                var destinationQty = Convert.ToDecimal(destinationArticle.Kolicina ?? 0);
                destinationArticle.Kolicina = Convert.ToInt32(destinationQty + item.Quantity);

                item.ProcessedQuantity = item.Quantity;
                if (transfer.Reserve)
                    item.ReservedQuantity = 0m;

                var lineAmount = Math.Abs((sourceArticle.NabavnaCena ?? 0m) * item.Quantity);
                movementEntries.Add(new DnevnikPromena
                {
                    TipPromene = TipPromeneConstants.PrenosIzlaz,
                    Datum = now.UtcDateTime,
                    ArtikalId = sourceArticle.Id,
                    Kolicina = -Convert.ToInt32(item.Quantity),
                    Iznos = lineAmount,
                    IDObjekat = Convert.ToInt32(transfer.SourceId),
                    BrojRacuna = transferDocument,
                    Komentar = $"Transfer {transfer.Id} OUT {transfer.SourceId}->{transfer.DestinationId}",
                    KorisnikIme = userId,
                    DataOrigin = "app"
                });
                movementEntries.Add(new DnevnikPromena
                {
                    TipPromene = TipPromeneConstants.PrenosUlaz,
                    Datum = now.UtcDateTime,
                    ArtikalId = destinationArticle.Id,
                    Kolicina = Convert.ToInt32(item.Quantity),
                    Iznos = lineAmount,
                    IDObjekat = Convert.ToInt32(transfer.DestinationId),
                    BrojRacuna = transferDocument,
                    Komentar = $"Transfer {transfer.Id} IN {transfer.SourceId}->{transfer.DestinationId}",
                    KorisnikIme = userId,
                    DataOrigin = "app"
                });
            }

            var reservations = await _db.StockReservations
                .Where(x => x.TransferId == transfer.Id)
                .ToListAsync(ct);
            if (reservations.Count > 0)
                _db.StockReservations.RemoveRange(reservations);

            _db.DnevnikPromena.AddRange(movementEntries);

            transfer.Status = TransferStatuses.Completed;
            transfer.CompletedAt = now;
            transfer.UpdatedAt = now;
            transfer.UpdatedBy = userId;

            await _db.SaveChangesAsync(ct);
            if (tx is not null)
                await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Transfer completed. TransferId={TransferId} Lines={LineCount} Movements={MovementCount} DurationMs={DurationMs}",
                transfer.Id,
                transfer.Items.Count,
                movementEntries.Count,
                sw.ElapsedMilliseconds);

            return await BuildResponseAsync(transfer, ct);
        }

        public async Task<TransferResponse> CancelAsync(long id, string userId, CancellationToken ct = default)
        {
            await EnsureTransferSchemaIfNeededAsync(ct);

            var now = DateTimeOffset.UtcNow;
            await using var tx = await BeginTransactionAsync(ct);
            await LockTransferRowAsync(id, ct);

            var transfer = await _db.Transfers
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == id, ct)
                ?? throw new InvalidOperationException($"Transfer {id} nije pronadjen.");

            if (string.Equals(transfer.Status, TransferStatuses.Completed, StringComparison.Ordinal))
                throw new InvalidOperationException($"Transfer {id} je vec zavrsen i ne moze se otkazati.");

            if (string.Equals(transfer.Status, TransferStatuses.Cancelled, StringComparison.Ordinal))
            {
                if (tx is not null)
                    await tx.CommitAsync(ct);
                return await BuildResponseAsync(transfer, ct);
            }

            var reservations = await _db.StockReservations
                .Where(x => x.TransferId == transfer.Id)
                .ToListAsync(ct);
            if (reservations.Count > 0)
                _db.StockReservations.RemoveRange(reservations);

            foreach (var item in transfer.Items)
                item.ReservedQuantity = 0m;

            transfer.Status = TransferStatuses.Cancelled;
            transfer.CancelledAt = now;
            transfer.UpdatedAt = now;
            transfer.UpdatedBy = userId;

            await _db.SaveChangesAsync(ct);
            if (tx is not null)
                await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Transfer cancelled. TransferId={TransferId} Lines={LineCount}",
                transfer.Id,
                transfer.Items.Count);

            return await BuildResponseAsync(transfer, ct);
        }

        public async Task<TransferResponse?> GetAsync(long id, CancellationToken ct = default)
        {
            await EnsureTransferSchemaIfNeededAsync(ct);

            var transfer = await _db.Transfers
                .AsNoTracking()
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == id, ct);

            if (transfer is null) return null;
            return await BuildResponseAsync(transfer, ct);
        }

        public async Task<TransferListResponse> ListAsync(
            int pageNumber,
            int pageSize,
            string? status,
            string? actor,
            string? createdBy,
            string? updatedBy,
            CancellationToken ct = default)
        {
            await EnsureTransferSchemaIfNeededAsync(ct);

            pageNumber = Math.Max(pageNumber, 1);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = _db.Transfers.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim().ToLowerInvariant();
                query = query.Where(x => x.Status == normalized);
            }
            if (!string.IsNullOrWhiteSpace(actor))
            {
                var normalized = actor.Trim();
                query = query.Where(x => x.CreatedBy == normalized || x.UpdatedBy == normalized);
            }
            if (!string.IsNullOrWhiteSpace(createdBy))
            {
                var normalized = createdBy.Trim();
                query = query.Where(x => x.CreatedBy == normalized);
            }
            if (!string.IsNullOrWhiteSpace(updatedBy))
            {
                var normalized = updatedBy.Trim();
                query = query.Where(x => x.UpdatedBy == normalized);
            }

            var pageQuery = query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new TransferListItemProjection
                {
                    Id = x.Id,
                    Status = x.Status,
                    SourceId = x.SourceId,
                    DestinationId = x.DestinationId,
                    Reserve = x.Reserve,
                    Notes = x.Notes,
                    CreatedBy = x.CreatedBy,
                    UpdatedBy = x.UpdatedBy,
                    ItemCount = x.Items.Count,
                    TotalQuantity = x.Items.Sum(i => i.Quantity),
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    CompletedAt = x.CompletedAt
                });

            if (_db.Database.IsRelational())
            {
                _logger.LogDebug(
                    "Transfer list SQL (page={PageNumber}, size={PageSize}, status={Status}, actor={Actor}, createdBy={CreatedBy}, updatedBy={UpdatedBy}): {Sql}",
                    pageNumber,
                    pageSize,
                    status,
                    actor,
                    createdBy,
                    updatedBy,
                    pageQuery.ToQueryString());
            }

            int total;
            List<TransferListItemProjection> transfers;

            try
            {
                total = await query.CountAsync(ct);
                transfers = await pageQuery.ToListAsync(ct);
            }
            catch (PostgresException pex) when (IsMissingTransferSchemaRelation(pex))
            {
                _logger.LogWarning(
                    pex,
                    "Transfer schema relation missing during list query (SqlState={SqlState}, Table={Table}, Position={Position}). Triggering self-heal and retry.",
                    pex.SqlState,
                    pex.TableName,
                    pex.Position);

                await EnsureTransferSchemaIfNeededAsync(ct, force: true);

                try
                {
                    total = await query.CountAsync(ct);
                    transfers = await pageQuery.ToListAsync(ct);
                }
                catch (PostgresException retryEx) when (IsMissingTransferSchemaRelation(retryEx))
                {
                    _logger.LogWarning(
                        retryEx,
                        "Transfer schema still unavailable after self-heal retry. Returning empty transfer list.");

                    total = 0;
                    transfers = [];
                }
            }
            catch (PostgresException pex)
            {
                _logger.LogError(
                    pex,
                    "Postgres error fetching transfers: SqlState={SqlState} Detail={Detail} Hint={Hint} Table={Table} Constraint={ConstraintName} Position={Position}",
                    pex.SqlState,
                    pex.Detail,
                    pex.Hint,
                    pex.TableName,
                    pex.ConstraintName,
                    pex.Position);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching transfers");
                throw;
            }

            return new TransferListResponse
            {
                Items = transfers,
                TotalCount = total,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        private static void ValidateCreateOrUpdateRequest(long sourceId, long destinationId, IEnumerable<TransferLineInputDto> items)
        {
            if (sourceId <= 0 || destinationId <= 0)
                throw new ArgumentException("Source i destination radnje moraju biti validne.");
            if (sourceId == destinationId)
                throw new ArgumentException("Source i destination radnja ne mogu biti iste.");

            var list = items?.ToList() ?? [];
            if (list.Count == 0)
                throw new ArgumentException("Transfer mora da sadrzi bar jednu stavku.");
            if (list.Any(x => x.SkuId <= 0))
                throw new ArgumentException("Svaka stavka mora imati validan skuId.");
            if (list.Any(x => x.Quantity <= 0))
                throw new ArgumentException("Kolicina mora biti veca od nule.");
            if (list.Any(x => decimal.Truncate(x.Quantity) != x.Quantity))
                throw new ArgumentException("Kolicina mora biti ceo broj.");
        }

        private static void ValidateTransferHasItems(Transfer transfer)
        {
            if (transfer.Items.Count == 0)
                throw new InvalidOperationException($"Transfer {transfer.Id} nema stavke.");
            if (transfer.Items.Any(x => x.Quantity <= 0))
                throw new InvalidOperationException($"Transfer {transfer.Id} ima stavke sa nevalidnom kolicinom.");
        }

        private static List<TransferLineInputDto> NormalizeLines(IEnumerable<TransferLineInputDto> items)
        {
            return items
                .GroupBy(x => new { x.SkuId, Unit = (x.Unit ?? string.Empty).Trim() }, x => x.Quantity)
                .Select(g => new TransferLineInputDto
                {
                    SkuId = g.Key.SkuId,
                    Unit = string.IsNullOrWhiteSpace(g.Key.Unit) ? null : g.Key.Unit,
                    Quantity = g.Sum()
                })
                .Where(x => x.Quantity > 0)
                .ToList();
        }

        private static string BuildSkuKey(Artikli article)
        {
            if (!string.IsNullOrWhiteSpace(article.PLU))
                return $"plu:{article.PLU.Trim().ToLowerInvariant()}";

            return $"name:{(article.Naziv ?? string.Empty).Trim().ToLowerInvariant()}";
        }

        private static string BuildTransferDocumentCode(long transferId) => $"TR-{transferId}";

        private static bool IsMissingTransferSchemaRelation(PostgresException ex)
        {
            if (!string.Equals(ex.SqlState, PostgresErrorCodes.UndefinedTable, StringComparison.Ordinal))
                return false;

            if (string.Equals(ex.TableName, "Transfers", StringComparison.Ordinal)
                || string.Equals(ex.TableName, "TransferItems", StringComparison.Ordinal)
                || string.Equals(ex.TableName, "StockReservations", StringComparison.Ordinal))
                return true;

            var message = ex.MessageText ?? ex.Message ?? string.Empty;
            return message.Contains("relation \"Transfers\" does not exist", StringComparison.Ordinal)
                   || message.Contains("relation \"TransferItems\" does not exist", StringComparison.Ordinal)
                   || message.Contains("relation \"StockReservations\" does not exist", StringComparison.Ordinal);
        }

        private async Task EnsureTransferSchemaIfNeededAsync(CancellationToken ct, bool force = false)
        {
            if (!force && _transferSchemaBootstrapCompleted)
                return;

            if (!_db.Database.IsRelational())
            {
                _transferSchemaBootstrapCompleted = true;
                return;
            }

            await TransferSchemaBootstrapLock.WaitAsync(ct);
            try
            {
                if (!force && _transferSchemaBootstrapCompleted)
                    return;

                const string createSql = """
                    CREATE TABLE IF NOT EXISTS "Transfers" (
                        "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                        "Status" character varying(32) NOT NULL DEFAULT 'draft',
                        "SourceId" bigint NOT NULL,
                        "DestinationId" bigint NOT NULL,
                        "Reserve" boolean NOT NULL DEFAULT FALSE,
                        "Notes" character varying(2000),
                        "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                        "ConfirmedAt" timestamp with time zone NULL,
                        "CompletedAt" timestamp with time zone NULL,
                        "CancelledAt" timestamp with time zone NULL,
                        "CreatedBy" character varying(200),
                        "UpdatedBy" character varying(200)
                    );

                    CREATE TABLE IF NOT EXISTS "TransferItems" (
                        "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                        "SkuId" bigint NOT NULL,
                        "Quantity" numeric(18,4) NOT NULL,
                        "ReservedQuantity" numeric(18,4) NOT NULL DEFAULT 0,
                        "ProcessedQuantity" numeric(18,4) NOT NULL DEFAULT 0,
                        "Unit" character varying(32),
                        "TransferId" bigint NOT NULL,
                        CONSTRAINT "FK_TransferItems_Transfers_TransferId"
                            FOREIGN KEY ("TransferId")
                            REFERENCES "Transfers" ("Id")
                            ON DELETE CASCADE
                    );

                    CREATE TABLE IF NOT EXISTS "StockReservations" (
                        "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                        "TransferId" bigint NOT NULL,
                        "SkuId" bigint NOT NULL,
                        "Quantity" numeric(18,4) NOT NULL,
                        "ExpiresAt" timestamp with time zone NULL,
                        "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW()
                    );

                    ALTER TABLE IF EXISTS "Transfers" ADD COLUMN IF NOT EXISTS "Notes" character varying(2000);
                    ALTER TABLE IF EXISTS "Transfers" ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW();
                    ALTER TABLE IF EXISTS "Transfers" ADD COLUMN IF NOT EXISTS "ConfirmedAt" timestamp with time zone NULL;
                    ALTER TABLE IF EXISTS "Transfers" ADD COLUMN IF NOT EXISTS "CompletedAt" timestamp with time zone NULL;
                    ALTER TABLE IF EXISTS "Transfers" ADD COLUMN IF NOT EXISTS "CancelledAt" timestamp with time zone NULL;
                    ALTER TABLE IF EXISTS "Transfers" ADD COLUMN IF NOT EXISTS "UpdatedBy" character varying(200);

                    ALTER TABLE IF EXISTS "TransferItems" ADD COLUMN IF NOT EXISTS "ReservedQuantity" numeric(18,4) NOT NULL DEFAULT 0;
                    ALTER TABLE IF EXISTS "TransferItems" ADD COLUMN IF NOT EXISTS "ProcessedQuantity" numeric(18,4) NOT NULL DEFAULT 0;

                    CREATE INDEX IF NOT EXISTS "IX_Transfers_Status" ON "Transfers" ("Status");
                    CREATE INDEX IF NOT EXISTS "IX_Transfers_Source_Destination" ON "Transfers" ("SourceId", "DestinationId");
                    CREATE INDEX IF NOT EXISTS "IX_Transfers_CreatedAt" ON "Transfers" ("CreatedAt");
                    CREATE INDEX IF NOT EXISTS "IX_TransferItems_SkuId" ON "TransferItems" ("SkuId");
                    CREATE INDEX IF NOT EXISTS "IX_TransferItems_TransferId" ON "TransferItems" ("TransferId");
                    CREATE INDEX IF NOT EXISTS "IX_StockReservations_TransferId" ON "StockReservations" ("TransferId");
                    CREATE INDEX IF NOT EXISTS "IX_StockReservations_SkuId" ON "StockReservations" ("SkuId");
                    """;

                var sw = Stopwatch.StartNew();
                await _db.Database.ExecuteSqlRawAsync(createSql, ct);
                sw.Stop();
                try { SqlCommandLoggingHelper.LogSqlExecution("transfer", "ExecuteSqlRaw", createSql, null, sw.ElapsedMilliseconds, true, null, null, Application.Logging.RequestLogContext.Current.RequestId, Application.Logging.RequestLogContext.Current.TraceId); } catch { }
                _transferSchemaBootstrapCompleted = true;
            }
            catch (PostgresException pex)
            {
                _logger.LogWarning(
                    pex,
                    "Transfer schema self-heal failed. SqlState={SqlState} Detail={Detail} Hint={Hint} Table={Table} Constraint={ConstraintName}",
                    pex.SqlState,
                    pex.Detail,
                    pex.Hint,
                    pex.TableName,
                    pex.ConstraintName);
                throw;
            }
            finally
            {
                TransferSchemaBootstrapLock.Release();
            }
        }

        private bool IsNpgsqlProvider()
        {
            var providerName = _db.Database.ProviderName;
            return providerName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
        }

        private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken ct)
        {
            if (!_db.Database.IsRelational())
                return null;

            return await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        }

        private async Task LockTransferRowAsync(long id, CancellationToken ct)
        {
            if (!_db.Database.IsRelational())
                return;

            FormattableString lockSql = $@"SELECT 1 FROM ""Transfers"" WHERE ""Id"" = {id} FOR UPDATE";
            var sw = Stopwatch.StartNew();
            await _db.Database.ExecuteSqlInterpolatedAsync(lockSql, ct);
            sw.Stop();
            try { SqlCommandLoggingHelper.LogSqlExecution("transfer", "ExecuteSqlInterpolated", lockSql.Format, null, sw.ElapsedMilliseconds, true, null, null, Application.Logging.RequestLogContext.Current.RequestId, Application.Logging.RequestLogContext.Current.TraceId); } catch { }
        }

        private async Task<Dictionary<long, Artikli>> LoadAndLockSourceStockAsync(Transfer transfer, CancellationToken ct)
        {
            var skuIds = transfer.Items.Select(x => x.SkuId).Distinct().ToArray();
            var skuInt = skuIds.Select(x => checked((int)x)).Distinct().ToArray();
            if (skuInt.Length == 0)
                return new Dictionary<long, Artikli>();

            if (_db.Database.IsRelational())
            {
                foreach (var sku in skuInt)
                {
                    FormattableString lockSql = $@"SELECT 1 FROM ""Artikli"" WHERE ""Id"" = {sku} FOR UPDATE";
                    var sw = Stopwatch.StartNew();
                    await _db.Database.ExecuteSqlInterpolatedAsync(lockSql, ct);
                    sw.Stop();
                    try { SqlCommandLoggingHelper.LogSqlExecution("transfer", "ExecuteSqlInterpolated", lockSql.Format, null, sw.ElapsedMilliseconds, true, null, null, Application.Logging.RequestLogContext.Current.RequestId, Application.Logging.RequestLogContext.Current.TraceId); } catch { }
                }
            }

            var rows = await _db.Artikli
                .Where(x => skuInt.Contains(x.Id))
                .ToListAsync(ct);

            return rows.ToDictionary(x => (long)x.Id);
        }

        private async Task<Dictionary<string, Artikli>> LoadDestinationStockByKeyAsync(
            Transfer transfer,
            IReadOnlyDictionary<long, Artikli> sourceBySku,
            CancellationToken ct)
        {
            var sourceRows = sourceBySku.Values.ToList();
            var plus = sourceRows
                .Select(x => x.PLU)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var names = sourceRows
                .Where(x => string.IsNullOrWhiteSpace(x.PLU))
                .Select(x => x.Naziv)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var destinationRows = await _db.Artikli
                .Where(x =>
                    x.IDObjekat == (int?)transfer.DestinationId &&
                    ((plus.Length > 0 && x.PLU != null && plus.Contains(x.PLU)) ||
                     (names.Length > 0 && names.Contains(x.Naziv))))
                .ToListAsync(ct);

            return destinationRows
                .GroupBy(BuildSkuKey)
                .ToDictionary(g => g.Key, g => g.First());
        }

        private async Task AcquireDestinationSkuCreationLockAsync(long destinationId, string skuKey, CancellationToken ct)
        {
            if (!IsNpgsqlProvider())
                return;

            var lockKey = $"transfer-dst:{destinationId}:{skuKey}";
            FormattableString lockSql = $@"SELECT pg_advisory_xact_lock(hashtext({lockKey}))";
            var swLock = Stopwatch.StartNew();
            await _db.Database.ExecuteSqlInterpolatedAsync(lockSql, ct);
            swLock.Stop();
            try { SqlCommandLoggingHelper.LogSqlExecution("transfer", "ExecuteSqlInterpolated", lockSql.Format, null, swLock.ElapsedMilliseconds, true, null, null, Application.Logging.RequestLogContext.Current.RequestId, Application.Logging.RequestLogContext.Current.TraceId); } catch { }
        }

        private async Task<Artikli?> FindDestinationArticleByKeyAsync(
            long destinationId,
            Artikli sourceArticle,
            CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(sourceArticle.PLU))
            {
                var plu = sourceArticle.PLU.Trim();
                return await _db.Artikli
                    .Where(x => x.IDObjekat == (int?)destinationId && x.PLU != null && string.Equals(x.PLU, plu, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.Id)
                    .FirstOrDefaultAsync(ct);
            }

            var naziv = (sourceArticle.Naziv ?? string.Empty).Trim();
            return await _db.Artikli
                .Where(x => x.IDObjekat == (int?)destinationId && x.Naziv != null && string.Equals(x.Naziv, naziv, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(ct);
        }

        private async Task<Artikli> GetOrCreateDestinationArticleAsync(
            Transfer transfer,
            Artikli sourceArticle,
            Dictionary<string, Artikli> destinationByKey,
            string userId,
            CancellationToken ct)
        {
            var key = BuildSkuKey(sourceArticle);
            if (destinationByKey.TryGetValue(key, out var existing))
                return existing;

            await AcquireDestinationSkuCreationLockAsync(transfer.DestinationId, key, ct);

            if (destinationByKey.TryGetValue(key, out existing))
                return existing;

            var existingInDb = await FindDestinationArticleByKeyAsync(transfer.DestinationId, sourceArticle, ct);
            if (existingInDb is not null)
            {
                destinationByKey[key] = existingInDb;
                return existingInDb;
            }

            var created = new Artikli
            {
                PLU = sourceArticle.PLU,
                Naziv = sourceArticle.Naziv,
                IDTipObuce = sourceArticle.IDTipObuce,
                IDDobavljac = sourceArticle.IDDobavljac,
                NabavnaCena = sourceArticle.NabavnaCena,
                NabavnaCenaDin = sourceArticle.NabavnaCenaDin,
                PrvaProdajnaCena = sourceArticle.PrvaProdajnaCena,
                ProdajnaCena = sourceArticle.ProdajnaCena,
                Velicina = sourceArticle.Velicina,
                Boja = sourceArticle.Boja,
                Kolicina = 0,
                MinimalnaKolicina = sourceArticle.MinimalnaKolicina,
                Komentar = $"Auto-created by transfer {transfer.Id}",
                IDObjekat = checked((int)transfer.DestinationId),
                IDSezona = sourceArticle.IDSezona,
                UpdatedAt = DateTime.UtcNow,
                Kategorija = sourceArticle.Kategorija,
                Pol = sourceArticle.Pol,
                Materijal = sourceArticle.Materijal,
                DataOrigin = "app",
                ImagePath = sourceArticle.ImagePath
            };

            _db.Artikli.Add(created);
            await _db.SaveChangesAsync(ct);

            destinationByKey[key] = created;
            _logger.LogInformation(
                "Transfer auto-created destination SKU row. TransferId={TransferId} SourceSkuId={SourceSkuId} DestinationSkuId={DestinationSkuId} DestinationStoreId={DestinationStoreId} User={UserId}",
                transfer.Id,
                sourceArticle.Id,
                created.Id,
                transfer.DestinationId,
                userId);

            return created;
        }

        private async Task<TransferResponse> BuildResponseAsync(Transfer transfer, CancellationToken ct)
        {
            var skuIds = transfer.Items.Select(x => checked((int)x.SkuId)).Distinct().ToArray();
            var artikli = await _db.Artikli
                .AsNoTracking()
                .Where(x => skuIds.Contains(x.Id))
                .Select(x => new { x.Id, x.PLU, x.Naziv, x.Kolicina })
                .ToListAsync(ct);

            var skuMap = artikli.ToDictionary(x => (long)x.Id);

            return new TransferResponse
            {
                Id = transfer.Id,
                Status = transfer.Status,
                SourceId = transfer.SourceId,
                DestinationId = transfer.DestinationId,
                Reserve = transfer.Reserve,
                Notes = transfer.Notes,
                CreatedAt = transfer.CreatedAt,
                UpdatedAt = transfer.UpdatedAt,
                ConfirmedAt = transfer.ConfirmedAt,
                CompletedAt = transfer.CompletedAt,
                CancelledAt = transfer.CancelledAt,
                CreatedBy = transfer.CreatedBy,
                UpdatedBy = transfer.UpdatedBy,
                LineCount = transfer.Items.Count,
                TotalQuantity = transfer.Items.Sum(x => x.Quantity),
                Items = transfer.Items
                    .OrderBy(x => x.Id)
                    .Select(x =>
                    {
                        skuMap.TryGetValue(x.SkuId, out var skuInfo);
                        return new TransferItemDto(
                            x.SkuId,
                            skuInfo?.PLU,
                            skuInfo?.Naziv,
                            x.Quantity,
                            x.ReservedQuantity,
                            x.ProcessedQuantity,
                            skuInfo?.Kolicina,
                            x.Unit);
                    })
                    .ToList()
            };
        }
    }
}
