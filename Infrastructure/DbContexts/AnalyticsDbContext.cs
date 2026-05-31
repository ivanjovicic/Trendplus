using Application.Artikli.Common.Interfaces;
using Domain.Model;
using Domain.Model.Analytics;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.DbContexts
{
    public class AnalyticsDbContext : DbContext, IAnalyticsDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductsDim>()
                .HasKey(x => x.ProductKey);

            modelBuilder.Entity<ProductsDim>()
                .Property(x => x.Timestamp)
                .IsRequired();

            modelBuilder.Entity<ProductsDim>()
                .Property(x => x.DataOrigin)
                .HasMaxLength(32)
                .HasDefaultValue("existing");

            modelBuilder.Entity<ProductsDim>()
                .HasIndex(x => x.ProductId)
                .IsUnique();

            modelBuilder.Entity<ProductsDim>()
                .HasIndex(x => x.Timestamp);
            
            modelBuilder.Entity<StoresDim>(entity =>
            {
                entity.HasKey(x => x.StoreKey);
                entity.Property(x => x.StoreName).HasMaxLength(300);
                entity.Property(x => x.City).HasMaxLength(200);
                entity.Property(x => x.Region).HasMaxLength(100);
                entity.Property(x => x.Telefon).HasMaxLength(50);
                entity.Property(x => x.Menedzer).HasMaxLength(200);
                entity.Property(x => x.DataOrigin).HasMaxLength(32).HasDefaultValue("existing");
                entity.HasIndex(x => x.StoreId).IsUnique();
                entity.HasIndex(x => x.DataOrigin);
            });

            modelBuilder.Entity<PerformanceLog>(entity =>
            {
                entity.ToTable("PerformanceLogs");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Timestamp).IsRequired();
                entity.Property(e => e.RequestType).HasMaxLength(200).IsRequired();
                entity.Property(e => e.RequestName).HasMaxLength(500).IsRequired();
                entity.Property(e => e.DurationMs).IsRequired();
                entity.Property(e => e.RequestData).HasMaxLength(4000);
                entity.Property(e => e.ResponseData).HasMaxLength(4000);
                entity.Property(e => e.ExceptionMessage).HasMaxLength(2000);
                entity.Property(e => e.IsSuccess).IsRequired();
                
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.DurationMs);
                entity.HasIndex(e => e.RequestName);
            });

            modelBuilder.Entity<ProductsDim>()
                .Property(x => x.Kolicina)
                .HasColumnType("integer");

            modelBuilder.Entity<ProductsDim>()
                .Property(x => x.Materijal)
                .HasMaxLength(100);

            modelBuilder.Entity<ProductsDim>()
                .Property(x => x.PLU)
                .HasMaxLength(100);

            modelBuilder.Entity<ProductsDim>()
                .Property(x => x.MinimalnaKolicina)
                .HasColumnType("integer");

            modelBuilder.Entity<SalesFact>(entity =>
            {
                entity.ToTable("SalesFacts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SaleId).IsRequired();
                entity.Property(e => e.BrojRacuna).HasMaxLength(100);
                entity.Property(e => e.SaleTimestampUtc).IsRequired();
                entity.Property(e => e.StoreId).IsRequired();
                entity.Property(e => e.PaymentType).HasMaxLength(100);
                entity.Property(e => e.TotalAmount).HasColumnType("numeric(18,2)");
                entity.Property(e => e.DataOrigin).HasMaxLength(32).HasDefaultValue("existing");

                entity.HasIndex(e => e.SaleId).IsUnique();
                entity.HasIndex(e => e.SaleTimestampUtc);
                entity.HasIndex(e => e.StoreId);
            });

            modelBuilder.Entity<SalesLineFact>(entity =>
            {
                entity.ToTable("SalesLineFacts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SaleId).IsRequired();
                entity.Property(e => e.ProductId).IsRequired();
                entity.Property(e => e.Qty).IsRequired();
                entity.Property(e => e.UnitPrice).HasColumnType("numeric(18,2)");
                entity.Property(e => e.LineTotal).HasColumnType("numeric(18,2)");
                entity.Property(e => e.NabavnaCena).HasColumnType("numeric(18,2)");
                entity.Property(e => e.DataOrigin).HasMaxLength(32).HasDefaultValue("existing");

                entity.HasIndex(e => e.SaleId);
                entity.HasIndex(e => new { e.ProductId, e.SaleId });
            });

            modelBuilder.Entity<SuppliersDim>(entity =>
            {
                entity.HasKey(x => x.SupplierKey);
                entity.Property(x => x.Naziv).HasMaxLength(300);
                entity.Property(x => x.Adresa).HasMaxLength(500);
                entity.Property(x => x.Telefon).HasMaxLength(50);
                entity.Property(x => x.Napomena).HasMaxLength(500);
                entity.Property(x => x.DataOrigin).HasMaxLength(32).HasDefaultValue("existing");
                entity.HasIndex(x => x.SupplierId).IsUnique();
            });

            modelBuilder.Entity<SeasonsDim>(entity =>
            {
                entity.HasKey(x => x.SeasonKey);
                entity.Property(x => x.Naziv).HasMaxLength(200);
                entity.Property(x => x.DataOrigin).HasMaxLength(32).HasDefaultValue("existing");
                entity.HasIndex(x => x.SeasonId).IsUnique();
                entity.HasIndex(x => x.DatumOd);
                entity.HasIndex(x => x.DatumDo);
            });

            modelBuilder.Entity<FootwearTypesDim>(entity =>
            {
                entity.HasKey(x => x.TypeKey);
                entity.Property(x => x.Naziv).HasMaxLength(200);
                entity.Property(x => x.DataOrigin).HasMaxLength(32).HasDefaultValue("existing");
                entity.HasIndex(x => x.TypeId).IsUnique();
            });

            modelBuilder.Entity<InventoryMovementFact>(entity =>
            {
                entity.ToTable("InventoryMovementFacts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TipPromene).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Datum).IsRequired();
                entity.Property(e => e.Iznos).HasColumnType("numeric(18,2)");
                entity.Property(e => e.StaraProdajnaCena).HasColumnType("numeric(18,2)");
                entity.Property(e => e.NovaProdajnaCena).HasColumnType("numeric(18,2)");
                entity.Property(e => e.BrojDokumenta).HasMaxLength(100);
                entity.Property(e => e.KorisnikIme).HasMaxLength(200);
                entity.Property(e => e.DataOrigin).HasMaxLength(32).HasDefaultValue("existing");

                entity.HasIndex(e => new { e.SourceId, e.DataOrigin }).IsUnique();
                entity.HasIndex(e => e.Datum);
                entity.HasIndex(e => new { e.ArtikalId, e.Datum });
                entity.HasIndex(e => new { e.StoreId, e.Datum });
                entity.HasIndex(e => e.TipPromene);
            });

            // Global Trends Tables
            modelBuilder.Entity<EuTrend>(entity =>
            {
                entity.ToTable("EuTrends");
                entity.HasKey(e => e.Id);
                
                // Temporarily ignore Embedding property until Python service is ready
                // TODO: Re-enable when embedding generation is implemented
                entity.Ignore(e => e.Embedding);
                
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.Brand);
                entity.HasIndex(e => e.Rank);
                entity.HasIndex(e => e.Season);
                entity.HasIndex(e => e.UpdatedAt);
            });

            modelBuilder.Entity<SocialTrend>(entity =>
            {
                entity.ToTable("SocialTrends");
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => new { e.Category, e.Hashtag }).IsUnique();
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.Hashtag);
                entity.HasIndex(e => e.TiktokGrowth);
                entity.HasIndex(e => e.UpdatedAt);
            });

            modelBuilder.Entity<GlobalTrendScore>(entity =>
            {
                entity.ToTable("GlobalTrendScores");
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => e.LocalProductId).IsUnique();
                entity.HasIndex(e => e.FinalGlobalScore);
                entity.HasIndex(e => e.EuTrendScore);
                entity.HasIndex(e => e.SocialTrendScore);
                entity.HasIndex(e => e.UpdatedAt);
            });

            modelBuilder.Entity<TrendHistory>(entity =>
            {
                entity.ToTable("TrendHistory");
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => new { e.LocalProductId, e.Date }).IsUnique();
                entity.HasIndex(e => e.LocalProductId);
                entity.HasIndex(e => e.Date);
            });

            modelBuilder.Entity<AmazonShoeProduct>(entity =>
            {
                entity.ToTable("amazon_shoe_products");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Asin).IsUnique();
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.Gender);
                entity.HasIndex(e => e.Rating);
                entity.HasIndex(e => e.TrendScore);
                entity.HasIndex(e => e.LastSynced);
                entity.Property(e => e.Price).HasColumnType("numeric(18,4)");
                entity.Property(e => e.OriginalPrice).HasColumnType("numeric(18,4)");
            });

            modelBuilder.Entity<EbayShoeProduct>(entity =>
            {
                entity.ToTable("ebay_shoe_products");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.EbayItemId).IsUnique();
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.Gender);
                entity.HasIndex(e => e.Rating);
                entity.HasIndex(e => e.TrendScore);
                entity.HasIndex(e => e.LastSynced);
                entity.Property(e => e.Price).HasColumnType("numeric(18,4)");
            });

            modelBuilder.Entity<GoogleShoppingProduct>(entity =>
            {
                entity.ToTable("google_shopping_products");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ProductId).IsUnique().HasFilter("\"ProductId\" IS NOT NULL");
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.Gender);
                entity.HasIndex(e => e.TrendScore);
                entity.HasIndex(e => e.Position);
                entity.HasIndex(e => e.LastSynced);
                entity.Property(e => e.Price).HasColumnType("numeric(18,4)");
            });

            // ── Trend Momentum Engine ─────────────────────────────────────

            modelBuilder.Entity<TrendProductSnapshot>(entity =>
            {
                entity.ToTable("trend_product_snapshots");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Score).HasColumnType("double precision");
                entity.Property(e => e.SocialScore).HasColumnType("double precision");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.HasIndex(e => new { e.CanonicalKey, e.SnapshotDate })
                      .HasDatabaseName("idx_trend_snapshots_key_date");
            });

            modelBuilder.Entity<TrendProductMomentum>(entity =>
            {
                entity.ToTable("trend_product_momentum");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MomentumScore).HasColumnType("double precision");
                entity.Property(e => e.ScoreDelta).HasColumnType("double precision");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.HasIndex(e => new { e.SnapshotDate, e.CanonicalKey })
                      .HasDatabaseName("idx_trend_momentum_date_key");
            });

            modelBuilder.Entity<TrendplusIndexRecord>(entity =>
            {
                entity.ToTable("trendplus_index");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.IndexValue).HasColumnType("double precision");
                entity.Property(e => e.BaseComponent).HasColumnType("double precision");
                entity.Property(e => e.MomentumComponent).HasColumnType("double precision");
                entity.Property(e => e.SocialComponent).HasColumnType("double precision");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.HasIndex(e => new { e.ScopeType, e.ScopeValue, e.SnapshotDate })
                      .IsUnique()
                      .HasDatabaseName("idx_trendplus_index_scope_date");
                entity.HasIndex(e => e.SnapshotDate)
                      .HasDatabaseName("idx_trendplus_index_date");
            });

            modelBuilder.Entity<InventoryRecommendation>(entity =>
            {
                entity.ToTable("inventory_recommendations");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SalesVelocity).HasColumnType("double precision");
                entity.Property(e => e.StockOnHand).HasColumnType("double precision");
                entity.Property(e => e.TrendScore).HasColumnType("double precision");
                entity.Property(e => e.MomentumScore).HasColumnType("double precision");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
                entity.HasIndex(e => e.SnapshotDate)
                      .HasDatabaseName("idx_inv_rec_date");
                entity.HasIndex(e => new { e.ProductId, e.SnapshotDate })
                      .HasDatabaseName("idx_inv_rec_product");
            });

            modelBuilder.Entity<AnalyticsRefreshRun>(entity =>
            {
                entity.ToTable("analytics_refresh_runs");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.JobKey)
                    .IsRequired()
                    .HasMaxLength(120);
                entity.Property(e => e.JobName)
                    .IsRequired()
                    .HasMaxLength(200);
                entity.Property(e => e.Status)
                    .IsRequired()
                    .HasMaxLength(32);

                entity.Property(e => e.StartedAtUtc)
                    .IsRequired();
                entity.Property(e => e.FinishedAtUtc);
                entity.Property(e => e.DurationSeconds);

                entity.Property(e => e.RefreshedObjectsJson)
                    .HasColumnType("jsonb");
                entity.Property(e => e.FailedObjectsJson)
                    .HasColumnType("jsonb");

                entity.Property(e => e.ErrorCode)
                    .HasMaxLength(120);
                entity.Property(e => e.ErrorMessage)
                    .HasMaxLength(2000);
                entity.Property(e => e.CorrelationId)
                    .HasMaxLength(100);

                entity.Property(e => e.TriggeredBy)
                    .IsRequired()
                    .HasMaxLength(80);
                entity.Property(e => e.ProcessMode)
                    .IsRequired()
                    .HasMaxLength(32);
                entity.Property(e => e.WorkerName)
                    .HasMaxLength(200);

                entity.Property(e => e.CreatedAtUtc)
                    .IsRequired()
                    .HasDefaultValueSql("now()");

                entity.HasIndex(e => new { e.JobKey, e.StartedAtUtc })
                    .IsDescending(false, true)
                    .HasDatabaseName("idx_analytics_refresh_runs_job_started");
                entity.HasIndex(e => e.Status)
                    .HasDatabaseName("idx_analytics_refresh_runs_status");
                entity.HasIndex(e => e.CreatedAtUtc)
                    .HasDatabaseName("idx_analytics_refresh_runs_created_at");
                entity.HasIndex(e => new { e.WorkerName, e.StartedAtUtc })
                    .HasDatabaseName("idx_analytics_refresh_runs_worker_started");
            });

            // ── Analytics Action Queue ───────────────────────────────────
            modelBuilder.Entity<AnalyticsActionItem>(entity =>
            {
                entity.ToTable("analytics_action_items");
                entity.HasKey(e => e.Id);
                
                // Core fields
                entity.Property(e => e.SourceType)
                    .HasMaxLength(32)
                    .IsRequired();
                entity.Property(e => e.SourceKey)
                    .HasMaxLength(500)
                    .IsRequired();
                entity.Property(e => e.Title)
                    .HasMaxLength(500)
                    .IsRequired();
                entity.Property(e => e.Description)
                    .HasMaxLength(2000);
                entity.Property(e => e.RecommendationStatus)
                    .HasMaxLength(200);
                entity.Property(e => e.Priority)
                    .HasMaxLength(8)
                    .IsRequired();
                entity.Property(e => e.Status)
                    .HasMaxLength(32)
                    .IsRequired();

                // Quality/impact fields
                entity.Property(e => e.ImpactEstimateRsd)
                    .HasColumnType("numeric(18,2)");
                entity.Property(e => e.DueAtUtc);
                entity.Property(e => e.ExpectedImpactRsd)
                    .HasColumnType("numeric(18,2)");
                entity.Property(e => e.MeasuredImpactRsd)
                    .HasColumnType("numeric(18,2)");
                entity.Property(e => e.OutcomeStatus)
                    .HasMaxLength(32);
                entity.Property(e => e.OutcomeMeasuredAtUtc);
                entity.Property(e => e.OutcomeNotes)
                    .HasMaxLength(4000);
                entity.Property(e => e.ConfidencePct);
                entity.Property(e => e.ReliabilityPct);
                entity.Property(e => e.DataQualityStatus)
                    .HasMaxLength(32);
                
                // Audit fields
                entity.Property(e => e.ActionUrl)
                    .HasMaxLength(1000);
                entity.Property(e => e.MetadataJson)
                    .HasColumnType("jsonb");
                entity.Property(e => e.CreatedAtUtc)
                    .IsRequired()
                    .HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAtUtc)
                    .IsRequired()
                    .HasDefaultValueSql("now()");
                entity.Property(e => e.CreatedByUserId)
                    .HasMaxLength(200);
                entity.Property(e => e.UpdatedByUserId)
                    .HasMaxLength(200);
                entity.Property(e => e.UpdatedByUserName)
                    .HasMaxLength(200);

                // Indexes for filtering and duplicate detection
                entity.HasIndex(e => new { e.SourceType, e.SourceKey })
                    .IsUnique()
                    .HasFilter("\"Status\" IN ('new', 'accepted', 'deferred')")
                    .HasDatabaseName("idx_analytics_action_sourcekey_open");
                // Additional composite indexes to improve common queries
                entity.HasIndex(e => new { e.SourceType, e.SourceKey, e.Status })
                    .HasDatabaseName("idx_analytics_action_sourcekey_status");
                entity.HasIndex(e => new { e.Priority, e.Status })
                    .HasDatabaseName("idx_analytics_action_priority_status");
                entity.HasIndex(e => e.Status)
                    .HasDatabaseName("idx_analytics_action_status");
                entity.HasIndex(e => e.Priority)
                    .HasDatabaseName("idx_analytics_action_priority");
                entity.HasIndex(e => new { e.SourceType, e.CreatedAtUtc })
                    .HasDatabaseName("idx_analytics_action_source_created");
                entity.HasIndex(e => e.UpdatedAtUtc)
                    .HasDatabaseName("idx_analytics_action_updated");
                entity.HasIndex(e => new { e.Status, e.UpdatedAtUtc })
                    .HasDatabaseName("idx_analytics_action_status_updated");
            });

            modelBuilder.Entity<AnalyticsActionNote>(entity =>
            {
                entity.ToTable("analytics_action_notes");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.ActionItemId)
                    .IsRequired();
                entity.Property(e => e.StatusFrom)
                    .HasMaxLength(32)
                    .IsRequired();
                entity.Property(e => e.StatusTo)
                    .HasMaxLength(32)
                    .IsRequired();
                entity.Property(e => e.Note)
                    .HasMaxLength(4000);
                entity.Property(e => e.CreatedAtUtc)
                    .IsRequired()
                    .HasDefaultValueSql("now()");
                entity.Property(e => e.CreatedByUserId)
                    .HasMaxLength(200);
                entity.Property(e => e.CreatedByUserName)
                    .HasMaxLength(200);

                // Audit trail should not be cascaded away implicitly.
                entity.HasOne(e => e.ActionItem)
                    .WithMany(e => e.Notes)
                    .HasForeignKey(e => e.ActionItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.ActionItemId)
                    .HasDatabaseName("idx_analytics_action_notes_action_item");
                entity.HasIndex(e => e.CreatedAtUtc)
                    .HasDatabaseName("idx_analytics_action_notes_created");
                entity.HasIndex(e => new { e.ActionItemId, e.CreatedAtUtc })
                    .HasDatabaseName("idx_analytics_action_notes_action_created");
            });
        }

        public DbSet<ProductsDim> ProductsDim => Set<ProductsDim>();
        public DbSet<StoresDim> StoresDim => Set<StoresDim>();
        public DbSet<PerformanceLog> PerformanceLogs => Set<PerformanceLog>();
        public DbSet<SalesFact> SalesFacts => Set<SalesFact>();
        public DbSet<SalesLineFact> SalesLineFacts => Set<SalesLineFact>();
        public DbSet<SuppliersDim> SuppliersDim => Set<SuppliersDim>();
        public DbSet<SeasonsDim> SeasonsDim => Set<SeasonsDim>();
        public DbSet<FootwearTypesDim> FootwearTypesDim => Set<FootwearTypesDim>();
        public DbSet<InventoryMovementFact> InventoryMovementFacts => Set<InventoryMovementFact>();
        public DbSet<ReturnFact> ReturnFacts => Set<ReturnFact>();
        
        // Global Trends
        public DbSet<EuTrend> EuTrends => Set<EuTrend>();
        public DbSet<SocialTrend> SocialTrends => Set<SocialTrend>();
        public DbSet<GlobalTrendScore> GlobalTrendScores => Set<GlobalTrendScore>();
        public DbSet<TrendHistory> TrendHistories => Set<TrendHistory>();

        // Amazon Shoes (SerpAPI)
        public DbSet<AmazonShoeProduct> AmazonShoeProducts => Set<AmazonShoeProduct>();

        // eBay Shoes (Browse API)
        public DbSet<EbayShoeProduct> EbayShoeProducts => Set<EbayShoeProduct>();

        // Google Shopping (SerpAPI)
        public DbSet<GoogleShoppingProduct> GoogleShoppingProducts => Set<GoogleShoppingProduct>();

        // ── Trend Momentum Engine & Pipeline ─────────────────────────────
        public DbSet<TrendProductSnapshot> TrendProductSnapshots => Set<TrendProductSnapshot>();
        public DbSet<TrendProductMomentum> TrendProductMomentums => Set<TrendProductMomentum>();
        public DbSet<TrendplusIndexRecord> TrendplusIndexRecords => Set<TrendplusIndexRecord>();
        public DbSet<InventoryRecommendation> InventoryRecommendations => Set<InventoryRecommendation>();
        public DbSet<AnalyticsRefreshRun> AnalyticsRefreshRuns => Set<AnalyticsRefreshRun>();

        // ── Analytics Action Queue ───────────────────────────────────────
        public DbSet<AnalyticsActionItem> AnalyticsActionItems => Set<AnalyticsActionItem>();
        public DbSet<AnalyticsActionNote> AnalyticsActionNotes => Set<AnalyticsActionNote>();

        public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options)
            : base(options) { }

        public DbConnection GetDbConnection()
            => Database.GetDbConnection();

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => base.SaveChangesAsync(cancellationToken);
    }
}
