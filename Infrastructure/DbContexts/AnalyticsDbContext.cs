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
        }

        public DbSet<ProductsDim> ProductsDim => Set<ProductsDim>();
        public DbSet<StoresDim> StoresDim => Set<StoresDim>();
        public DbSet<PerformanceLog> PerformanceLogs => Set<PerformanceLog>();

        public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options)
            : base(options) { }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => base.SaveChangesAsync(cancellationToken);
    }
}
