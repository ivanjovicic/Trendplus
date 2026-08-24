using System;
using System.Collections.Generic;
using Application.Analytics;
using Application.Artikli.Common.Interfaces;
using Domain.Model;
using Domain.Model.Analytics;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;

namespace Api.Tests;

internal static class PilotAnalyticsSeedPack
{
    public const string PackId = "pilot-analytics-proof-pack-v1";

    public static readonly DateTime ProductDecisionFromUtc = new(2026, 5, 21, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime ProductDecisionToUtc = new(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime InventoryUpdatedUtc = new(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime InventoryUpdatedMinusTwoMinutesUtc = InventoryUpdatedUtc.AddMinutes(-2);
    public static readonly DateTime InventoryUpdatedMinusThreeMinutesUtc = InventoryUpdatedUtc.AddMinutes(-3);
    public static readonly DateTime InventoryUpdatedMinusFourMinutesUtc = InventoryUpdatedUtc.AddMinutes(-4);

    public static readonly IReadOnlyList<PilotAnalyticsSeedFamilySpec> Families =
    [
        new(
            Family: "dashboard",
            CanonicalBasis: "Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.DashboardBootstrap_SeededData_ReturnsNonEmptyExecutiveSnapshot",
            RequestedPeriod: "2026-01-05 through 2026-01-07",
            Scope: "storeId=1, dataScope=all",
            ExpectedOutputs:
            [
                "summary totalRevenue stays 1100",
                "executive topSuppliers and topMarginProducts stay non-empty",
                "dashboard meta stays success"
            ],
            AllowedStates:
            [
                "explicit warning / partial state",
                "explicit empty source state",
                "honest freshness / provenance warning"
            ],
            ProofFiles:
            [
                "Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs",
                "Api.Tests/CachedAnalyticsOperationalFallbackTests.cs",
                "docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md"
            ]),
        new(
            Family: "product-decision-center",
            CanonicalBasis: "Api.Tests/ProductDecisionCenterBuilderIntegrationTests.SeedDecisionDataAsync",
            RequestedPeriod: "2026-05-21 through 2026-06-19",
            Scope: "storeId=1, supplierId=null, dataScope=all",
            ExpectedOutputs:
            [
                "row 101 stays REPLENISH with expectedImpactRsd=500",
                "row 102 stays FIX_DATA with critical data-quality blockers",
                "summary counts remain 1 replenish / 1 bad-data row",
                "unknown store returns explicit empty success meta",
                "freshness is intentionally historical and may surface as stale"
            ],
            AllowedStates:
            [
                "no_rows_for_period",
                "insufficient_data",
                "stale freshness"
            ],
            ProofFiles:
            [
                "Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs",
                "Api.Tests/DecisionBoardEndpointsTests.cs",
                "Api.Tests/AnalyticsReportsContractTests.cs"
            ]),
        new(
            Family: "supplier-decision-sales",
            CanonicalBasis: "Api.Tests/Fixtures/supplier-sales-stats-seed.sql + Api.Tests/SupplierDecisionHubContractTests.cs",
            RequestedPeriod: "2026-02-15 through 2026-03-15",
            Scope: "sezonaId=1, dataScope=all",
            ExpectedOutputs:
            [
                "Supplier A remains the dominant deterministic row in the integration fixture",
                "unknown supplier data normalizes into the Nepoznato bucket",
                "missing cost metadata remains visible in the data-quality slice",
                "supplier-sales stats JSON stays deterministic for identical requests"
            ],
            AllowedStates:
            [
                "explicit empty dataset",
                "warning / partial trust state",
                "unavailable report"
            ],
            ProofFiles:
            [
                "Api.Tests/Fixtures/supplier-sales-stats-seed.sql",
                "Api.Tests/SupplierDecisionHubContractTests.cs",
                "Api.Tests/AnalyticsSupplierSalesIntegrationTests.cs",
                "Api.Tests/AnalyticsReportsContractTests.cs"
            ]),
        new(
            Family: "inventory",
            CanonicalBasis: "Api.Tests/InventoryListEndpointIntegrationTests.Seed",
            RequestedPeriod: "n/a",
            Scope: "cached and uncached inventory list/detail/insights routes",
            ExpectedOutputs:
            [
                "OOS-101 remains out-of-stock risk with recommendation allowed",
                "EMPTY-104 remains insufficient_data with recommendation blocked",
                "pagination and value sort stay deterministic",
                "explicit empty filter stays a successful empty response"
            ],
            AllowedStates:
            [
                "explicit empty success",
                "insufficient_data",
                "unavailable dependency",
                "partial / warning"
            ],
            ProofFiles:
            [
                "Api.Tests/InventoryListEndpointIntegrationTests.cs",
                "Api.Tests/ObservedInventoryDailySnapshotTests.cs",
                "Api.Tests/InventorySnapshotContractTests.cs"
            ]),
        new(
            Family: "analytics-actions",
            CanonicalBasis: "Api.Tests/AnalyticsActionsEndpointsTests.AnalyticsActionsTestHost.SeedActionAsync",
            RequestedPeriod: "n/a",
            Scope: "/api/analytics/actions and action status/outcome patch flows",
            ExpectedOutputs:
            [
                "admin key is required for protected mutations",
                "ledger snapshot records preserve source recommendation metadata",
                "status/outcome patches round-trip through the action store"
            ],
            AllowedStates:
            [
                "explicit empty list",
                "unavailable source state",
                "warning state for partial/outcome gaps"
            ],
            ProofFiles:
            [
                "Api.Tests/AnalyticsActionsEndpointsTests.cs",
                "Api.Tests/AnalyticsActionTimelineFilterProjectionTests.cs"
            ]),
        new(
            Family: "decision-board",
            CanonicalBasis: "Api.Tests/DecisionBoardEndpointsTests.CreateProductRow + product-decision seed pack",
            RequestedPeriod: "2026-06-19 snapshot",
            Scope: "decision board aggregate sections and urgency/impact sections",
            ExpectedOutputs:
            [
                "expectedImpactRsd is preserved when present",
                "blocked statuses keep lost sales off the impact section",
                "insufficient_data remains a successful empty-style board state"
            ],
            AllowedStates:
            [
                "section-level no-signals state",
                "honest blocker / warning state",
                "explicit unavailable aggregate state"
            ],
            ProofFiles:
            [
                "Api.Tests/DecisionBoardEndpointsTests.cs"
            ]),
        new(
            Family: "pilot-intake-readiness",
            CanonicalBasis: "Api.Tests/AnalyticsReportsContractTests.CreatePilotIntakeReport",
            RequestedPeriod: "pilot-intake report window used by the readiness page",
            Scope: "/analytics/pilot-readiness and /analytics/reports/pilot-intake",
            ExpectedOutputs:
            [
                "ready readiness datasets remain success and populate KPIs",
                "below-threshold readiness disables recommendation but stays visible",
                "no-import stays explicit empty success with non-fake-zero copy"
            ],
            AllowedStates:
            [
                "explicit empty success",
                "insufficient_data",
                "warning / degraded report state"
            ],
            ProofFiles:
            [
                "Api.Tests/AnalyticsReportsContractTests.cs",
                "Klijent/clientapp/src/pages/PilotReadinessPage.tsx"
            ])
    ];

    public static void SeedProductDecisionCenter(TrendplusDbContext db, DateTime fromDate, DateTime toDate)
    {
        db.Dobavljaci.Add(new Dobavljac
        {
            Id = 1,
            Naziv = "Pouzdan dobavljač",
            DataOrigin = "existing"
        });

        db.Artikli.AddRange(
            new Artikli
            {
                Id = 101,
                PLU = "SKU-101",
                Naziv = "Model za dopunu",
                IDDobavljac = 1,
                IDObjekat = 1,
                Kolicina = 0,
                MinimalnaKolicina = 5,
                NabavnaCena = 50m,
                Kategorija = "Patike",
                Boja = "Crna",
                Velicina = "42",
                DataOrigin = "existing",
                UpdatedAt = toDate
            },
            new Artikli
            {
                Id = 102,
                PLU = "SKU-102",
                Naziv = "Model sa lošim podacima",
                IDDobavljac = null,
                IDObjekat = 1,
                Kolicina = 1,
                MinimalnaKolicina = 3,
                NabavnaCena = null,
                Kategorija = null,
                Boja = null,
                Velicina = null,
                DataOrigin = "existing",
                UpdatedAt = toDate
            });

        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje
            {
                Id = 1,
                DatumProdaje = toDate.AddHours(10),
                IDObjekat = 1,
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 2,
                DatumProdaje = fromDate.AddDays(-5).AddHours(10),
                IDObjekat = 1,
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 3,
                DatumProdaje = toDate.AddHours(11),
                IDObjekat = 1,
                DataOrigin = "existing"
            });

        db.ProdajaStavke.AddRange(
            new ProdajaStavka
            {
                Id = 11,
                IdProdaja = 1,
                IdArtikal = 101,
                Kolicina = 30,
                Cena = 100m,
                NabavnaCena = 50m
            },
            new ProdajaStavka
            {
                Id = 12,
                IdProdaja = 2,
                IdArtikal = 101,
                Kolicina = 30,
                Cena = 100m,
                NabavnaCena = 50m
            },
            new ProdajaStavka
            {
                Id = 13,
                IdProdaja = 3,
                IdArtikal = 102,
                Kolicina = 2,
                Cena = 200m,
                NabavnaCena = null
            });
    }

    public static void SeedInventory(TrendplusDbContext db, DateTime? baseUtc = null)
    {
        var now = baseUtc ?? DateTime.UtcNow;

        db.Dobavljaci.AddRange(
            new Dobavljac { Id = 1, Naziv = "Dobavljač A", DataOrigin = "existing" },
            new Dobavljac { Id = 2, Naziv = "Dobavljač B", DataOrigin = "existing" });

        db.Artikli.AddRange(
            new Artikli
            {
                Id = 101,
                PLU = "OOS-101",
                Naziv = "Model OOS",
                IDObjekat = 1,
                IDDobavljac = 1,
                Kolicina = 0,
                MinimalnaKolicina = 5,
                NabavnaCena = 100m,
                DataOrigin = "existing",
                UpdatedAt = now
            },
            new Artikli
            {
                Id = 102,
                PLU = "HEALTHY-102",
                Naziv = "Model Healthy",
                IDObjekat = 1,
                IDDobavljac = 1,
                Kolicina = 10,
                MinimalnaKolicina = 2,
                NabavnaCena = 200m,
                DataOrigin = "existing",
                UpdatedAt = now.AddMinutes(-2)
            },
            new Artikli
            {
                Id = 103,
                PLU = "OTHER-103",
                Naziv = "Drugi artikal",
                IDObjekat = 1,
                IDDobavljac = 2,
                Kolicina = 5,
                MinimalnaKolicina = 1,
                NabavnaCena = 100m,
                DataOrigin = "existing",
                UpdatedAt = now.AddMinutes(-3)
            },
            new Artikli
            {
                Id = 104,
                PLU = "EMPTY-104",
                Naziv = "Bez signala",
                IDObjekat = 2,
                IDDobavljac = 1,
                Kolicina = 0,
                MinimalnaKolicina = 2,
                NabavnaCena = 50m,
                DataOrigin = "existing",
                UpdatedAt = now.AddMinutes(-4)
            });

        db.ProdajaZaglavlja.AddRange(
            new ProdajaZaglavlje
            {
                Id = 201,
                DatumProdaje = now.AddDays(-5),
                IDObjekat = 1,
                DataOrigin = "existing"
            },
            new ProdajaZaglavlje
            {
                Id = 202,
                DatumProdaje = now.AddDays(-4),
                IDObjekat = 1,
                DataOrigin = "existing"
            });

        db.ProdajaStavke.AddRange(
            new ProdajaStavka { Id = 301, IdProdaja = 201, IdArtikal = 101, Kolicina = 12, Cena = 250m, NabavnaCena = 100m },
            new ProdajaStavka { Id = 302, IdProdaja = 202, IdArtikal = 102, Kolicina = 4, Cena = 400m, NabavnaCena = 200m },
            new ProdajaStavka { Id = 303, IdProdaja = 202, IdArtikal = 103, Kolicina = 5, Cena = 160m, NabavnaCena = 100m });

        db.DnevnikPromena.AddRange(
            new DnevnikPromena
            {
                Id = 401,
                ArtikalId = 101,
                Datum = now.AddDays(-5),
                TipPromene = TipPromeneConstants.Prodaja,
                Kolicina = -12,
                IDObjekat = 1
            },
            new DnevnikPromena
            {
                Id = 402,
                ArtikalId = 102,
                Datum = now.AddDays(-10),
                TipPromene = TipPromeneConstants.UlazRobe,
                Kolicina = 5,
                IDObjekat = 1
            });
    }

    public static AnalyticsActionItem CreateCanonicalAnalyticsActionItem(
        string sourceType,
        string sourceKey,
        string? status = null,
        string? outcomeStatus = null,
        decimal? measuredImpactRsd = null,
        DateTime? outcomeMeasuredAtUtc = null,
        string? outcomeNotes = null,
        string? metadataJson = null,
        DateTime? createdAtUtc = null,
        DateTime? updatedAtUtc = null,
        DateTime? dueAtUtc = null)
    {
        var createdUtc = createdAtUtc ?? new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
        var updatedUtc = updatedAtUtc ?? new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
        var dueUtc = dueAtUtc ?? new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        return new AnalyticsActionItem
        {
            SourceType = sourceType,
            SourceKey = sourceKey,
            SourceId = 101,
            Title = "Proveri dopunu",
            Description = "Proveriti efekat akcije",
            RecommendationStatus = "dopuna",
            Priority = AnalyticsActionConstants.Priorities.P1,
            Status = status ?? AnalyticsActionConstants.Statuses.Accepted,
            CreatedAtUtc = createdUtc,
            UpdatedAtUtc = updatedUtc,
            DueAtUtc = dueUtc,
            ExpectedImpactRsd = 5000m,
            OutcomeStatus = outcomeStatus,
            MeasuredImpactRsd = measuredImpactRsd,
            OutcomeMeasuredAtUtc = outcomeMeasuredAtUtc,
            OutcomeNotes = outcomeNotes,
            MetadataJson = metadataJson,
        };
    }
}

internal sealed record PilotAnalyticsSeedFamilySpec(
    string Family,
    string CanonicalBasis,
    string RequestedPeriod,
    string Scope,
    IReadOnlyList<string> ExpectedOutputs,
    IReadOnlyList<string> AllowedStates,
    IReadOnlyList<string> ProofFiles);
