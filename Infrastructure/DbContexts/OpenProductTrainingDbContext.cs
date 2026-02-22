using Domain.Model.OpenProductTraining;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.DbContexts
{
    public class OpenProductTrainingDbContext : DbContext
    {
        public OpenProductTrainingDbContext(DbContextOptions<OpenProductTrainingDbContext> options)
            : base(options)
        {
        }

        public DbSet<TrainingDataset> Datasets => Set<TrainingDataset>();
        public DbSet<RawTrainingProduct> RawProducts => Set<RawTrainingProduct>();
        public DbSet<TrainingBrand> Brands => Set<TrainingBrand>();
        public DbSet<TrainingCategory> Categories => Set<TrainingCategory>();
        public DbSet<TrainingProduct> Products => Set<TrainingProduct>();
        public DbSet<TrainingProductImage> ProductImages => Set<TrainingProductImage>();
        public DbSet<TrainingProductAttribute> ProductAttributes => Set<TrainingProductAttribute>();
        public DbSet<TrainingProductPriceHistory> ProductPriceHistory => Set<TrainingProductPriceHistory>();
        public DbSet<TrainingProductReviewStats> ProductReviewStats => Set<TrainingProductReviewStats>();
        public DbSet<TrainingLabel> TrainingLabels => Set<TrainingLabel>();
        public DbSet<TrainingProductSplit> ProductSplits => Set<TrainingProductSplit>();
        public DbSet<TrainingProductFeatureVector> ProductFeatureVectors => Set<TrainingProductFeatureVector>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TrainingDataset>(entity =>
            {
                entity.ToTable("dataset");
                entity.HasIndex(x => x.Name).IsUnique(false);
                entity.Property(x => x.Id)
                    .HasColumnName("id");
                entity.Property(x => x.Name)
                    .HasColumnName("name");
                entity.Property(x => x.CreatedAt)
                    .HasColumnName("created_at");
                entity.Property(x => x.SourceType)
                    .HasColumnName("source_type");
                entity.Property(x => x.Description)
                    .HasColumnName("description");
                entity.Property(x => x.License)
                    .HasColumnName("license");
                entity.Property(x => x.RawLocation)
                    .HasColumnName("raw_location");
            });

            modelBuilder.Entity<RawTrainingProduct>(entity =>
            {
                entity.ToTable("raw_product");
                entity.HasIndex(x => new { x.DatasetId, x.ExternalId }).IsUnique();
                entity.Property(x => x.Id).HasColumnName("id");
                entity.Property(x => x.DatasetId).HasColumnName("dataset_id");
                entity.Property(x => x.ExternalId).HasColumnName("external_id");
                entity.Property(x => x.RawPayload).HasColumnName("raw_payload").HasColumnType("jsonb");
                entity.Property(x => x.ImportedAt).HasColumnName("imported_at");

                entity.HasOne(x => x.Dataset)
                    .WithMany(x => x.RawProducts)
                    .HasForeignKey(x => x.DatasetId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TrainingBrand>(entity =>
            {
                entity.ToTable("brand");
                entity.Property(x => x.Id).HasColumnName("id");
                entity.Property(x => x.Name).HasColumnName("name");
                entity.HasIndex(x => x.Name).IsUnique();
            });

            modelBuilder.Entity<TrainingCategory>(entity =>
            {
                entity.ToTable("category");
                entity.Property(x => x.Id).HasColumnName("id");
                entity.Property(x => x.Name).HasColumnName("name");
                entity.HasIndex(x => x.Name).IsUnique();
                entity.Property(x => x.ParentId).HasColumnName("parent_id");

                entity.HasOne(x => x.Parent)
                    .WithMany(x => x.Children)
                    .HasForeignKey(x => x.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TrainingProduct>(entity =>
            {
                entity.ToTable("product");
                entity.HasIndex(x => new { x.DatasetId, x.ExternalId }).IsUnique();
                entity.HasIndex(x => x.BrandId);
                entity.HasIndex(x => x.CategoryId);
                entity.HasIndex(x => x.ShoeType);
                entity.HasIndex(x => x.Price);
                entity.HasIndex(x => x.Gender);
                entity.HasIndex(x => x.AvgRating);

                entity.Property(x => x.Id).HasColumnName("id");
                entity.Property(x => x.DatasetId).HasColumnName("dataset_id");
                entity.Property(x => x.ExternalId).HasColumnName("external_id");
                entity.Property(x => x.BrandId).HasColumnName("brand_id");
                entity.Property(x => x.CategoryId).HasColumnName("category_id");
                entity.Property(x => x.Title).HasColumnName("title");
                entity.Property(x => x.Description).HasColumnName("description");
                entity.Property(x => x.Gender).HasColumnName("gender");
                entity.Property(x => x.ShoeType).HasColumnName("shoe_type");
                entity.Property(x => x.MainImageUrl).HasColumnName("main_image_url");
                entity.Property(x => x.AvgRating).HasColumnName("avg_rating").HasColumnType("numeric(3,2)");
                entity.Property(x => x.ReviewCount).HasColumnName("review_count");
                entity.Property(x => x.Currency).HasMaxLength(10);
                entity.Property(x => x.Currency).HasColumnName("currency");
                entity.Property(x => x.Price).HasColumnName("price").HasColumnType("numeric(10,2)");
                entity.Property(x => x.CreatedAt).HasColumnName("created_at");
                entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");

                entity.HasOne(x => x.Dataset)
                    .WithMany(x => x.Products)
                    .HasForeignKey(x => x.DatasetId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Brand)
                    .WithMany(x => x.Products)
                    .HasForeignKey(x => x.BrandId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(x => x.Category)
                    .WithMany(x => x.Products)
                    .HasForeignKey(x => x.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<TrainingProductImage>(entity =>
            {
                entity.ToTable("product_image");
                entity.Property(x => x.Id).HasColumnName("id");
                entity.Property(x => x.ProductId).HasColumnName("product_id");
                entity.Property(x => x.ImageUrl).HasColumnName("image_url");
                entity.Property(x => x.LocalPath).HasColumnName("local_path");
                entity.Property(x => x.IsPrimary).HasColumnName("is_primary");

                entity.HasOne(x => x.Product)
                    .WithMany(x => x.Images)
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TrainingProductAttribute>(entity =>
            {
                entity.ToTable("product_attribute");
                entity.Property(x => x.Id).HasColumnName("id");
                entity.Property(x => x.ProductId).HasColumnName("product_id");
                entity.Property(x => x.Key).HasColumnName("key");
                entity.Property(x => x.ValueRaw).HasColumnName("value_raw");
                entity.Property(x => x.ValueNormalized).HasColumnName("value_normalized");
                entity.HasIndex(x => new { x.ProductId, x.Key }).IsUnique();

                entity.HasOne(x => x.Product)
                    .WithMany(x => x.Attributes)
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TrainingProductPriceHistory>(entity =>
            {
                entity.ToTable("product_price_history");
                entity.Property(x => x.Id).HasColumnName("id");
                entity.Property(x => x.ProductId).HasColumnName("product_id");
                entity.Property(x => x.Currency).HasColumnName("currency");
                entity.Property(x => x.CollectedAt).HasColumnName("collected_at");
                entity.Property(x => x.Price).HasColumnName("price").HasColumnType("numeric(10,2)");
                entity.HasIndex(x => new { x.ProductId, x.CollectedAt });

                entity.HasOne(x => x.Product)
                    .WithMany(x => x.PriceHistory)
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TrainingProductReviewStats>(entity =>
            {
                entity.ToTable("product_review_stats");
                entity.HasKey(x => x.ProductId);
                entity.Property(x => x.ProductId).HasColumnName("product_id");
                entity.Property(x => x.AvgRating).HasColumnName("avg_rating").HasColumnType("numeric(3,2)");
                entity.Property(x => x.RatingCount).HasColumnName("rating_count");
                entity.Property(x => x.ReviewTextCount).HasColumnName("review_text_count");

                entity.HasOne(x => x.Product)
                    .WithOne(x => x.ReviewStats)
                    .HasForeignKey<TrainingProductReviewStats>(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TrainingLabel>(entity =>
            {
                entity.ToTable("training_label");
                entity.Property(x => x.Id).HasColumnName("id");
                entity.Property(x => x.ProductId).HasColumnName("product_id");
                entity.Property(x => x.LabelType).HasColumnName("label_type");
                entity.Property(x => x.ValueNumeric).HasColumnName("value_numeric").HasColumnType("numeric(12,4)");
                entity.Property(x => x.ValueText).HasColumnName("value_text");
                entity.Property(x => x.CreatedAt).HasColumnName("created_at");
                entity.HasIndex(x => x.ProductId);
                entity.HasIndex(x => x.LabelType);

                entity.HasOne(x => x.Product)
                    .WithMany(x => x.TrainingLabels)
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TrainingProductSplit>(entity =>
            {
                entity.ToTable("product_split");
                entity.HasKey(x => x.ProductId);
                entity.Property(x => x.ProductId).HasColumnName("product_id");
                entity.Property(x => x.Split)
                    .HasColumnName("split")
                    .HasColumnType("text");  // EF reads the dataset_split ENUM as text
                entity.HasIndex(x => x.Split);

                entity.HasOne(x => x.Product)
                    .WithOne(x => x.ProductSplit)
                    .HasForeignKey<TrainingProductSplit>(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TrainingProductFeatureVector>(entity =>
            {
                entity.ToTable("product_feature_vector");
                entity.Property(x => x.Id).HasColumnName("id");
                entity.Property(x => x.ProductId).HasColumnName("product_id");
                entity.Property(x => x.FeatureType).HasColumnName("feature_type");
                entity.Property(x => x.VectorDim).HasColumnName("vector_dim");
                entity.Property(x => x.Vector).HasColumnName("vector");
                entity.Property(x => x.CreatedAt).HasColumnName("created_at");
                entity.HasIndex(x => new { x.ProductId, x.FeatureType }).IsUnique();

                entity.HasOne(x => x.Product)
                    .WithMany(x => x.FeatureVectors)
                    .HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Npgsql 6+ fix: map all DateTime properties from TIMESTAMPTZ as UTC ──
            // The open_product_training schema was created with TIMESTAMPTZ columns.
            // Npgsql 6+ refuses to read timestamptz into DateTime unless Kind=Utc is set.
            var utcConverter = new ValueConverter<DateTime, DateTime>(
                write => write.ToUniversalTime(),
                read  => DateTime.SpecifyKind(read, DateTimeKind.Utc));

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties()
                    .Where(p => p.ClrType == typeof(DateTime)))
                {
                    property.SetColumnType("timestamp with time zone");
                    property.SetValueConverter(utcConverter);
                }
            }

            base.OnModelCreating(modelBuilder);
        }
    }
}
