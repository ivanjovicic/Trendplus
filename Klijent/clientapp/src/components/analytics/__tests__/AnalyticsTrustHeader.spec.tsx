import { render, screen, within } from "@testing-library/react";
import type { ComponentProps } from "react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import AnalyticsTrustHeader from "../AnalyticsTrustHeader";

function renderHeader(overrides: Partial<ComponentProps<typeof AnalyticsTrustHeader>> = {}) {
  return render(
    <MemoryRouter>
      <AnalyticsTrustHeader
        title="Izvršni pregled"
        description="Decision-support pregled sa statusom kvaliteta podataka."
        periodFrom="2026-06-01T00:00:00Z"
        periodTo="2026-06-30T23:59:59Z"
        lastRefreshAt="2026-07-01T08:15:00Z"
        dataFreshnessStatus="fresh"
        dataSource="analytics_daily_summary"
        provenanceBasis="mv_analytics_daily_summary"
        dataQualityStatus="good"
        mode="recommendation"
        dataQualitySummary={{
          missingSupplierCount: 2,
          missingCostCount: 5,
          missingCategoryCount: 1,
          insufficientSignalCount: 3,
          ignoredRowsCount: 4,
        }}
        methodologyHref="/docs/methodology"
        showOperationsTrust
        {...overrides}
      />
    </MemoryRouter>,
  );
}

