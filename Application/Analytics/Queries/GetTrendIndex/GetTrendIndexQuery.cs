using MediatR;
using System;
using System.Collections.Generic;

namespace Application.Analytics.Queries.GetTrendIndex;

public record GetTrendIndexQuery(
    string    ScopeType  = "market",   // "market" | "brand" | "category" | "brand_market"
    string?   ScopeValue = null,       // npr. "DE", "nike", "sneaker"
    DateOnly? Date       = null,
    int       DaysBack   = 30          // za history chart
) : IRequest<TrendIndexResult>;

public record TrendIndexDto(
    DateOnly SnapshotDate,
    string   ScopeType,
    string   ScopeValue,
    double?  IndexValue,
    double?  BaseComponent,
    double?  MomentumComponent,
    double?  SocialComponent
);

public record TrendIndexResult(
    string             ScopeType,
    string?            ScopeValue,
    double?            LatestIndex,
    List<TrendIndexDto> History
);
