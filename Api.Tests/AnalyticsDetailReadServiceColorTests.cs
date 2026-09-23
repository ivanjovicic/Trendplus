using Api.Models;
using Api.Services;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsDetailReadServiceColorTests
{
    [Fact]
    public async Task ColorDetailProjectsRecommendationProvenanceAndComparablePeriod()
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase($"analytics-color-detail-{Guid.NewGuid():N}")
            .Options;
        await using var db = new TrendplusDbContext(options);

        db.Artikli.AddRange(
            new Artikli
            {
                Id = 1,
                Naziv = "Crna 1",
                Boja = "Crna",
                NabavnaCenaDin = 40m,
                DataOrigin = "existing"
            },
            new Artikli
            {
                Id = 2,
                Naziv = "Crna 2",
                Boja = "Crna",
                NabavnaCenaDin = 60m,
                DataOrigin = "existing"
            });
        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje { Id = 1, DatumProdaje = new DateTime(2026, 9, 10), DataOrigin = "existing" },
            new ProdajaZaglavlje { Id = 2, DatumProdaje = new DateTime(2026, 9, 9), DataOrigin = "existing" });
        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 1, IdProdaja = 1, IdArtikal = 1, Kolicina = 1, Cena = 100m, NabavnaCena = 40m },
            new ProdajaStavka { Id = 2, IdProdaja = 1, IdArtikal = 2, Kolicina = 1, Cena = 160m, NabavnaCena = 60m },
            new ProdajaStavka { Id = 3, IdProdaja = 2, IdArtikal = 1, Kolicina = 1, Cena = 80m, NabavnaCena = 40m });
        await db.SaveChangesAsync();

        var service = new AnalyticsDetailReadService(
            db,
            new StubDnevnikPromenaReadService(),
            Options.Create(new AnalyticsSnapshotOptions { UseSnapshotCost = false }));
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = QueryString.Create(
            new Dictionary<string, string?>
            {
                ["fromDate"] = "2026-09-10",
                ["toDate"] = "2026-09-10",
                ["dataScope"] = "all"
            });

        var result = await service.GetDetailAsync(
            "color-sales-stats",
            "Crna",
            httpContext.Request.Query);

        Assert.NotNull(result);
        Assert.Equal("Crna", result.Title);
        Assert.Equal("260.00", result.Fields.Single(field => field.Key == "ukupanPromet").Value);
        Assert.Equal("80.00", result.Fields.Single(field => field.Key == "previousPeriodRevenue").Value);
        Assert.NotNull(result.Recommendation);
        Assert.False(result.Recommendation!.RecommendationAllowed);
        Assert.NotEmpty(result.Recommendation.ReasonCodes);
        Assert.NotNull(result.Provenance);
        Assert.Equal("all", result.Provenance!.DataScope);
        Assert.Equal(new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc), result.Provenance.EffectiveFromUtc);
        Assert.False(result.Provenance.SnapshotActive);
    }

    private sealed class StubDnevnikPromenaReadService : IDnevnikPromenaReadService
    {
        public Task<DnevnikPromenaListResponseDto> GetPagedAsync(
            DnevnikPromenaListQuery query,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<DnevnikPromenaDetailDto?> GetByIdAsync(
            int id,
            CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
