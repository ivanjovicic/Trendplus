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

        var facets = PreNivelacijaPriorityEndpoints.BuildFilterFacets(candidates, null, null, null);

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

        var facets = PreNivelacijaPriorityEndpoints.BuildFilterFacets(candidates, null, null, null);

        Assert.Empty(facets.Seasons);
        Assert.Empty(facets.FootwearTypes);
        Assert.Empty(facets.Suppliers);
    }

    [Fact]
    public void BuildFilterFacets_SupplierSelectionKeepsOtherSuppliersInFacet()
    {
        var candidates = new List<PreNivelacijaSkuCandidateDto>
        {
            CreateCandidate(1, supplierId: 10, supplierName: "Dobavljac A", seasonId: 1, season: "Prolece", footwearTypeId: 1, footwearType: "Sneaker"),
            CreateCandidate(2, supplierId: 20, supplierName: "Dobavljac B", seasonId: 1, season: "Prolece", footwearTypeId: 1, footwearType: "Sneaker"),
            CreateCandidate(3, supplierId: 20, supplierName: "Dobavljac B", seasonId: 2, season: "Jesen", footwearTypeId: 2, footwearType: "Boot"),
        };

        var facets = PreNivelacijaPriorityEndpoints.BuildFilterFacets(
            candidates,
            selectedSupplierId: 10,
            selectedSeasonId: 1,
            selectedFootwearTypeId: 1);

        Assert.Contains(facets.Suppliers, option => option.Id == 10 && option.Count == 1);
        Assert.Contains(facets.Suppliers, option => option.Id == 20 && option.Count == 1);
        Assert.DoesNotContain(facets.Seasons, option => option.Id == 2);
        Assert.Contains(facets.Seasons, option => option.Id == 1 && option.Count == 1);
        Assert.Contains(facets.FootwearTypes, option => option.Id == 1 && option.Count == 1);
    }

    [Fact]
    public void BuildFilterFacets_KeepsSelectedOptionWithZeroCount()
    {
        var candidates = new List<PreNivelacijaSkuCandidateDto>
        {
            CreateCandidate(1, supplierId: 10, supplierName: "Dobavljac A", seasonId: 1, season: "Prolece", footwearTypeId: 1, footwearType: "Sneaker"),
            CreateCandidate(2, supplierId: 20, supplierName: "Dobavljac B", seasonId: 2, season: "Jesen", footwearTypeId: 2, footwearType: "Boot"),
        };

        var facets = PreNivelacijaPriorityEndpoints.BuildFilterFacets(
            candidates,
            selectedSupplierId: 10,
            selectedSeasonId: 2,
            selectedFootwearTypeId: 2);

        Assert.Contains(facets.Suppliers, option => option.Id == 10 && option.Count == 0);
        Assert.Contains(facets.Suppliers, option => option.Id == 20 && option.Count == 1);
    }

    [Fact]
    public void ApplyDimensionFilters_FiltersBySelectedDimensions()
    {
        var candidates = new List<PreNivelacijaSkuCandidateDto>
        {
            CreateCandidate(1, supplierId: 10, supplierName: "A", seasonId: 1, season: "S1", footwearTypeId: 1, footwearType: "T1"),
            CreateCandidate(2, supplierId: 20, supplierName: "B", seasonId: 1, season: "S1", footwearTypeId: 1, footwearType: "T1"),
        };

        var filtered = PreNivelacijaPriorityEndpoints.ApplyDimensionFilters(candidates, supplierId: 20, seasonId: 1, footwearTypeId: 1);

        Assert.Single(filtered);
        Assert.Equal(2, filtered[0].ArtikalId);
    }

    private static PreNivelacijaSkuCandidateDto CreateCandidate(
        int artikalId,
        int supplierId,
        string supplierName,
        int seasonId,
        string season,
        int footwearTypeId,
        string footwearType)
    {
        return new PreNivelacijaSkuCandidateDto
        {
            ArtikalId = artikalId,
            Sku = $"SKU-{artikalId}",
            SupplierId = supplierId,
            SupplierName = supplierName,
            SeasonId = seasonId,
            Season = season,
            FootwearTypeId = footwearTypeId,
            FootwearType = footwearType,
            Recommendation = new PreNivelacijaRecommendationDto(),
        };
    }
}
