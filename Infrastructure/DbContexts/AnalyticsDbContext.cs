using Application.Artikli.Common.Interfaces;
using Domain.Model;
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
        }

        public DbSet<ProductsDim> ProductsDim => Set<ProductsDim>();
        public DbSet<StoresDim> StoresDim => Set<StoresDim>();
        public DbSet<PerformanceLog> PerformanceLogs => Set<PerformanceLog>();
        public DbSet<SalesFact> SalesFacts => Set<SalesFact>();
        public DbSet<SalesLineFact> SalesLineFacts => Set<SalesLineFact>();

        public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options)
            : base(options) { }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => base.SaveChangesAsync(cancellationToken);
    }
}
