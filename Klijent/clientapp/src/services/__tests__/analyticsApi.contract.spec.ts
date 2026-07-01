import { rest } from "msw";
import { describe, expect, it } from "vitest";
import { server } from "../../mocks/server";
import {
  getAnalyticsActions,
  getAnalyticsActionOutcomeSummary,
  getDataQualityIssues,
  getDataQualityTopOffenders,
  getDecisionBoardAggregate,
  updateAnalyticsActionOutcome,
} from "../analyticsApi";
import { getColorSalesStats } from "../colorSalesStatsApi";

describe("analytics API contract requests", () => {
  it("requests Decision Board aggregate with explicit filters and default data scope", async () => {
    let receivedUrl: URL | null = null;

    server.use(
      rest.get("/api/analytics/decision-board", (req, res, ctx) => {
        receivedUrl = req.url;
        return res(ctx.status(200), ctx.json({
          generatedAtUtc: "2026-07-01T08:00:00Z",
          periodFromUtc: "2026-06-01T00:00:00Z",
          periodToUtc: "2026-07-01T00:00:00Z",
          lastRefreshAtUtc: "2026-07-01T07:55:00Z",
          overallDataQualityStatus: "warning",
          recommendationNote: "Backend aggregate is source of truth.",
          warnings: ["BOARD_PARTIAL"],
          metrics: [],
          sourceStates: [],
          sections: [],
          meta: { success: true, dataQualityStatus: "warning" },
        }));
      }),
    );

    const result = await getDecisionBoardAggregate({
      fromDate: "2026-06-01T00:00:00Z",
      toDate: "2026-07-01T00:00:00Z",
      storeId: 2,
      supplierId: 7,
      category: "Sandale",
      gender: "Ž",
      seasonId: 3,
      minRevenue: 10000,
      onlyHighConfidence: true,
      excludeOosBeforeMarkdown: false,
      search: "crna",
    });

    expect(result.overallDataQualityStatus).toBe("warning");
    expect(receivedUrl?.searchParams.get("fromDate")).toBe("2026-06-01T00:00:00Z");
    expect(receivedUrl?.searchParams.get("toDate")).toBe("2026-07-01T00:00:00Z");
    expect(receivedUrl?.searchParams.get("storeId")).toBe("2");
    expect(receivedUrl?.searchParams.get("supplierId")).toBe("7");
    expect(receivedUrl?.searchParams.get("category")).toBe("Sandale");
    expect(receivedUrl?.searchParams.get("gender")).toBe("Ž");
    expect(receivedUrl?.searchParams.get("seasonId")).toBe("3");
    expect(receivedUrl?.searchParams.get("minRevenue")).toBe("10000");
    expect(receivedUrl?.searchParams.get("onlyHighConfidence")).toBe("true");
    expect(receivedUrl?.searchParams.get("excludeOosBeforeMarkdown")).toBe("false");
    expect(receivedUrl?.searchParams.get("search")).toBe("crna");
    expect(receivedUrl?.searchParams.get("dataScope")).toBe("all");
  });

  it("requests Color Sales Stats using the dedicated color endpoint contract", async () => {
    let receivedUrl: URL | null = null;

    server.use(
      rest.get("/api/analytics/color-sales-stats", (req, res, ctx) => {
        receivedUrl = req.url;
        return res(ctx.status(200), ctx.json({
          generatedAt: "2026-07-01T08:00:00Z",
          fromDate: "2026-06-01T00:00:00Z",
          toDate: "2026-07-01T00:00:00Z",
          dataWindowFrom: "2024-01-01T00:00:00Z",
          dataWindowTo: "2026-07-01T00:00:00Z",
          sezonaId: 3,
          storeId: 2,
          dataScope: "imported",
          colors: [],
          totals: {
            ukupanPromet: 0,
            ukupanMarzniDoprinos: 0,
            prePromet: 0,
            poslePromet: 0,
            ukupnaKolicina: 0,
            preKolicina: 0,
            posleKolicina: 0,
            previousPeriodRevenue: null,
            previousPeriodUnits: null,
            brojBoja: 0,
            popRevenueChangePct: null,
            popUnitsChangePct: null,
            prePostNivelacijaRevenueImpactPct: null,
            prePostNivelacijaUnitsImpactPct: null,
          },
          dataQuality: {
            missingCostRevenue: 0,
            missingCostRevenueSharePct: null,
            unknownColorRevenue: 0,
            unknownColorRevenueSharePct: null,
            revenueWithNivelacijaSplit: 0,
            revenueWithNivelacijaSplitSharePct: null,
          },
          sezone: [],
        }));
      }),
    );

    const result = await getColorSalesStats({
      fromDate: "2026-06-01T00:00:00Z",
      toDate: "2026-07-01T00:00:00Z",
      sezonaId: 3,
      storeId: 2,
      dataScope: "imported",
    });

    expect(result.sezonaId).toBe(3);
    expect(receivedUrl?.pathname).toBe("/api/analytics/color-sales-stats");
    expect(receivedUrl?.searchParams.get("fromDate")).toBe("2026-06-01T00:00:00Z");
    expect(receivedUrl?.searchParams.get("toDate")).toBe("2026-07-01T00:00:00Z");
    expect(receivedUrl?.searchParams.get("sezonaId")).toBe("3");
    expect(receivedUrl?.searchParams.get("storeId")).toBe("2");
    expect(receivedUrl?.searchParams.get("dataScope")).toBe("imported");
  });

  it("requests Analytics Actions list and outcome summary with matching filter semantics", async () => {
    const receivedPaths: string[] = [];

    server.use(
      rest.get("/api/analytics/actions", (req, res, ctx) => {
        receivedPaths.push(`actions:${req.url.searchParams.toString()}`);
        return res(ctx.status(200), ctx.json({
          items: [],
          totalCount: 0,
          page: 2,
          pageSize: 25,
          totalPages: 0,
        }));
      }),
      rest.get("/api/analytics/actions/outcomes/summary", (req, res, ctx) => {
        receivedPaths.push(`summary:${req.url.searchParams.toString()}`);
        return res(ctx.status(200), ctx.json({
          meta: {
            success: true,
            periodMode: "created",
            createdFrom: "2026-04-01T00:00:00Z",
            createdTo: "2026-07-01T00:00:00Z",
            generatedAtUtc: "2026-07-01T08:00:00Z",
            sampleSize: 0,
            measuredSampleSize: 0,
            warnings: [],
            emptyReason: "empty",
          },
          totals: {
            createdCount: 0,
            closedCount: 0,
            openCount: 0,
            measuredCount: 0,
            pendingOutcomeCount: 0,
            successCount: 0,
            neutralCount: 0,
            negativeCount: 0,
            notMeasuredCount: 0,
          },
          impact: { measuredImpactSampleCount: 0 },
          bySourceType: [],
          byPriority: [],
          byOutcomeStatus: [],
          byDataQuality: [],
          byConfidenceBucket: [],
          byReliabilityBucket: [],
        }));
      }),
    );

    await getAnalyticsActions({
      status: "accepted",
      priority: "P1",
      sourceType: "inventory",
      dataQualityStatus: "warning",
      search: "dopuna",
      page: 2,
      pageSize: 25,
    });
    await getAnalyticsActionOutcomeSummary({
      createdFrom: "2026-04-01T00:00:00Z",
      createdTo: "2026-07-01T00:00:00Z",
      sourceType: "inventory",
      priority: "P1",
      dataQualityStatus: "warning",
    });

    expect(receivedPaths[0]).toContain("status=accepted");
    expect(receivedPaths[0]).toContain("priority=P1");
    expect(receivedPaths[0]).toContain("sourceType=inventory");
    expect(receivedPaths[0]).toContain("dataQualityStatus=warning");
    expect(receivedPaths[0]).toContain("search=dopuna");
    expect(receivedPaths[0]).toContain("page=2");
    expect(receivedPaths[0]).toContain("pageSize=25");
    expect(receivedPaths[0]).toContain("dataScope=all");
    expect(receivedPaths[1]).toContain("createdFrom=2026-04-01T00%3A00%3A00Z");
    expect(receivedPaths[1]).toContain("createdTo=2026-07-01T00%3A00%3A00Z");
    expect(receivedPaths[1]).toContain("sourceType=inventory");
    expect(receivedPaths[1]).toContain("priority=P1");
    expect(receivedPaths[1]).toContain("dataQualityStatus=warning");
    expect(receivedPaths[1]).toContain("dataScope=all");
  });

  it("submits Analytics Action outcome PATCH with null evidence for pending states", async () => {
    let receivedBody: unknown = null;

    server.use(
      rest.patch("/api/analytics/actions/:id/outcome", async (req, res, ctx) => {
        receivedBody = await req.json();
        return res(ctx.status(200), ctx.json({
          id: Number(req.params.id),
          sourceType: "inventory",
          sourceKey: "inventory:101",
          title: "Dopuni artikal",
          priority: "P1",
          status: "accepted",
          outcomeStatus: "pending",
          measuredImpactRsd: null,
          outcomeMeasuredAtUtc: null,
          outcomeNotes: "Čeka merenje.",
          dataQualityStatus: "warning",
          createdAtUtc: "2026-07-01T08:00:00Z",
          updatedAtUtc: "2026-07-01T08:00:00Z",
        }));
      }),
    );

    const result = await updateAnalyticsActionOutcome(101, {
      outcomeStatus: "pending",
      measuredImpactRsd: null,
      outcomeMeasuredAtUtc: null,
      outcomeNotes: "Čeka merenje.",
    });

    expect(result.outcomeStatus).toBe("pending");
    expect(receivedBody).toEqual({
      outcomeStatus: "pending",
      measuredImpactRsd: null,
      outcomeMeasuredAtUtc: null,
      outcomeNotes: "Čeka merenje.",
    });
  });

  it("requests Data Quality issue and top-offender endpoints with explicit scope", async () => {
    const received: string[] = [];

    server.use(
      rest.get("/api/analytics/data-quality/list", (req, res, ctx) => {
        received.push(`issues:${req.url.searchParams.toString()}`);
        return res(ctx.status(200), ctx.json({
          page: 3,
          pageSize: 10,
          total: 0,
          items: [],
          meta: { success: true, dataQualityStatus: "warning" },
        }));
      }),
      rest.get("/api/analytics/data-quality/top-offenders", (req, res, ctx) => {
        received.push(`top:${req.url.searchParams.toString()}`);
        return res(ctx.status(200), ctx.json({
          issueType: "missingSupplier",
          limit: 5,
          count: 0,
          items: [],
          meta: { success: true },
        }));
      }),
    );

    await getDataQualityIssues({
      type: "missingSupplier",
      page: 3,
      pageSize: 10,
      q: "sandala",
      sortBy: "sales30d",
      sortDir: "desc",
      dataScope: "imported",
    });
    await getDataQualityTopOffenders("missingSupplier", 5, "imported");

    expect(received[0]).toContain("type=missingSupplier");
    expect(received[0]).toContain("page=3");
    expect(received[0]).toContain("pageSize=10");
    expect(received[0]).toContain("q=sandala");
    expect(received[0]).toContain("sortBy=sales30d");
    expect(received[0]).toContain("sortDir=desc");
    expect(received[0]).toContain("dataScope=imported");
    expect(received[1]).toContain("issueType=missingSupplier");
    expect(received[1]).toContain("limit=5");
    expect(received[1]).toContain("dataScope=imported");
  });
});
