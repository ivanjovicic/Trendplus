import { renderToStaticMarkup } from "react-dom/server";
import {
  agingColumns,
  buildInsightStudioExportTrustMetadata,
  buildMarginCoverageCopy,
  categoryColumns,
  changeBadge,
  lifecycleColumns,
  INSIGHT_STUDIO_CATEGORY_CHART_SCOPE_LABEL,
  reorderItemColumns,
  supplierV1Columns,
} from "../InsightStudioPage";

describe("changeBadge", () => {
  it("renders positive, negative, neutral and unknown changes distinctly", () => {
    const positive = renderToStaticMarkup(changeBadge(12.3));
    const negative = renderToStaticMarkup(changeBadge(-4.5));
    const neutral = renderToStaticMarkup(changeBadge(0));
    const unknown = renderToStaticMarkup(changeBadge(null));

    expect(positive).toContain("text-success");
    expect(positive).toContain("\u25B2 12,3%");

    expect(negative).toContain("text-error");
    expect(negative).toContain("\u25BC 4,5%");

    expect(neutral).toContain("text-muted");
    expect(neutral).toContain("0,0%");
    expect(neutral).not.toContain("\u25B2");
    expect(neutral).not.toContain("\u25BC");

    expect(unknown).toContain("text-warning");
    expect(unknown).toContain("Nije dostupno");
  });
});

describe("buildMarginCoverageCopy", () => {
  it("marks low margin coverage as estimated and surfaces revenueWithCost in the tooltip", () => {
    const copy = buildMarginCoverageCopy({
      revenue: 1000,
      marginDataCoveragePct: 72.3,
      revenueWithCost: 680,
    });

    expect(copy.isEstimated).toBe(true);
    expect(copy.subtext).toContain("72,3%");
    expect(copy.subtext).toContain("⚠");
    expect(copy.tooltip).toContain("Pokriće troška: 72,3% prihoda");
    expect(copy.tooltip).toContain("Promet sa troškom: 680 RSD");
    expect(copy.tooltip).toContain("Nepokriven promet: 320 RSD");
    expect(copy.tooltip).toContain("Marža je procena");
  });

  it("keeps high coverage as non-estimated", () => {
    const copy = buildMarginCoverageCopy({
      revenue: 1000,
      marginDataCoveragePct: 91.2,
      revenueWithCost: 912,
    });

    expect(copy.isEstimated).toBe(false);
    expect(copy.subtext).toContain("91,2%");
    expect(copy.subtext).not.toContain("⚠");
    expect(copy.tooltip).toContain("Marža je pokrivena dovoljnim troškom.");
  });
});


describe("Insight Studio export trust context", () => {
  it("exports available coverage, source-basis, lifecycle and aging evidence fields", () => {
    const keys = (columns: { key: string }[]) => columns.map(column => column.key);

    expect(keys(supplierV1Columns)).toEqual(expect.arrayContaining([
      "marginDataCoveragePct", "revenueWithCost", "systemBenchmarkAvailable", "systemMarginPct",
    ]));
    expect(keys(categoryColumns)).toEqual(expect.arrayContaining([
      "marginDataCoveragePct", "revenueWithCost", "revenueBasis", "estimated", "velocityDenominatorBasis",
    ]));
    expect(keys(lifecycleColumns)).toContain("baselineStatus");
    expect(keys(agingColumns)).toEqual(expect.arrayContaining([
      "stockValue", "marginDataAvailable", "neverSold", "agingEvidenceStatus",
    ]));
    expect(keys(reorderItemColumns)).toEqual(expect.arrayContaining([
      "marginDataCoveragePct", "revenueWithCost", "profitReliable", "costCoveragePct",
    ]));
  });

  it("preserves only API-provided quality, freshness and warning metadata with Serbian labels", () => {
    const metadata = buildInsightStudioExportTrustMetadata("ABC classification API", {
      success: true,
      dataQualityStatus: "warning",
      warningCode: "partial_cost_coverage",
      warningMessage: "Some costs are unavailable",
      dataFreshnessStatus: "stale",
      isPartial: true,
    });

    expect(metadata).toEqual(expect.arrayContaining([
      { key: "exportSource", label: "Prikazani izvor", value: "ABC classification API" },
      { key: "responseSuccess", label: "API odgovor uspešan", value: "Da" },
      { key: "dataQualityStatus", label: "Kvalitet podataka", value: "Oprez" },
      { key: "dataFreshnessStatus", label: "Svežina podataka", value: "Zastareli podaci" },
      { key: "isPartial", label: "Delimični podaci", value: "Da" },
      { key: "warningCode", label: "Upozorenje (kod)", value: "partial_cost_coverage" },
      { key: "warningMessage", label: "Upozorenje", value: "Some costs are unavailable" },
    ]));
    expect(metadata.every((entry) => !/Displayed|response|Partial|Warning|freshness/i.test(entry.label))).toBe(true);
    expect(buildInsightStudioExportTrustMetadata("Insight Studio", { success: false, isPartial: false })).toEqual(expect.arrayContaining([
      { key: "responseSuccess", label: "API odgovor uspešan", value: "Ne" },
      { key: "isPartial", label: "Delimični podaci", value: "Ne" },
    ]));
    expect(buildInsightStudioExportTrustMetadata("Insight Studio")).toEqual([
      { key: "exportSource", label: "Prikazani izvor", value: "Insight Studio" },
    ]);
  });
});


describe("Insight Studio chart scope labels", () => {
  it("distinguishes the top-eight category chart from its full table", () => {
    expect(INSIGHT_STUDIO_CATEGORY_CHART_SCOPE_LABEL).toContain("8 kategorija sa najvećim prihodom");
    expect(INSIGHT_STUDIO_CATEGORY_CHART_SCOPE_LABEL).toContain("tabela ispod prikazuje sve kategorije");
  });
});
