using Application.Artikli.Common.Interfaces;
using Domain.Model;
using Domain.Model.Analytics;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
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
                .HasIndex(x => x.ProductId);

            modelBuilder.Entity<ProductsDim>()
                .HasIndex(x => x.Timestamp);
            
            modelBuilder.Entity<StoresDim>()
                .HasKey(x => x.StoreKey);

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

                entity.HasIndex(e => e.SaleId);
                entity.HasIndex(e => new { e.ProductId, e.SaleId });
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
        }

        public DbSet<ProductsDim> ProductsDim => Set<ProductsDim>();
        public DbSet<StoresDim> StoresDim => Set<StoresDim>();
        public DbSet<PerformanceLog> PerformanceLogs => Set<PerformanceLog>();
        public DbSet<SalesFact> SalesFacts => Set<SalesFact>();
        public DbSet<SalesLineFact> SalesLineFacts => Set<SalesLineFact>();
        
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

        public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options)
            : base(options) { }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => base.SaveChangesAsync(cancellationToken);
    }
}
