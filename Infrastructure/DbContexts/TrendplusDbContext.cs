using System.Data.Common;
using Application.Artikli.Common.Interfaces;
using Domain.Model;
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
                eb.HasIndex(e => e.DataOrigin);
                eb.HasIndex(e => new { e.IDObjekat, e.Datum });
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

                eb.HasIndex(e => new { e.DatumProdaje, e.IDObjekat });
                eb.HasIndex(e => new { e.DataOrigin, e.DatumProdaje });
                
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

                eb.HasIndex(e => e.IdArtikal);
                eb.HasIndex(e => new { e.IdProdaja, e.IdArtikal });
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
                
                eb.HasIndex(e => e.BrojZapisnika).IsUnique();
                eb.HasIndex(e => e.IDDobavljac);
                eb.HasIndex(e => e.DatumPovracaja);
                
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
                
                eb.HasIndex(e => e.IdArtikal);
            });

            modelBuilder.Entity<DataImportBatch>(eb =>
            {
                eb.ToTable("DataImportBatches");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.SourceSystem).IsRequired().HasMaxLength(64);
                eb.Property(e => e.SourceFileName).IsRequired().HasMaxLength(300);
                eb.Property(e => e.SourceFilePath).HasMaxLength(800);
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
                eb.Property(e => e.ProcessedRowCount).HasDefaultValue(0);
                eb.Property(e => e.SkippedRowCount).HasDefaultValue(0);
                eb.Property(e => e.RowsInserted).HasDefaultValue(0);
                eb.Property(e => e.RowsUpdated).HasDefaultValue(0);
                eb.Property(e => e.RowsUnchanged).HasDefaultValue(0);
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
                eb.Property(e => e.CursorTimestampUtc);
                eb.Property(e => e.CursorId);
                eb.Property(e => e.CursorTieBreakerId);
                eb.Property(e => e.OverlapSeconds).HasDefaultValue(60);
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
        public DbSet<Domain.Transfers.Transfer> Transfers { get; set; } = null!;
        public DbSet<Domain.Transfers.TransferItem> TransferItems { get; set; } = null!;
        public DbSet<Infrastructure.Model.StockReservation> StockReservations { get; set; } = null!;

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
