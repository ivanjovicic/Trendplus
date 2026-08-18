using System.Data.Common;
using Application.Artikli.Common.Interfaces;
using Domain.Model;
using Domain.Model.Analytics;
using Domain.Model.Documents;
using Domain.Model.Prodaja;
using Domain.Model.Povracaj;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.DbContexts
{
    public class TrendplusDbContext : DbContext, ITrendplusDbContext
    {
        public TrendplusDbContext(DbContextOptions<TrendplusDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Artikli>(eb =>
            {
                eb.ToTable("Artikli");
                eb.HasKey(e => e.Id);

                eb.Property(e => e.DataOrigin)
                  .HasMaxLength(32)
                  .HasDefaultValue("existing");
                eb.Property(e => e.SourceTableKey).HasMaxLength(128);
                eb.Property(e => e.SourceRowId);
                eb.Property(e => e.SourceUpdatedAtUtc);
                eb.Property(e => e.SourceHash).HasMaxLength(128);
                eb.Property(e => e.SourceBatchId);
                
                eb.Property(e => e.Materijal)
                  .HasMaxLength(100)
                  .IsRequired(false);

                // Image support
                eb.Property(e => e.ImagePath)
                  .HasMaxLength(500)
                  .IsRequired(false);
                
                eb.HasIndex(e => e.ImagePath);
                eb.HasIndex(e => e.IDObjekat);
                eb.HasIndex(e => e.IDDobavljac);
                eb.HasIndex(e => e.DataOrigin);
                eb.HasIndex(e => new { e.IDObjekat, e.IDDobavljac });
                eb.HasIndex(e => new { e.DataOrigin, e.SourceTableKey, e.SourceRowId })
                  .IsUnique()
                  .HasFilter("\"DataOrigin\" = 'access' AND \"SourceTableKey\" IS NOT NULL AND \"SourceRowId\" IS NOT NULL");
                eb.HasIndex(e => new { e.DataOrigin, e.SourceTableKey, e.SourceUpdatedAtUtc, e.SourceRowId });
                
                // Navigation to multiple images
                eb.HasMany(e => e.Images)
                  .WithOne(i => i.Product)
                  .HasForeignKey(i => i.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
            });

            // ProductImage mapping for AI embeddings
            modelBuilder.Entity<ProductImage>(eb =>
            {
                eb.ToTable("ProductImages");
                eb.HasKey(e => e.Id);
                
                eb.Property(e => e.ProductId).IsRequired();
                eb.Property(e => e.FileName).IsRequired().HasMaxLength(500);
                eb.Property(e => e.CreatedAt).IsRequired();
                eb.Property(e => e.IsPrimary).IsRequired().HasDefaultValue(false);
                
                // Ignore Embedding property for now - will be enabled when Python service is ready
                eb.Ignore(e => e.Embedding);
                
                eb.HasIndex(e => e.ProductId);
                eb.HasIndex(e => e.CreatedAt);
                eb.HasIndex(e => new { e.ProductId, e.IsPrimary })
                  .HasFilter("\"IsPrimary\" = true");
            });

            modelBuilder.Entity<ErrorRecord>(eb =>
            {
                eb.ToTable("ErrorRecords");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.Timestamp).IsRequired();
                eb.Property(e => e.Message).HasMaxLength(2000);
                eb.Property(e => e.ExceptionType).HasMaxLength(500);
                eb.Property(e => e.StackTrace).HasMaxLength(4000);
                eb.Property(e => e.Path).HasMaxLength(1000);
                eb.Property(e => e.UserName).HasMaxLength(200);
                eb.Property(e => e.ClientApp).HasMaxLength(1000);
            });

            modelBuilder.Entity<DnevnikPromena>(eb =>
            {
                eb.ToTable("DnevnikPromena");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.TipPromene).IsRequired().HasMaxLength(100);
                eb.Property(e => e.Datum).IsRequired();
                eb.Property(e => e.Iznos).HasColumnType("decimal(18,2)");
                eb.Property(e => e.BrojRacuna).HasMaxLength(100);
                eb.Property(e => e.Komentar).HasMaxLength(500);
                eb.Property(e => e.KorisnikIme).HasMaxLength(200);
                eb.Property(e => e.Kolicina);
                eb.Property(e => e.IDObjekat);
                eb.Property(e => e.RedniBroj);
                eb.Property(e => e.StaraProdajnaCena).HasColumnType("decimal(18,2)");
                eb.Property(e => e.NovaProdajnaCena).HasColumnType("decimal(18,2)");
                eb.Property(e => e.DataOrigin).IsRequired().HasMaxLength(32).HasDefaultValue("existing");
                eb.Property(e => e.SourceTableKey).HasMaxLength(128);
                eb.Property(e => e.SourceRowId);
                eb.Property(e => e.SourceUpdatedAtUtc);
                eb.Property(e => e.SourceHash).HasMaxLength(128);
                eb.Property(e => e.SourceBatchId);
                eb.HasIndex(e => e.DataOrigin);
                eb.HasIndex(e => new { e.IDObjekat, e.Datum });
                eb.HasIndex(e => new { e.DataOrigin, e.SourceTableKey, e.SourceRowId })
                  .HasFilter("\"DataOrigin\" = 'access' AND \"SourceTableKey\" IS NOT NULL AND \"SourceRowId\" IS NOT NULL");
                eb.HasIndex(e => new { e.DataOrigin, e.SourceTableKey, e.SourceUpdatedAtUtc, e.SourceRowId });
            });

            modelBuilder.Entity<Sezona>(eb =>
            {
                eb.ToTable("Sezone");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.Naziv).IsRequired().HasMaxLength(100);
                eb.Property(e => e.DatumOd).IsRequired();
                eb.Property(e => e.DatumDo).IsRequired();
                eb.Property(e => e.DataOrigin).IsRequired().HasMaxLength(32).HasDefaultValue("existing");
            });

            modelBuilder.Entity<Dobavljac>(eb =>
            {
                eb.ToTable("Dobavljaci");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.DataOrigin).IsRequired().HasMaxLength(32).HasDefaultValue("existing");
            });

            modelBuilder.Entity<TipObuce>(eb =>
            {
                eb.ToTable("TipoviObuce");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.DataOrigin).IsRequired().HasMaxLength(32).HasDefaultValue("existing");
            });

            modelBuilder.Entity<OutboxMessage>(eb =>
            {
                eb.ToTable("OutboxMessages");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.EventType).IsRequired().HasMaxLength(200);
                eb.Property(e => e.Payload).IsRequired();
                eb.Property(e => e.CreatedAt).IsRequired();
                eb.Property(e => e.IsProcessed).IsRequired();
                eb.Property(e => e.RetryCount).HasDefaultValue(0);
                eb.Property(e => e.ErrorMessage).HasMaxLength(2000);
                eb.Property(e => e.CorrelationId).HasMaxLength(100);

                eb.HasIndex(e => e.IsProcessed);
                eb.HasIndex(e => e.CreatedAt);
            });

            modelBuilder.Entity<DocumentRecord>(eb =>
            {
                eb.ToTable("Documents");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.TemplateName).IsRequired().HasMaxLength(200);
                eb.Property(e => e.DocumentType).IsRequired().HasMaxLength(100);
                eb.Property(e => e.TableKey).IsRequired().HasMaxLength(200);
                eb.Property(e => e.TableTitle).IsRequired().HasMaxLength(300);
                eb.Property(e => e.Format).IsRequired().HasMaxLength(32);
                eb.Property(e => e.Orientation).IsRequired().HasMaxLength(32);
                eb.Property(e => e.Status).IsRequired().HasMaxLength(32);
                eb.Property(e => e.RequestedByUserId).IsRequired().HasMaxLength(200);
                eb.Property(e => e.RequestedByUserName).IsRequired().HasMaxLength(200);
                eb.Property(e => e.RequestedByRoles).HasMaxLength(1000);
                eb.Property(e => e.Locale).HasMaxLength(16);
                eb.Property(e => e.MimeType).HasMaxLength(150);
                eb.Property(e => e.FileName).HasMaxLength(260);
                eb.Property(e => e.StoragePath).HasMaxLength(500);
                eb.Property(e => e.FileUrl).HasMaxLength(1000);
                eb.Property(e => e.Sha256).HasMaxLength(128);
                eb.Property(e => e.ErrorMessage).HasMaxLength(4000);
                eb.HasIndex(e => e.Status);
                eb.HasIndex(e => e.CreatedAtUtc);
                eb.HasIndex(e => new { e.Status, e.NextAttemptAtUtc });
                eb.HasIndex(e => e.BatchId);
                eb.HasIndex(e => e.RequestedByUserId);
            });

            modelBuilder.Entity<DocumentTemplate>(eb =>
            {
                eb.ToTable("DocumentTemplates");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.Name).IsRequired().HasMaxLength(200);
                eb.Property(e => e.Type).IsRequired().HasMaxLength(100);
                eb.Property(e => e.Locale).IsRequired().HasMaxLength(16);
                eb.Property(e => e.CreatedByUserId).HasMaxLength(200);
                eb.HasIndex(e => new { e.Name, e.Version }).IsUnique();
                eb.HasIndex(e => new { e.Type, e.IsActive });
            });

            modelBuilder.Entity<DocumentAudit>(eb =>
            {
                eb.ToTable("DocumentAudits");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.Action).IsRequired().HasMaxLength(64);
                eb.Property(e => e.UserId).IsRequired().HasMaxLength(200);
                eb.Property(e => e.UserName).IsRequired().HasMaxLength(200);
                eb.Property(e => e.Roles).HasMaxLength(1000);
                eb.Property(e => e.IpAddress).HasMaxLength(128);
                eb.Property(e => e.UserAgent).HasMaxLength(1024);
                eb.HasIndex(e => new { e.DocumentId, e.CreatedAtUtc });
            });

            // Prodaja mapping
            modelBuilder.Entity<ProdajaZaglavlje>(eb =>
            {
                eb.ToTable("prodaja_zaglavlje");
                eb.HasKey(e => e.Id);
                
                // Explicit column mapping for PostgreSQL snake_case
                eb.Property(e => e.Id).HasColumnName("id");
                eb.Property(e => e.BrojRacuna).HasColumnName("broj_racuna").HasMaxLength(100);
                eb.Property(e => e.DatumProdaje).HasColumnName("datum_prodaje").IsRequired();
                eb.Property(e => e.NacinPlacanja).HasColumnName("nacin_placanja").HasMaxLength(100);
                eb.Property(e => e.IDObjekat).HasColumnName("id_objekat");
                eb.Property(e => e.KorisnikIme).HasColumnName("korisnik_ime").HasMaxLength(200);
                eb.Property(e => e.DataOrigin).HasColumnName("data_origin").IsRequired().HasMaxLength(32).HasDefaultValue("existing");
                eb.Property(e => e.SourceTableKey).HasColumnName("source_table_key").HasMaxLength(128);
                eb.Property(e => e.SourceRowId).HasColumnName("source_row_id");
                eb.Property(e => e.SourceUpdatedAtUtc).HasColumnName("source_updated_at_utc");
                eb.Property(e => e.SourceHash).HasColumnName("source_hash").HasMaxLength(128);
                eb.Property(e => e.SourceBatchId).HasColumnName("source_batch_id");

                eb.HasIndex(e => new { e.DatumProdaje, e.IDObjekat });
                eb.HasIndex(e => new { e.DataOrigin, e.DatumProdaje });
                eb.HasIndex(e => new { e.DataOrigin, e.SourceTableKey, e.SourceRowId })
                  .IsUnique()
                  .HasFilter("data_origin = 'access' AND source_table_key IS NOT NULL AND source_row_id IS NOT NULL");
                eb.HasIndex(e => new { e.DataOrigin, e.SourceTableKey, e.SourceUpdatedAtUtc, e.SourceRowId });
                
                eb.HasMany(e => e.Stavke)
                  .WithOne(s => s.Prodaja)
                  .HasForeignKey(s => s.IdProdaja)
                  .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ProdajaStavka>(eb =>
            {
                eb.ToTable("prodaja_stavke");
                eb.HasKey(e => e.Id);
                
                // Explicit column mapping for PostgreSQL snake_case
                eb.Property(e => e.Id).HasColumnName("id");
                eb.Property(e => e.IdProdaja).HasColumnName("id_prodaja").IsRequired();
                eb.Property(e => e.IdArtikal).HasColumnName("id_artikal").IsRequired();
                eb.Property(e => e.Kolicina).HasColumnName("kolicina").IsRequired();
                eb.Property(e => e.Cena).HasColumnName("cena").HasColumnType("decimal(18,2)").IsRequired();
                eb.Property(e => e.NabavnaCena).HasColumnName("nabavna_cena").HasColumnType("decimal(18,2)");
                eb.Property(e => e.SourceTableKey).HasColumnName("source_table_key").HasMaxLength(128);
                eb.Property(e => e.SourceRowId).HasColumnName("source_row_id");
                eb.Property(e => e.SourceUpdatedAtUtc).HasColumnName("source_updated_at_utc");
                eb.Property(e => e.SourceHash).HasColumnName("source_hash").HasMaxLength(128);
                eb.Property(e => e.SourceBatchId).HasColumnName("source_batch_id");

                eb.HasIndex(e => e.IdArtikal);
                eb.HasIndex(e => new { e.IdProdaja, e.IdArtikal });
                eb.HasIndex(e => new { e.SourceTableKey, e.SourceRowId })
                  .IsUnique()
                  .HasFilter("source_table_key IS NOT NULL AND source_row_id IS NOT NULL");
                eb.HasIndex(e => new { e.SourceTableKey, e.SourceUpdatedAtUtc, e.SourceRowId });
            });

            // Povracaj mapping
            modelBuilder.Entity<PovracajZaglavlje>(eb =>
            {
                eb.ToTable("povracaj_zaglavlje");
                eb.HasKey(e => e.Id);
                
                eb.Property(e => e.Id).HasColumnName("id");
                eb.Property(e => e.BrojZapisnika).HasColumnName("broj_zapisnika").HasMaxLength(100).IsRequired();
                eb.Property(e => e.DatumPovracaja).HasColumnName("datum_povracaja").IsRequired();
                eb.Property(e => e.IDDobavljac).HasColumnName("id_dobavljac").IsRequired();
                eb.Property(e => e.RazlogPovracaja).HasColumnName("razlog_povracaja");
                eb.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
                eb.Property(e => e.UkupanIznos).HasColumnName("ukupan_iznos").HasColumnType("decimal(18,2)");
                eb.Property(e => e.Komentar).HasColumnName("komentar");
                eb.Property(e => e.KreatorKorisnik).HasColumnName("kreirao_korisnik").HasMaxLength(200);
                eb.Property(e => e.OdobrioKorisnik).HasColumnName("odobrio_korisnik").HasMaxLength(200);
                eb.Property(e => e.DatumKreiranja).HasColumnName("datum_kreiranja").IsRequired();
                eb.Property(e => e.DatumOdobrenja).HasColumnName("datum_odobrenja");
                eb.Property(e => e.DataOrigin).HasColumnName("data_origin").IsRequired().HasMaxLength(32).HasDefaultValue("existing");
                eb.Property(e => e.SourceTableKey).HasColumnName("source_table_key").HasMaxLength(128);
                eb.Property(e => e.SourceRowId).HasColumnName("source_row_id");
                eb.Property(e => e.SourceUpdatedAtUtc).HasColumnName("source_updated_at_utc");
                eb.Property(e => e.SourceHash).HasColumnName("source_hash").HasMaxLength(128);
                eb.Property(e => e.SourceBatchId).HasColumnName("source_batch_id");
                
                eb.HasIndex(e => e.BrojZapisnika).IsUnique();
                eb.HasIndex(e => e.IDDobavljac);
                eb.HasIndex(e => e.DatumPovracaja);
                eb.HasIndex(e => new { e.DataOrigin, e.SourceTableKey, e.SourceRowId })
                  .IsUnique()
                  .HasFilter("data_origin = 'access' AND source_table_key IS NOT NULL AND source_row_id IS NOT NULL");
                eb.HasIndex(e => new { e.DataOrigin, e.SourceTableKey, e.SourceUpdatedAtUtc, e.SourceRowId });
                
                eb.HasMany(e => e.Stavke)
                  .WithOne(s => s.Povracaj)
                  .HasForeignKey(s => s.IdPovracaj)
                  .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PovracajStavka>(eb =>
            {
                eb.ToTable("povracaj_stavke");
                eb.HasKey(e => e.Id);
                
                eb.Property(e => e.Id).HasColumnName("id");
                eb.Property(e => e.IdPovracaj).HasColumnName("id_povracaj").IsRequired();
                eb.Property(e => e.IdArtikal).HasColumnName("id_artikal").IsRequired();
                eb.Property(e => e.Kolicina).HasColumnName("kolicina").IsRequired();
                eb.Property(e => e.Cena).HasColumnName("cena").HasColumnType("decimal(18,2)").IsRequired();
                eb.Property(e => e.Razlog).HasColumnName("razlog");
                eb.Property(e => e.StanjeArtikla).HasColumnName("stanje_artikla").HasMaxLength(100);
                eb.Property(e => e.SourceTableKey).HasColumnName("source_table_key").HasMaxLength(128);
                eb.Property(e => e.SourceRowId).HasColumnName("source_row_id");
                eb.Property(e => e.SourceUpdatedAtUtc).HasColumnName("source_updated_at_utc");
                eb.Property(e => e.SourceHash).HasColumnName("source_hash").HasMaxLength(128);
                eb.Property(e => e.SourceBatchId).HasColumnName("source_batch_id");
                
                eb.HasIndex(e => e.IdArtikal);
                eb.HasIndex(e => new { e.SourceTableKey, e.SourceRowId })
                  .IsUnique()
                  .HasFilter("source_table_key IS NOT NULL AND source_row_id IS NOT NULL");
                eb.HasIndex(e => new { e.SourceTableKey, e.SourceUpdatedAtUtc, e.SourceRowId });
            });

            modelBuilder.Entity<DataImportBatch>(eb =>
            {
                eb.ToTable("DataImportBatches");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.SourceSystem).IsRequired().HasMaxLength(64);
                eb.Property(e => e.SourceFileName).IsRequired().HasMaxLength(300);
                eb.Property(e => e.SourceFilePath).HasMaxLength(800);
                eb.Property(e => e.SourceStorageKey).HasMaxLength(1024);
                eb.Property(e => e.SourceStorageProvider).HasMaxLength(32);
                eb.Property(e => e.QueuedAtUtc).IsRequired();
                eb.Property(e => e.StartedAtUtc).IsRequired();
                eb.Property(e => e.CompletedAtUtc);
                eb.Property(e => e.LastHeartbeatUtc);
                eb.Property(e => e.Status).IsRequired().HasMaxLength(32);
                eb.Property(e => e.CurrentStep).HasMaxLength(64);
                eb.Property(e => e.CurrentTable).HasMaxLength(300);
                eb.Property(e => e.SummaryJson);
                eb.Property(e => e.ErrorMessage).HasMaxLength(4000);
                eb.Property(e => e.ErrorDetailsJson);
                eb.Property(e => e.RequestedBy).HasMaxLength(200);
                eb.Property(e => e.ImportMode).IsRequired().HasMaxLength(16).HasDefaultValue("auto");
                eb.Property(e => e.ImportStrategy).IsRequired().HasMaxLength(32).HasDefaultValue("full");
                eb.Property(e => e.IncludeAnalytics).HasDefaultValue(true);
                eb.Property(e => e.OverwriteExisting).HasDefaultValue(true);
                eb.Property(e => e.IncludeTemporaryTables).HasDefaultValue(false);
                eb.Property(e => e.SkipInvalidForeignKeys).HasDefaultValue(true);
                eb.Property(e => e.CancellationRequested).HasDefaultValue(false);
                eb.Property(e => e.CancellationRequestedAtUtc);
                eb.Property(e => e.RetryCount).HasDefaultValue(0);
                eb.Property(e => e.ProgressPercent).HasDefaultValue(0);
                eb.Property(e => e.RowsRead).HasDefaultValue(0);
                eb.Property(e => e.RowsAccepted).HasDefaultValue(0);
                eb.Property(e => e.RowsWritten).HasDefaultValue(0);
                eb.Property(e => e.IsIncremental).HasDefaultValue(false);
                eb.Property(e => e.CursorSnapshot).HasColumnType("jsonb");
                eb.Property(e => e.CursorBeforeJson).HasColumnType("jsonb");
                eb.Property(e => e.CursorAfterJson).HasColumnType("jsonb");
                eb.Property(e => e.ProcessedRowCount).HasDefaultValue(0);
                eb.Property(e => e.SkippedRowCount).HasDefaultValue(0);
                eb.Property(e => e.RowsInserted).HasDefaultValue(0);
                eb.Property(e => e.RowsUpdated).HasDefaultValue(0);
                eb.Property(e => e.RowsUnchanged).HasDefaultValue(0);
                eb.Property(e => e.RowsStaged).HasDefaultValue(0);
                eb.Property(e => e.RowsSkippedStale).HasDefaultValue(0);
                eb.Property(e => e.RowsRejected).HasDefaultValue(0);
                eb.Property(e => e.ShadowMismatchCount).HasDefaultValue(0);
                eb.Property(e => e.SourceFileHash).HasMaxLength(128);
                eb.Property(e => e.DurationSeconds);
                eb.Property(e => e.TotalImported).HasDefaultValue(0);
                eb.Property(e => e.TotalUpdated).HasDefaultValue(0);
                eb.Property(e => e.TotalErrors).HasDefaultValue(0);
                eb.Property(e => e.DataOrigin).IsRequired().HasMaxLength(32).HasDefaultValue("access");

                eb.HasMany(e => e.LogEntries)
                  .WithOne(l => l.Batch)
                  .HasForeignKey(l => l.BatchId)
                  .OnDelete(DeleteBehavior.Cascade);

                eb.HasIndex(e => e.StartedAtUtc);
                eb.HasIndex(e => e.QueuedAtUtc);
                eb.HasIndex(e => e.LastHeartbeatUtc);
                eb.HasIndex(e => e.Status);
                eb.HasIndex(e => e.CancellationRequested);
            });

            modelBuilder.Entity<AccessImportCursor>(eb =>
            {
                eb.ToTable("AccessImportCursors");
                eb.HasKey(e => e.TableKey);
                eb.Property(e => e.TableKey).HasMaxLength(128);
                eb.Property(e => e.CursorMode).IsRequired().HasMaxLength(32);
                eb.Property(e => e.SourceKey).HasMaxLength(256);
                eb.Property(e => e.CursorTimestampColumn).HasMaxLength(128);
                eb.Property(e => e.CursorIdColumn).HasMaxLength(128);
                eb.Property(e => e.CursorTimestampUtc);
                eb.Property(e => e.CursorId);
                eb.Property(e => e.CursorTieBreakerId);
                eb.Property(e => e.OverlapSeconds).HasDefaultValue(60);
                eb.Property(e => e.LastRowsRead).HasDefaultValue(0);
                eb.Property(e => e.LastRowsMerged).HasDefaultValue(0);
                eb.Property(e => e.LastLagSeconds);
                eb.Property(e => e.LastSuccessfulBatchId);
                eb.Property(e => e.LastRunStartedAtUtc);
                eb.Property(e => e.LastRunCompletedAtUtc);
                eb.Property(e => e.LeaseOwner).HasMaxLength(200);
                eb.Property(e => e.LeaseAcquiredAtUtc);
                eb.Property(e => e.LeaseExpiresAtUtc);
                eb.Property(e => e.LastError).HasMaxLength(2000);
                eb.Property(e => e.UpdatedAtUtc).HasDefaultValueSql("NOW()");

                eb.HasIndex(e => e.LastSuccessfulBatchId);
                eb.HasIndex(e => e.LeaseExpiresAtUtc);
            });

            modelBuilder.Entity<SourceSyncCheckpoint>(eb =>
            {
                eb.ToTable("SourceSyncCheckpoints");
                eb.HasKey(e => new { e.ConnectionId, e.MappingProfileId, e.SourceStream });
                eb.Property(e => e.ConnectionId).HasMaxLength(128);
                eb.Property(e => e.MappingProfileId).HasMaxLength(64);
                eb.Property(e => e.SourceStream).HasMaxLength(128);
                eb.Property(e => e.CursorMode).IsRequired().HasMaxLength(32).HasDefaultValue("id");
                eb.Property(e => e.ExternalKeyTieBreaker).HasMaxLength(256);
                eb.Property(e => e.OverlapSeconds).HasDefaultValue(60);
                eb.Property(e => e.SchemaFingerprint).HasMaxLength(80);
                eb.Property(e => e.FailureCategory).HasMaxLength(64);
                eb.Property(e => e.LastError).HasMaxLength(2000);
                eb.Property(e => e.TenantScope).IsRequired().HasMaxLength(32).HasDefaultValue("n/a_dedicated");
                eb.Property(e => e.UpdatedAtUtc).HasDefaultValueSql("NOW()");
                eb.HasIndex(e => e.FailureCategory);
            });

            modelBuilder.Entity<SourceSyncAppliedRow>(eb =>
            {
                eb.ToTable("SourceSyncAppliedRows");
                eb.HasKey(e => new { e.ConnectionId, e.MappingProfileId, e.SourceStream, e.ExternalKey });
                eb.Property(e => e.ConnectionId).HasMaxLength(128);
                eb.Property(e => e.MappingProfileId).HasMaxLength(64);
                eb.Property(e => e.SourceStream).HasMaxLength(128);
                eb.Property(e => e.ExternalKey).HasMaxLength(256);
                eb.Property(e => e.PayloadHash).IsRequired().HasMaxLength(80);
                eb.Property(e => e.ApplyStatus).IsRequired().HasMaxLength(16);
                eb.Property(e => e.RejectionReason).HasMaxLength(64);
                eb.Property(e => e.UpdatedAtUtc).HasDefaultValueSql("NOW()");
                eb.HasIndex(e => e.LastBatchId);
            });

            // Transfers
            modelBuilder.Entity<Domain.Transfers.Transfer>(eb =>
            {
                eb.ToTable("Transfers");
                eb.HasKey(e => e.Id);

                eb.Property(e => e.Status).IsRequired().HasMaxLength(32).HasDefaultValue("draft");
                eb.Property(e => e.SourceId).IsRequired();
                eb.Property(e => e.DestinationId).IsRequired();
                eb.Property(e => e.Reserve).IsRequired().HasDefaultValue(false);
                eb.Property(e => e.Notes).HasMaxLength(2000);
                eb.Property(e => e.CreatedAt).IsRequired();
                eb.Property(e => e.UpdatedAt).IsRequired();
                eb.Property(e => e.ConfirmedAt);
                eb.Property(e => e.CompletedAt);
                eb.Property(e => e.CancelledAt);
                eb.Property(e => e.CreatedBy).HasMaxLength(200);
                eb.Property(e => e.UpdatedBy).HasMaxLength(200);

                eb.HasMany(e => e.Items)
                  .WithOne()
                  .HasForeignKey("TransferId")
                  .OnDelete(DeleteBehavior.Cascade);

                eb.HasIndex(e => e.Status);
                eb.HasIndex(e => new { e.SourceId, e.DestinationId });
                eb.HasIndex(e => e.CreatedAt);
            });

            modelBuilder.Entity<Domain.Transfers.TransferItem>(eb =>
            {
                eb.ToTable("TransferItems");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.SkuId).IsRequired();
                eb.Property(e => e.Quantity).HasColumnType("decimal(18,4)").IsRequired();
                eb.Property(e => e.ReservedQuantity).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
                eb.Property(e => e.ProcessedQuantity).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
                eb.Property(e => e.Unit).HasMaxLength(32);
                eb.HasIndex(e => e.SkuId);
                eb.HasIndex("TransferId");
            });

            modelBuilder.Entity<Infrastructure.Model.StockReservation>(eb =>
            {
                eb.ToTable("StockReservations");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.TransferId).IsRequired();
                eb.Property(e => e.SkuId).IsRequired();
                eb.Property(e => e.Quantity).HasColumnType("decimal(18,4)").IsRequired();
                eb.Property(e => e.ExpiresAt);
                eb.Property(e => e.CreatedAt).IsRequired();
                eb.HasIndex(e => e.TransferId);
                eb.HasIndex(e => e.SkuId);
            });

            modelBuilder.Entity<AccessImportLog>(eb =>
            {
                eb.ToTable("AccessImportLog");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.TableName).IsRequired().HasMaxLength(128);
                eb.Property(e => e.RowIndex).HasDefaultValue(0);
                eb.Property(e => e.Severity).IsRequired().HasMaxLength(16).HasDefaultValue("info");
                eb.Property(e => e.Message).IsRequired().HasMaxLength(2000);
                eb.Property(e => e.SourceRowJson);
                eb.Property(e => e.CreatedAtUtc).HasDefaultValueSql("NOW()");

                eb.HasIndex(e => e.BatchId);
                eb.HasIndex(e => e.Severity);
                eb.HasIndex(e => new { e.BatchId, e.TableName });
            });

            // ── Analytics cost snapshot ──
            modelBuilder.Entity<AnalyticsCostSnapshotBatch>(eb =>
            {
                eb.ToTable("analytics_cost_snapshot_batches");
                eb.HasKey(e => e.Id);

                eb.Property(e => e.Id).HasColumnName("id");
                eb.Property(e => e.Scope).HasColumnName("scope").IsRequired().HasMaxLength(50).HasDefaultValue("access_origin");
                eb.Property(e => e.Status).HasColumnName("status").IsRequired().HasMaxLength(20).HasDefaultValue("draft");
                eb.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
                eb.Property(e => e.GeneratedAtUtc).HasColumnName("generated_at_utc");
                eb.Property(e => e.ActivatedAtUtc).HasColumnName("activated_at_utc");
                eb.Property(e => e.DeactivatedAtUtc).HasColumnName("deactivated_at_utc");
                eb.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired().HasMaxLength(100).HasDefaultValue("system");
                eb.Property(e => e.Description).HasColumnName("description");
                eb.Property(e => e.RowCount).HasColumnName("row_count").HasDefaultValue(0);
                eb.Property(e => e.TotalRevenueCovered).HasColumnName("total_revenue_covered").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                eb.Property(e => e.CoveragePct).HasColumnName("coverage_pct").HasDefaultValue(0d);
                eb.Property(e => e.NoCostPct).HasColumnName("no_cost_pct").HasDefaultValue(0d);
                eb.Property(e => e.GenerationDurationMs).HasColumnName("generation_duration_ms");
                eb.Property(e => e.DryRun).HasColumnName("dry_run").HasDefaultValue(false);
                eb.Property(e => e.ErrorMessage).HasColumnName("error_message");
                eb.Property(e => e.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");

                eb.HasIndex(e => e.Scope)
                  .IsUnique()
                  .HasFilter("\"status\" = 'active'")
                  .HasDatabaseName("ux_snapshot_batches_active_scope");

                eb.HasIndex(e => new { e.Status, e.Scope })
                  .HasDatabaseName("ix_snapshot_batches_status");

                eb.HasMany(e => e.Snapshots)
                  .WithOne(s => s.Batch)
                  .HasForeignKey(s => s.BatchId)
                  .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AnalyticsSaleLineCostSnapshot>(eb =>
            {
                eb.ToTable("analytics_sale_line_cost_snapshots");
                eb.HasKey(e => e.Id);

                eb.Property(e => e.Id).HasColumnName("id");
                eb.Property(e => e.BatchId).HasColumnName("batch_id").IsRequired();
                eb.Property(e => e.ProdajaStavkaId).HasColumnName("prodaja_stavka_id").IsRequired();
                eb.Property(e => e.ResolvedUnitCost).HasColumnName("resolved_unit_cost").HasColumnType("decimal(18,4)").IsRequired();
                eb.Property(e => e.CostSource).HasColumnName("cost_source").IsRequired();
                eb.Property(e => e.ProductCostRsdAtSnapshot).HasColumnName("product_cost_rsd_at_snapshot").HasColumnType("decimal(18,4)");
                eb.Property(e => e.ProductCostLegacyAtSnapshot).HasColumnName("product_cost_legacy_at_snapshot").HasColumnType("decimal(18,4)");
                eb.Property(e => e.ArtikalId).HasColumnName("artikal_id").IsRequired();

                eb.HasIndex(e => new { e.BatchId, e.ProdajaStavkaId })
                  .IsUnique()
                  .HasDatabaseName("ux_snapshot_lines_batch_stavka");

                eb.HasIndex(e => e.ProdajaStavkaId)
                  .HasDatabaseName("ix_snapshot_lines_stavka");

                eb.HasIndex(e => new { e.BatchId, e.CostSource })
                  .HasDatabaseName("ix_snapshot_lines_batch_source");
            });

            // ── Worker runtime settings ──
            modelBuilder.Entity<WorkerRuntimeSettings>(eb =>
            {
                eb.ToTable("WorkerRuntimeSettings");
                eb.HasKey(e => e.Id);
                
                eb.Property(e => e.WorkerName).IsRequired().HasMaxLength(200);
                eb.Property(e => e.IsScheduleEnabled).IsRequired().HasDefaultValue(true);
                eb.Property(e => e.IsManuallyStopped).IsRequired().HasDefaultValue(false);
                eb.Property(e => e.UpdatedAtUtc).IsRequired().HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
                eb.Property(e => e.UpdatedBy).HasMaxLength(200);
                eb.Property(e => e.Notes).HasMaxLength(1000);
                
                eb.HasIndex(e => e.WorkerName).IsUnique();
                eb.HasIndex(e => e.UpdatedAtUtc);
            });

            modelBuilder.Entity<CreatedIdDto>().HasNoKey();
        }

        public DbSet<CreatedIdDto> CreatedIds => Set<CreatedIdDto>();
        public DbSet<Artikli> Artikli { get; set; } = null!;
        public DbSet<ProductImage> ProductImages { get; set; } = null!; // NEW
        public DbSet<CrossPlatformProductAvailability> CrossPlatformProducts { get; set; } = null!;
        public DbSet<TipObuce> TipoviObuce { get; set; } = null!;
        public DbSet<Dobavljac> Dobavljaci { get; set; } = null!;
        public DbSet<ErrorRecord> ErrorRecords { get; set; } = null!;
        public DbSet<DnevnikPromena> DnevnikPromena { get; set; } = null!;
        public DbSet<Sezona> Sezone { get; set; } = null!;
        public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
        public DbSet<DocumentRecord> Documents { get; set; } = null!;
        public DbSet<DocumentTemplate> DocumentTemplates { get; set; } = null!;
        public DbSet<DocumentAudit> DocumentAudits { get; set; } = null!;
        public DbSet<ProdajaZaglavlje> ProdajaZaglavlja { get; set; } = null!;
        public DbSet<ProdajaStavka> ProdajaStavke { get; set; } = null!;
        public DbSet<PovracajZaglavlje> PovracajZaglavlja { get; set; } = null!;
        public DbSet<PovracajStavka> PovracajStavke { get; set; } = null!;
        public DbSet<DataImportBatch> DataImportBatches { get; set; } = null!;
        public DbSet<AccessImportCursor> AccessImportCursors { get; set; } = null!;
        public DbSet<AccessImportLog> AccessImportLogs { get; set; } = null!;
        public DbSet<SourceSyncCheckpoint> SourceSyncCheckpoints { get; set; } = null!;
        public DbSet<SourceSyncAppliedRow> SourceSyncAppliedRows { get; set; } = null!;
        public DbSet<Domain.Transfers.Transfer> Transfers { get; set; } = null!;
        public DbSet<Domain.Transfers.TransferItem> TransferItems { get; set; } = null!;
        public DbSet<Infrastructure.Model.StockReservation> StockReservations { get; set; } = null!;
        public DbSet<AnalyticsCostSnapshotBatch> AnalyticsCostSnapshotBatches { get; set; } = null!;
        public DbSet<AnalyticsSaleLineCostSnapshot> AnalyticsSaleLineCostSnapshots { get; set; } = null!;
        public DbSet<WorkerRuntimeSettings> WorkerRuntimeSettings { get; set; } = null!;

        public DbConnection GetDbConnection()
        {
            return Database.GetDbConnection();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries<Artikli>())
            {
                if (entry.State is EntityState.Added or EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = now;
                }
            }

            foreach (var entry in ChangeTracker.Entries<DocumentRecord>())
            {
                if (entry.State == EntityState.Added && entry.Entity.CreatedAtUtc == default)
                {
                    entry.Entity.CreatedAtUtc = now;
                }

                if (entry.State is EntityState.Added or EntityState.Modified)
                {
                    entry.Entity.UpdatedAtUtc = now;
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
