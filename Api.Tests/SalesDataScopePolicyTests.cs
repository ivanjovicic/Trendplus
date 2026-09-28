using Api.Services;
using Domain.Model.Prodaja;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class SalesDataScopePolicyTests
{
    [Theory]
    [InlineData("access", "imported", true)]
    [InlineData("existing", "imported", false)]
    [InlineData("access", "existing", false)]
    [InlineData("existing", "existing", true)]
    [InlineData(null, "existing", true)]
    [InlineData("", "existing", true)]
    [InlineData("access", "all", true)]
    public void HeaderOriginIsTheOnlyCertifiedSalesScopeInput(string? origin, string scope, bool expected)
    {
        Assert.Equal(expected, SalesDataScopePolicy.IsIncluded(origin, scope));

        var header = new ProdajaZaglavlje();
        if (origin is not null)
        {
            header.DataOrigin = origin;
        }
        Assert.Equal(expected, SalesDataScopePolicy.HeaderPredicate(scope).Compile()(header));
    }

    [Fact]
    public void InvalidScopeNormalizesToAll()
    {
        Assert.Equal("all", SalesDataScopePolicy.Normalize(" warehouse "));
        Assert.True(SalesDataScopePolicy.IsIncluded("unknown", " warehouse "));
    }
}
