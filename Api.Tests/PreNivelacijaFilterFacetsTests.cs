using Api.Models;
using Xunit;
using Trendplus2.Endpoints;

namespace Api.Tests;

public sealed class PreNivelacijaFilterFacetsTests
{
    [Fact]
    public void BuildFilterFacets_IncludesSeasonAndFootwearFromFullCandidateUniverse()
    {
        var candidates = new List<PreNivelacijaSkuCandidateDto>
        {
            new()
            {
                ArtikalId = 1,
                Sku = "SKU-1",
                SeasonId = 7,
                Season = "Prolece/Leto",
                FootwearTypeId = 4,
                FootwearType = "Sneaker",
            },
            new()
            {
                ArtikalId = 2,
                Sku = "SKU-2",
                SeasonId = 8,
                Season = "Jesen/Zima",
                FootwearTypeId = 5,
                FootwearType = "Boot",
            },
        };

        var facets = PreNivelacijaPriorityEndpoints.BuildFilterFacets(candidates);

        Assert.Equal(
            ["Jesen/Zima", "Prolece/Leto"],
            facets.Seasons.Select(option => option.Label).ToArray());
        Assert.Equal([8, 7], facets.Seasons.Select(option => option.Id).ToArray());
        Assert.Equal(
            ["Boot", "Sneaker"],
            facets.FootwearTypes.Select(option => option.Label).ToArray());
        Assert.Equal([5, 4], facets.FootwearTypes.Select(option => option.Id).ToArray());
    }

    [Fact]
    public void BuildFilterFacets_IgnoresMissingOrPlaceholderValues()
    {
        var candidates = new List<PreNivelacijaSkuCandidateDto>
        {
            new()
            {
                ArtikalId = 1,
                Sku = "SKU-1",
                SeasonId = null,
                Season = "N/A",
                FootwearTypeId = 0,
                FootwearType = "N/A",
            },
        };

        var facets = PreNivelacijaPriorityEndpoints.BuildFilterFacets(candidates);

        Assert.Empty(facets.Seasons);
        Assert.Empty(facets.FootwearTypes);
    }
}
