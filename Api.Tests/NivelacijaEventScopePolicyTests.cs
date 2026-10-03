using Application.Analytics;
using Domain.Model;
using Xunit;

namespace Api.Tests;

public sealed class NivelacijaEventScopePolicyTests
{
    [Theory]
    [InlineData(null, 7, true)]
    [InlineData(7, 7, true)]
    [InlineData(8, 7, false)]
    [InlineData(7, null, true)]
    public void EventScope_AppliesChainWideAndMatchingStoreEvents(
        int? eventStoreId,
        int? requestedStoreId,
        bool expected)
    {
        Assert.Equal(expected, NivelacijaEventScopePolicy.AppliesToStore(eventStoreId, requestedStoreId));
    }

    [Fact]
    public void ApplyStoreScope_FiltersStoreEventsButRetainsChainWideEvents()
    {
        var events = new[]
        {
            new DnevnikPromena { Id = 1, IDObjekat = null },
            new DnevnikPromena { Id = 2, IDObjekat = 7 },
            new DnevnikPromena { Id = 3, IDObjekat = 8 }
        }.AsQueryable();

        var actual = NivelacijaEventScopePolicy.ApplyStoreScope(events, 7)
            .Select(item => item.Id)
            .OrderBy(id => id)
            .ToArray();

        Assert.Equal([1, 2], actual);
    }

    [Fact]
    public void VendorReaderSqlUsesTheSharedChainWideStorePredicate()
    {
        Assert.Equal(
            "(@storeId IS NULL OR d.\"IDObjekat\" IS NULL OR d.\"IDObjekat\" = @storeId::int)",
            NivelacijaEventScopePolicy.VendorSqlStorePredicate);
    }
}
