using System.Data.Common;
using Application.Artikli.Common.Interfaces;
using Domain.Model;
using Domain.Model.Prodaja;
using Domain.Model.Povracaj;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace Infrastructure.DbContexts
{
    public class TrendplusDbContext : DbContext, ITrendplusDbContext
    {
        public TrendplusDbContext(DbContextOptions<TrendplusDbContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            
            // Enable pgvector extension
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql(o => o.UseVector());
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Enable pgvector extension in PostgreSQL
            modelBuilder.HasPostgresExtension("vector");

            modelBuilder.Entity<Artikli>(eb =>
            {
                eb.ToTable("Artikli");
                eb.HasKey(e => e.Id);
                
                // Image support
                eb.Property(e => e.ImagePath)
                  .HasMaxLength(500)
                  .IsRequired(false);
                
                eb.HasIndex(e => e.ImagePath);
                
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

            modelBuilder.Entity<CreatedIdDto>().HasNoKey();
        }

        public DbSet<CreatedIdDto> CreatedIds => Set<CreatedIdDto>();
        public DbSet<Artikli> Artikli { get; set; } = null!;
        public DbSet<ProductImage> ProductImages { get; set; } = null!; // NEW
        public DbSet<TipObuce> TipoviObuce { get; set; } = null!;
        public DbSet<Dobavljac> Dobavljaci { get; set; } = null!;
        public DbSet<ErrorRecord> ErrorRecords { get; set; } = null!;
        public DbSet<DnevnikPromena> DnevnikPromena { get; set; } = null!;
        public DbSet<Sezona> Sezone { get; set; } = null!;
        public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
        public DbSet<ProdajaZaglavlje> ProdajaZaglavlja { get; set; } = null!;
        public DbSet<ProdajaStavka> ProdajaStavke { get; set; } = null!;
        public DbSet<PovracajZaglavlje> PovracajZaglavlja { get; set; } = null!;
        public DbSet<PovracajStavka> PovracajStavke { get; set; } = null!;

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
