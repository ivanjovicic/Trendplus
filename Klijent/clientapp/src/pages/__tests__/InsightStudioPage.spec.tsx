import { renderToStaticMarkup } from "react-dom/server";
import { buildMarginCoverageCopy, changeBadge } from "../InsightStudioPage";

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
    expect(unknown).toContain("N/A");
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