describe("AnalyticsTrustHeader", () => {
  it("renders decision context, freshness, data quality summary and support links", () => {
    renderHeader();

    expect(screen.getByRole("heading", { name: "Izvršni pregled" })).toBeInTheDocument();
    expect(screen.getByText("Preporuka sistema")).toBeInTheDocument();
    expect(screen.getByText("Podaci deluju pouzdano")).toBeInTheDocument();
    expect(screen.getByText("Sveže")).toBeInTheDocument();
    expect(screen.getByText("analytics_daily_summary")).toBeInTheDocument();
    expect(screen.getByText("mv_analytics_daily_summary")).toBeInTheDocument();

    const summary = screen.getByText("Sažetak kvaliteta podataka").closest(".ath-summary");
    expect(summary).not.toBeNull();
    expect(within(summary as HTMLElement).getByText("Artikli bez dobavljača")).toBeInTheDocument();
    expect(within(summary as HTMLElement).getByText("Redovi bez nabavne cene")).toBeInTheDocument();
    expect(within(summary as HTMLElement).getByText("Ignorisani redovi")).toBeInTheDocument();
    expect(within(summary as HTMLElement).getByText("5")).toBeInTheDocument();

    expect(screen.getByRole("link", { name: "Kvalitet podataka" })).toHaveAttribute("href", "/analytics/data-quality");
    expect(screen.getByRole("link", { name: "Status osvežavanja" })).toHaveAttribute("href", "/admin/configuration?panel=workers");
    expect(screen.getByRole("link", { name: "Metodologija i tumačenje signala" })).toHaveAttribute("href", "/docs/methodology");
  });

  it("explains source freshness evidence and the observed sales horizon", () => {
    renderHeader({
      observedPeriodFrom: "2026-05-12T00:00:00Z",
      observedPeriodTo: "2026-05-28T00:00:00Z",
      dataFreshnessStatus: "unknown",
      meta: {
        success: true,
        dataFreshnessStatus: "unknown",
        dataFreshnessReasonCode: "source_import_not_store_scoped",
        dataFreshnessEvidenceId: null,
      },
    });

    expect(screen.getByText("Nije poznato")).toBeInTheDocument();
    expect(screen.getByText("Import dokaz nije vezan za izabranu prodavnicu")).toBeInTheDocument();
    expect(screen.getByText("Posmatrani period")).toBeInTheDocument();
  });

  it("shows running subtitle while refresh is active", () => {
    renderHeader({ refreshIsRunning: true, refreshCurrentStep: "supplier_decision_mvs" });

    expect(screen.getByText(/Osvežavanje je u toku \(osvežavanje signala dobavljača\)/)).toBeInTheDocument();
    expect(screen.queryByText(/supplier_decision_mvs/)).not.toBeInTheDocument();
  });

  it("shows backend readiness and links the exact integrity evidence context", () => {
    renderHeader({
      meta: {
        success: true,
        context: { fingerprint: "response-context-7" } as never,
        decisionReadiness: {
          state: "signal_only",
          surfaceRole: "signal",
          recommendationAllowed: true,
          reasonCodes: ["supporting_signal"],
          evidenceReferences: ["color.net_sales_signed"],
          repairPath: null,
        },
        operationsIntegrityStatus: "verified",
        operationsIntegrityContextMatches: true,
        operationsIntegrityCheckedAtUtc: "2026-07-01T08:15:00Z",
        operationsIntegrityEvidenceId: "sales-evidence-7",
        operationsIntegrityFamily: "supplier_shoe_type",
        operationsIntegrityContextFingerprint: "integrity-context-7",
        operationsIntegritySourceGeneration: "generation-7",
      },
    });

    expect(screen.getByTestId("analytics-trust-readiness-state")).toHaveTextContent("Samo signal");
    expect(screen.getByTestId("analytics-trust-integrity-state")).toHaveTextContent("Provereno za ovaj kontekst");
    expect(screen.getByText("ID dokaza: sales-evidence-7")).toBeInTheDocument();
    expect(screen.getByText("Kontekst dokaza: integrity-context-7")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Pregledaj dokaz" })).toHaveAttribute(
      "href",
      "/api/analytics/operations-integrity/evidence/sales-evidence-7",
    );
  });

  it("never shows verified when the matching snapshot has no durable evidence ID", () => {
    renderHeader({
      meta: {
        success: true,
        operationsIntegrityStatus: "verified",
        operationsIntegrityContextMatches: true,
        operationsIntegrityContextFingerprint: "integrity-context-without-id",
      },
    });

    expect(screen.getByTestId("analytics-trust-integrity-state")).toHaveTextContent("Nije provereno — dokaz nije dostupan");
    expect(screen.getByTestId("analytics-trust-integrity-state")).not.toHaveClass("ath-trust-state-verified");
    expect(screen.queryByText("Poslednji nalaz: verified")).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Pregledaj dokaz" })).not.toBeInTheDocument();
  });

  it.each([
    { readiness: "decision_ready", integrity: "unverified", contextMatches: true, readinessLabel: "Spremno za odluku", integrityLabel: "Nije provereno" },
    { readiness: "blocked", integrity: "degraded", contextMatches: true, readinessLabel: "Blokirano", integrityLabel: "Provera je degradirana" },
    { readiness: "blocked", integrity: "drift_detected", contextMatches: true, readinessLabel: "Blokirano", integrityLabel: "Odstupanje je otkriveno" },
  ])("renders $readiness / $integrity without upgrading integrity to green", ({ readiness, integrity, contextMatches, readinessLabel, integrityLabel }) => {
    renderHeader({
      meta: {
        success: true,
        decisionReadiness: {
          state: readiness,
          surfaceRole: "recommendation",
          reasonCodes: [],
          evidenceReferences: [],
        },
        operationsIntegrityStatus: integrity,
        operationsIntegrityContextMatches: contextMatches,
        operationsIntegrityEvidenceId: "state-evidence-1",
        operationsIntegrityContextFingerprint: "state-context-1",
      },
    });

    expect(screen.getByTestId("analytics-trust-readiness-state")).toHaveTextContent(readinessLabel);
    const integrityState = screen.getByTestId("analytics-trust-integrity-state");
    expect(integrityState).toHaveTextContent(integrityLabel);
    expect(integrityState).not.toHaveClass("ath-trust-state-verified");
    if (integrity === "drift_detected") expect(integrityState).toHaveClass("ath-trust-state-drift_detected");
  });

  it("fails closed for mismatched generations, missing proof and loading stale data", () => {
    const { rerender } = renderHeader({
      meta: {
        success: true,
        decisionReadiness: { state: "decision_ready", surfaceRole: "recommendation", reasonCodes: [], evidenceReferences: [] },
        operationsIntegrityStatus: "verified",
        operationsIntegrityContextMatches: false,
        operationsIntegrityEvidenceId: "old-context-evidence",
        operationsIntegrityContextFingerprint: "old-integrity-context",
      },
    });

    expect(screen.getByTestId("analytics-trust-integrity-state")).toHaveTextContent("Nije provereno za izabrani kontekst");
    expect(screen.getByTestId("analytics-trust-integrity-state")).not.toHaveClass("ath-trust-state-verified");
    expect(screen.getByText("Poslednji nalaz: verified")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Pregledaj dokaz" })).toHaveAttribute(
      "href",
      "/api/analytics/operations-integrity/evidence/old-context-evidence",
    );

    rerender(
      <MemoryRouter>
        <AnalyticsTrustHeader title="Test" description="Test" mode="signal" trustPending meta={{
          success: true,
          decisionReadiness: { state: "decision_ready", surfaceRole: "signal", reasonCodes: [], evidenceReferences: [] },
          operationsIntegrityStatus: "verified",
          operationsIntegrityContextMatches: true,
          operationsIntegrityEvidenceId: "old-context-evidence",
        }} showOperationsTrust />
      </MemoryRouter>,
    );
    expect(screen.getByTestId("analytics-trust-readiness-state")).toHaveTextContent("Provera u toku");
    expect(screen.getByTestId("analytics-trust-integrity-state")).toHaveTextContent("Provera u toku");
    expect(screen.queryByRole("link", { name: "Pregledaj dokaz" })).not.toBeInTheDocument();
  });

  it("labels Nivelacija without an independent probe as unverified and evidence-free", () => {
    renderHeader({
      meta: {
        success: true,
        decisionReadiness: { state: "blocked", surfaceRole: "report", reasonCodes: ["nivelacija.evidence"], evidenceReferences: [] },
        operationsIntegrityStatus: "unverified",
        operationsIntegrityFamily: "nivelacija",
        operationsIntegrityContextMatches: false,
      },
    });

    expect(screen.getByTestId("analytics-trust-integrity-state")).toHaveTextContent("Nije nezavisno provereno");
    expect(screen.queryByRole("link", { name: "Pregledaj dokaz" })).not.toBeInTheDocument();
  });

  it("prioritizes fallback messaging over gated messaging and keeps dataset lineage visible", () => {
    renderHeader({
      usedFallback: true,
      recommendationAllowed: false,
      requestedDataset: "requested_window",
      effectiveDataset: "all_time",
      effectivePeriodLabel: "All-time fallback",
      fallbackReason: "Nema dovoljno redova u traženom periodu.",
      fallbackReasonCode: "NO_WINDOW_ROWS",
      dataQualityStatus: "warning",
      dataFreshnessStatus: "stale",
      provenanceBasis: "mv_supplier_decision_score_cache_90d",
    });

    expect(screen.getByText("Postoje upozorenja")).toBeInTheDocument();
    expect(screen.getByText("Zastarelo")).toBeInTheDocument();
    expect(screen.getByText("requested_window -> celokupna istorija")).toBeInTheDocument();
    expect(screen.getByText("All-time fallback")).toBeInTheDocument();
    expect(screen.getByText("keš signala odluke dobavljača")).toBeInTheDocument();
    expect(screen.getByText(/Pomoćni skup je aktivan\./i)).toBeInTheDocument();
    expect(screen.getByText(/Nema dovoljno zapisa u traženom periodu/i)).toBeInTheDocument();
    expect(screen.queryByText(/NO_WINDOW_ROWS/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Preporuka je gated/i)).not.toBeInTheDocument();
    expect(screen.getByText(/Prikaz može biti delimičan ili zastareo/i)).toBeInTheDocument();
  });

  it("renders gated and missing-summary states without inventing quality counts", () => {
    renderHeader({
      recommendationAllowed: false,
      dataQualityStatus: "insufficient_data",
      dataQualitySummary: undefined,
      requestedDataset: null,
      effectiveDataset: null,
      mode: "recommendation",
      recommendationNote: "Ne prikazuj konačnu preporuku bez jačeg signala.",
      emptyStateReason: "Nema dovoljno podataka za izabrani period.",
    });

    expect(screen.getByText("Preporuka sistema")).toBeInTheDocument();
    expect(screen.getByText("Nedovoljno podataka")).toBeInTheDocument();
    expect(screen.getByText(/Preporuka je gated/i)).toBeInTheDocument();
    expect(screen.getByText("Detaljan kvalitet podataka nije dostupan za ovaj ekran.")).toBeInTheDocument();
    expect(screen.getByText("Ne prikazuj konačnu preporuku bez jačeg signala.")).toBeInTheDocument();
    expect(screen.getByText("Nema dovoljno podataka za izabrani period.")).toBeInTheDocument();
    expect(screen.queryByText("Skup podataka")).not.toBeInTheDocument();
  });

  it.each([
    { mode: "signal" as const, recommendationAllowed: undefined },
    { mode: "signal" as const, recommendationAllowed: false },
    { mode: "signal" as const, recommendationAllowed: true },
    { mode: "report" as const, recommendationAllowed: undefined },
    { mode: "report" as const, recommendationAllowed: false },
    { mode: "report" as const, recommendationAllowed: true },
  ])("does not invent a recommendation gate for $mode when permission is $recommendationAllowed", ({ mode, recommendationAllowed }) => {
    renderHeader({ mode, recommendationAllowed });

    expect(screen.queryByText(/Preporuka je gated/i)).not.toBeInTheDocument();
  });

  it.each([
    { mode: "recommendation" as const, recommendationAllowed: undefined },
    { mode: "recommendation" as const, recommendationAllowed: false },
  ])("keeps the recommendation gate for recommendation mode when permission is $recommendationAllowed", ({ mode, recommendationAllowed }) => {
    renderHeader({ mode, recommendationAllowed });

    expect(screen.getByText(/Preporuka je gated/i)).toBeInTheDocument();
  });

  it.each([
    { value: " STALE ", label: "Zastarelo" },
    { value: "CRITICAL", label: "Kritično" },
  ])("preserves $label freshness warning for harmless token formatting", ({ value, label }) => {
    renderHeader({ dataFreshnessStatus: value });

    expect(screen.getByText(label)).toBeInTheDocument();
  });

  it("keeps supported freshness normalization and fails closed for unknown tokens and non-finite counts", () => {
    renderHeader({
      dataFreshnessStatus: "  STALE ",
      refreshIsRunning: true,
      refreshCurrentStep: "internal_secret_step",
      usedFallback: true,
      fallbackReasonCode: "internal_secret_fallback",
      dataQualitySummary: {
        missingSupplierCount: Number.NaN,
        missingCostCount: Number.POSITIVE_INFINITY,
        missingCategoryCount: -2,
        insufficientSignalCount: 0,
        ignoredRowsCount: Number.NEGATIVE_INFINITY,
      },
    });

    expect(screen.getByText("Zastarelo")).toBeInTheDocument();
    expect(screen.getByText(/Osvežavanje je u toku \(Obrada podataka\)/)).toBeInTheDocument();
    expect(screen.getByText(/Dodatni razlog pomoćnog skupa nije naveden/i)).toBeInTheDocument();
    expect(screen.queryByText(/internal_secret/i)).not.toBeInTheDocument();
    expect(screen.queryByText("NaN")).not.toBeInTheDocument();
    expect(screen.queryByText("Infinity")).not.toBeInTheDocument();
    expect(screen.getByText("-2")).toBeInTheDocument();
    expect(screen.getByText("0")).toBeInTheDocument();
  });

  it("does not expose technical fallback reasons", () => {
    renderHeader({
      usedFallback: true,
      fallbackReason: "sql_timeout_v2 at internal_table",
      fallbackReasonCode: "internal_secret_fallback",
    });

    expect(screen.queryByText(/sql_timeout_v2|internal_table|internal_secret_fallback/i)).not.toBeInTheDocument();
    expect(screen.getAllByText(/Dodatni razlog pomoćnog skupa nije naveden/i).length).toBeGreaterThan(0);
  });

  it("does not crash on malformed optional text fields", () => {
    renderHeader({
      dataSource: {} as never,
      requestedDataset: {} as never,
      effectiveDataset: {} as never,
      recommendationNote: {} as never,
      emptyStateReason: {} as never,
      fallbackReason: {} as never,
    });

    expect(screen.getByText("Izvor podataka nije naveden")).toBeInTheDocument();
  });
});
