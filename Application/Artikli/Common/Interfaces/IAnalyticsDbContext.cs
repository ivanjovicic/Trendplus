using Domain.Model;
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

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
