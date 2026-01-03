using System.Data.Common;
using Application.Artikli.Common.Interfaces;
using Domain.Model;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.DbContexts
{
    public class TrendplusDbContext : DbContext, ITrendplusDbContext
    {
        public TrendplusDbContext(DbContextOptions<TrendplusDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
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

                eb.Property(e => e.StaraProdajnaCena).HasColumnType("decimal(18,2)");
                eb.Property(e => e.NovaProdajnaCena).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<Sezona>(eb =>
            {
                eb.ToTable("Sezone");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.Naziv).IsRequired().HasMaxLength(100);
                eb.Property(e => e.DatumOd).IsRequired();
                eb.Property(e => e.DatumDo).IsRequired();
            });

            modelBuilder.Entity<CreatedIdDto>().HasNoKey();
        }

        public DbSet<CreatedIdDto> CreatedIds => Set<CreatedIdDto>();
        public DbSet<Artikli> Artikli { get; set; } = null!;
        public DbSet<TipObuce> TipoviObuce { get; set; } = null!;
        public DbSet<Dobavljac> Dobavljaci { get; set; } = null!;
        public DbSet<ErrorRecord> ErrorRecords { get; set; } = null!;
        public DbSet<DnevnikPromena> DnevnikPromena { get; set; } = null!;
        public DbSet<Sezona> Sezone { get; set; } = null!;

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

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
