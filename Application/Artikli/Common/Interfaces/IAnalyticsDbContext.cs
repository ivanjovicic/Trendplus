using Domain.Model;
using Domain.Model.Analytics;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Artikli.Common.Interfaces
{
    public interface IAnalyticsDbContext 
    {
        public DbSet<ProductsDim> ProductsDim { get; }
        public DbSet<StoresDim> StoresDim { get; }
        public DbSet<PerformanceLog> PerformanceLogs { get; }
        public DbSet<SalesFact> SalesFacts { get; }
        public DbSet<SalesLineFact> SalesLineFacts { get; }
        public DbSet<SuppliersDim> SuppliersDim { get; }
        public DbSet<SeasonsDim> SeasonsDim { get; }
        public DbSet<FootwearTypesDim> FootwearTypesDim { get; }
        public DbSet<InventoryMovementFact> InventoryMovementFacts { get; }

        // Trend Engine tables
        public DbSet<TrendProductSnapshot> TrendProductSnapshots { get; }
        public DbSet<TrendProductMomentum> TrendProductMomentums { get; }
        public DbSet<TrendplusIndexRecord> TrendplusIndexRecords { get; }
        public DbSet<InventoryRecommendation> InventoryRecommendations { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
