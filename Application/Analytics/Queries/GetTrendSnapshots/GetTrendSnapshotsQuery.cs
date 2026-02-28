using MediatR;
using System;
using System.Collections.Generic;

namespace Application.Analytics.Queries.GetTrendSnapshots;

public record GetTrendSnapshotsQuery(
    DateOnly? Date     = null,
    string?   Market   = null,
    string?   Brand    = null,
    string?   Category = null,
    int       Top      = 50
) : IRequest<TrendSnapshotsResult>;

public record TrendSnapshotDto(
    long     Id,
    DateOnly SnapshotDate,
    string   CanonicalKey,
    string   ProductName,
    string   Brand,
    string?  Category,
    string?  Market,
    double   Score,
    int      RankGlobal,
    double?  SocialScore,
    int      SourceCount,
    int      UniqueSources
);

public record TrendSnapshotsResult(
    DateOnly           Date,
    int                TotalProducts,
    List<TrendSnapshotDto> Items
);
