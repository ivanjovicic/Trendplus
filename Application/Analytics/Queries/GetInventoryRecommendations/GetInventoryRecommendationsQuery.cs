using MediatR;
using System;
using System.Collections.Generic;

namespace Application.Analytics.Queries.GetInventoryRecommendations;

public record GetInventoryRecommendationsQuery(
    DateOnly? Date     = null,
    string?   Brand    = null,
    string?   Category = null,
    int       MinQty   = 1,     // filtriraj preporuke manje od MinQty
    int       Top      = 100
) : IRequest<InventoryRecommendationsResult>;

public record InventoryRecommendationDto(
    long     Id,
    DateOnly SnapshotDate,
    string   ProductId,
    string?  Brand,
    string?  Category,
    double   SalesVelocity,
    double   StockOnHand,
    double   TrendScore,
    double   MomentumScore,
    int      RecommendedQty
);

public record InventoryRecommendationsResult(
    DateOnly                       Date,
    int                            TotalItems,
    List<InventoryRecommendationDto> Items,
    string                         DataQualityStatus = "insufficient_data",
    bool                           RecommendationAllowed = false,
    bool                           UsedFallback = false
);
