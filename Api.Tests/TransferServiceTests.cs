using System;
using System.Linq;
using System.Threading.Tasks;
using Api.Dtos;
using Api.Services;
using Domain.Model;
using Domain.Transfers;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

public class TransferServiceTests
{
    [Fact]
    public async Task CreateDraft_CreatesTransferWithDraftStatus()
    {
        var db = CreateDb("tp_transfer_create_draft");
        var service = new TransferService(db, new NullLogger<TransferService>());

        var req = new TransferCreateRequest
        {
            SourceId = 1,
            DestinationId = 2,
            Reserve = true,
            Notes = "test",
            Items =
            {
                new TransferLineInputDto { SkuId = 101, Quantity = 2m, Unit = "pcs" },
                new TransferLineInputDto { SkuId = 101, Quantity = 1m, Unit = "pcs" } // aggregated
            }
        };

        var result = await service.CreateDraftAsync(req, "tester");

        Assert.True(result.Id > 0);
        Assert.Equal(TransferStatuses.Draft, result.Status);
        Assert.Equal(1, result.LineCount);
        Assert.Equal(3m, result.TotalQuantity);
        Assert.Equal(3m, result.Items.Single().Quantity);
    }

    [Fact]
    public async Task Confirm_Throws_WhenInsufficientStock()
    {
        var db = CreateDb("tp_transfer_confirm_insufficient");
        db.Artikli.Add(new Artikli
        {
            Id = 201,
            Naziv = "A",
            Kolicina = 1,
            IDObjekat = 1,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new TransferService(db, new NullLogger<TransferService>());
        var draft = await service.CreateDraftAsync(new TransferCreateRequest
        {
            SourceId = 1,
            DestinationId = 2,
            Reserve = true,
            Items = { new TransferLineInputDto { SkuId = 201, Quantity = 5m, Unit = "pcs" } }
        }, "tester");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmAsync(draft.Id, "tester"));
    }

    [Fact]
    public async Task Complete_MovesStockAndWritesBalancedMovements()
    {
        var db = CreateDb("tp_transfer_complete_balanced");
        db.Artikli.Add(new Artikli
        {
            Id = 301,
            PLU = "SKU-301",
            Naziv = "Patike 301",
            Kolicina = 10,
            IDObjekat = 1,
            NabavnaCena = 100m,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new TransferService(db, new NullLogger<TransferService>());
        var draft = await service.CreateDraftAsync(new TransferCreateRequest
        {
            SourceId = 1,
            DestinationId = 2,
            Reserve = true,
            Items = { new TransferLineInputDto { SkuId = 301, Quantity = 4m, Unit = "pcs" } }
        }, "tester");

        await service.ConfirmAsync(draft.Id, "tester");
        var completed = await service.CompleteAsync(draft.Id, "tester");

        Assert.Equal(TransferStatuses.Completed, completed.Status);

        var source = await db.Artikli.FirstAsync(x => x.Id == 301);
        var destination = await db.Artikli.FirstAsync(x => x.IDObjekat == 2 && x.PLU == "SKU-301");
        Assert.Equal(6, source.Kolicina);
        Assert.Equal(4, destination.Kolicina);

        var movements = db.DnevnikPromena.Where(x => x.BrojRacuna == $"TR-{draft.Id}").ToList();
        Assert.Equal(2, movements.Count);
        Assert.Single(movements, x => x.TipPromene == TipPromeneConstants.PrenosIzlaz && x.Kolicina == -4);
        Assert.Single(movements, x => x.TipPromene == TipPromeneConstants.PrenosUlaz && x.Kolicina == 4);
    }

    [Fact]
    public async Task ListAsync_FiltersByActorAndUpdatedBy()
    {
        var db = CreateDb("tp_transfer_actor_filters");
        var service = new TransferService(db, new NullLogger<TransferService>());

        var first = await service.CreateDraftAsync(new TransferCreateRequest
        {
            SourceId = 1,
            DestinationId = 2,
            Items = { new TransferLineInputDto { SkuId = 1, Quantity = 1m, Unit = "pcs" } }
        }, "user-a");

        var second = await service.CreateDraftAsync(new TransferCreateRequest
        {
            SourceId = 1,
            DestinationId = 2,
            Items = { new TransferLineInputDto { SkuId = 2, Quantity = 1m, Unit = "pcs" } }
        }, "user-b");

        await service.UpdateDraftAsync(second.Id, new TransferUpdateRequest
        {
            Reserve = false,
            Items = { new TransferLineInputDto { SkuId = 2, Quantity = 2m, Unit = "pcs" } }
        }, "auditor-x");

        var actorResult = await service.ListAsync(1, 50, null, "user-a", null, null);
        Assert.Single(actorResult.Items);
        Assert.Equal(first.Id, actorResult.Items[0].Id);

        var updatedByResult = await service.ListAsync(1, 50, null, null, null, "auditor-x");
        Assert.Single(updatedByResult.Items);
        Assert.Equal(second.Id, updatedByResult.Items[0].Id);
    }

    private static TrendplusDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new TrendplusDbContext(options);
    }
}
