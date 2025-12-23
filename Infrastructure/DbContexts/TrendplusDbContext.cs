using Application.Artikli.Common.Interfaces;
using Domain.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.DbContexts
{
    public class TrendplusDbContext : DbContext, ITrendplusDbContext
    {
        public TrendplusDbContext(DbContextOptions<TrendplusDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Map entity to the actual table name in the database
            modelBuilder.Entity<Artikli>().ToTable("Artikli");
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
            modelBuilder.Entity<CreatedIdDto>().HasNoKey();
        }
        public DbSet<CreatedIdDto> CreatedIds => Set<CreatedIdDto>();
        public DbSet<Artikli> Artikli { get; set; }

        public DbSet<TipObuce> TipoviObuce { get; set; }

        public DbSet<Dobavljac> Dobavljaci { get; set; }

        // Added error records DbSet
        public DbSet<ErrorRecord> ErrorRecords { get; set; }

        public async Task<int> ExecuteStoredProcedureWithOutputAsync(string sql, SqlParameter[] parameters, CancellationToken cancellationToken = default)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            // Pozovi SP
            await Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);

            // Pronađi OUTPUT parametar (pretpostavljamo da je poslednji)
            var outputParam = parameters.FirstOrDefault(p => p.Direction == System.Data.ParameterDirection.Output);
            if (outputParam == null) throw new InvalidOperationException("OUTPUT parameter not found");

            return (int)outputParam.Value!;
        }
    }
}
