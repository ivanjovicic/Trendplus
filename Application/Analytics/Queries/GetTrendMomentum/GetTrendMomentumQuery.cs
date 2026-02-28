using MediatR;
using System;
using System.Collections.Generic;

namespace Application.Analytics.Queries.GetTrendMomentum;

public record GetTrendMomentumQuery(
    DateOnly? Date   = null,
    string?   Market = null,
    string?   Brand  = null,
    int       Top    = 50,
    bool      Rising = true   // true = sort by momentum desc, false = asc (falling)
) : IRequest<TrendMomentumResult>;

public record TrendMomentumDto(
    string   CanonicalKey,
    string   ProductName,
    string   Brand,
    DateOnly SnapshotDate,
    double   MomentumScore,
    double   ScoreDelta,
    int      RankDelta,
    bool     IsNewEntry,
    double?  TodayScore,
    int?     TodayRank
);

public record TrendMomentumResult(
    DateOnly               Date,
    int                    TotalProducts,
    List<TrendMomentumDto> Items
);
