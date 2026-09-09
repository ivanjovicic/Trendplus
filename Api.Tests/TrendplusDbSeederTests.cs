using Domain.Model;
using Infrastructure.DbContexts;
using Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests;

public sealed class TrendplusDbSeederTests
{
    [Fact]
    public async Task SeedAsync_DecrementsArticleStockByGeneratedSeedLineQuantities()
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase($"trendplus-seeder-{Guid.NewGuid():N}")
            .Options;

        await using var db = new TrendplusDbContext(options);

        foreach (var name in BuildSeedArticleNames())
        {
            db.Artikli.Add(new Artikli
            {
                Naziv = name,
                Kolicina = 100,
                ProdajnaCena = 100m,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();

        var initialStock = await db.Artikli
            .AsNoTracking()
            .ToDictionaryAsync(article => article.Id, article => article.Kolicina);

        await TrendplusDbSeeder.SeedAsync(db);

        var seedSaleIds = await db.ProdajaZaglavlja
            .Where(sale => sale.BrojRacuna != null && sale.BrojRacuna.StartsWith("SEED-"))
            .Select(sale => sale.Id)
            .ToListAsync();

        Assert.Equal(100, seedSaleIds.Count);

        var seedLines = await db.ProdajaStavke
            .Where(line => seedSaleIds.Contains(line.IdProdaja))
            .AsNoTracking()
            .ToListAsync();

        var soldByArticle = seedLines
            .GroupBy(line => line.IdArtikal)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Kolicina));

        var finalStock = await db.Artikli
            .AsNoTracking()
            .ToDictionaryAsync(article => article.Id, article => article.Kolicina);

        Assert.NotEmpty(soldByArticle);
        foreach (var (articleId, stockBeforeSeed) in initialStock)
        {
            var sold = soldByArticle.GetValueOrDefault(articleId, 0);
            Assert.Equal(stockBeforeSeed - sold, finalStock[articleId]);
            Assert.True(finalStock[articleId] >= 0);
        }
    }

    private static IReadOnlyList<string> BuildSeedArticleNames()
    {
        var names = new List<string>
        {
            "Nike Air Max 90",
            "Birkenstock Arizona",
            "Adidas Ultraboost",
            "Timberland Winter Boot",
            "Papuce EVA Basic"
        };

        var brands = new[] { "Nike", "Adidas", "Puma", "Reebok", "New Balance" };
        var models = new[] { "Runner", "Street", "Classic", "Sport", "Lite" };
        var colors = new[] { "Black", "White", "Blue", "Red", "Gray" };
        var sizes = new[] { 36, 37, 38, 39, 40, 41, 42, 43, 44, 45 };

        for (var i = 0; i < 140; i++)
        {
            var brand = brands[i % brands.Length];
            var model = models[(i / brands.Length) % models.Length];
            var color = colors[(i / (brands.Length * models.Length)) % colors.Length];
            var size = sizes[i % sizes.Length];
            names.Add($"{brand} {model} {color} {size}");
        }

        return names;
    }
}
