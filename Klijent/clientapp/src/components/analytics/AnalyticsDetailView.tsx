import React from "react";
import { Copy, RefreshCw } from "lucide-react";
import InfoTip from "../ui/InfoTip";
import { getAnalyticsDetail } from "../../services/analyticsDetailApi";
import { getAnalyticsDetailSnapshot } from "../../services/analyticsTableState";
import type { AnalyticsDetailResponse } from "../../types/analyticsTable";
import { InventoryState } from "../inventory/InventoryPageShell";
import { normalizeRecommendationPct } from "../../utils/canonicalRecommendationSemantics";

function DetailSkeleton() {
  return (
    <div className="space-y-4 animate-pulse">
      {[0, 1, 2].map((section) => (
        <section key={section} className="rounded-2xl border border-muted bg-surface-elevated p-4">
          <div className="mb-4 h-4 w-40 rounded bg-muted/20" />
          <div className="space-y-3">
            {[0, 1, 2, 3].map((row) => (
              <div key={row} className="grid gap-2 sm:grid-cols-[160px_1fr]">
                <div className="h-3 w-24 rounded bg-muted/10" />
                <div className="h-4 w-full rounded bg-muted/15" />
              </div>
            ))}
          </div>
        </section>
      ))}
    </div>
  );
}

function DetailRow(props: { label: string; value?: string | null; highlight?: boolean }) {
  return (
    <div className="grid gap-1 border-b border-muted/30 py-3 sm:grid-cols-[160px_1fr] sm:gap-3">
      <div className="text-xs uppercase tracking-wide text-muted">{props.label}</div>
      <div className={props.highlight ? "font-semibold text-success" : "text-contrast"}>
        {props.value || "-"}
      </div>
    </div>
  );
}

function formatDetailReasonCode(code: string): string {
  const labels: Record<string, string> = {
    unknown_entity: "Nepoznat identitet",
    new_entity: "Novi tip u odnosu na prethodni period",
    previous_period_missing: "Nedostaje prethodni uporediv period",
    no_previous_baseline: "Nema prethodne osnove",
    missing_known_margin_baseline: "Nedostaje osnova poznate marže",
    unknown_bucket_share_unavailable: "Nedostaje udeo nepoznatih podataka",
    missing_cost_coverage: "Nedovoljno pokriće troškom",
    missing_split_coverage: "Nedostaje uporediv signal nivelacije",
    limited_nivelacija_coverage: "Ograničeno pokriće nivelacije",
    unknown_heavy_dataset: "Veliki udeo nepoznatih podataka",
    tiny_sample: "Premali uzorak",
    unstable_margin: "Nestabilan signal marže",
    pop_unavailable: "PoP poređenje nije dostupno",
  };
  return labels[code] ?? "Dodatno ograničenje procene";
}

function formatDataQualityStatus(value: string): string {
  return value === "good" ? "Dobro" : value === "warning" ? "Upozorenje" : "Kritično";
}

async function copyValue(value: string) {
  try {
    await navigator.clipboard.writeText(value);
  } catch {
    // best effort copy
  }
}

export default function AnalyticsDetailView(props: {
  table: string;
  recordId: string;
  queryString?: string;
}) {
  const [detail, setDetail] = React.useState<AnalyticsDetailResponse | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [usedSnapshot, setUsedSnapshot] = React.useState(false);

  const load = React.useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      console.info("Detail opened", { table: props.table, id: props.recordId });
      const backend = await getAnalyticsDetail(props.table, props.recordId, props.queryString);
      if (backend) {
        setDetail(backend);
        setUsedSnapshot(false);
        return;
      }

      const snapshot = getAnalyticsDetailSnapshot(props.table, props.recordId);
      setDetail(snapshot);
      setUsedSnapshot(snapshot != null);
    } catch (reason) {
      const snapshot = getAnalyticsDetailSnapshot(props.table, props.recordId);
      if (snapshot) {
        setDetail(snapshot);
        setUsedSnapshot(true);
        return;
      }

      setError(reason instanceof Error ? reason.message : "Greška pri učitavanju detalja.");
      setDetail(null);
    } finally {
      setLoading(false);
    }
  }, [props.recordId, props.queryString, props.table]);

  React.useEffect(() => {
    void load();
  }, [load]);

  if (loading) {
    return <DetailSkeleton />;
  }

  if (error) {
    return (
      <div className="space-y-3">
        <InventoryState message={error} tone="danger" />
        <div className="flex justify-center">
          <button
            type="button"
            onClick={() => void load()}
            className="inline-flex items-center gap-2 rounded-lg border border-info bg-info/10 px-3 py-2 text-xs font-semibold text-info"
          >
            <RefreshCw size={14} />
            Pokusaj ponovo
          </button>
        </div>
      </div>
    );
  }

  if (!detail) {
    return <InventoryState message="Detalj nije pronadjen za izabrani zapis." tone="neutral" />;
  }

  const recommendationReliability = detail.recommendation
    ? normalizeRecommendationPct(detail.recommendation.reliabilityPct)
    : null;
  const recommendationConfidence = detail.recommendation
    ? normalizeRecommendationPct(detail.recommendation.confidencePct)
    : null;
  const decisionScoreValue = detail.provenance?.decisionScore ?? null;

  return (
    <div className="space-y-5 text-sm">
      <section className="rounded-2xl border border-muted bg-surface-elevated p-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <div className="text-xs uppercase tracking-wide text-muted">Detalj analitike</div>
            <div className="mt-2 text-lg font-semibold text-contrast">{detail.title} <InfoTip text="Detaljan prikaz izabranog zapisa sa dodatnim poljima i metapodacima." /></div>
            {detail.subtitle ? <div className="mt-1 text-sm text-muted">{detail.subtitle}</div> : null}
            {usedSnapshot ? (
              <div className="mt-2 text-xs text-warning">
                Prikazan je sačuvani snimak reda jer detalj sa servera nije dostupan za ovu tabelu.
              </div>
            ) : null}
          </div>
          <button
            type="button"
            onClick={() => void copyValue(detail.recordId)}
            className="inline-flex items-center gap-1 rounded-lg border border-muted bg-surface-darker px-3 py-2 text-xs text-muted hover:text-contrast transition-colors"
            title="Kopiraj identifikator zapisa u clipboard"
          >
            <Copy size={13} />
            Kopiraj ID
          </button>
        </div>
      </section>

      {detail.recommendation ? (
        <section className="rounded-2xl border border-muted bg-surface-elevated p-4">
          <h3 className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">Preporuka i pouzdanost</h3>
          <DetailRow label="Preporuka" value={detail.recommendation.label} highlight={detail.recommendation.recommendationAllowed} />
          <DetailRow label="Obrazloženje" value={detail.recommendation.summary} />
          <DetailRow label="Preporuka dozvoljena" value={detail.recommendation.recommendationAllowed ? "Da" : "Ne"} />
          <DetailRow label="Pouzdanost" value={recommendationReliability == null ? "Nije dostupno" : `${recommendationReliability.toLocaleString("sr-RS")} %`} />
          <DetailRow label="Sigurnost preporuke" value={recommendationConfidence == null ? "Nije dostupno" : `${recommendationConfidence.toLocaleString("sr-RS")} %`} />
          <DetailRow label="Kvalitet podataka" value={formatDataQualityStatus(detail.recommendation.dataQualityStatus)} />
          {detail.recommendation.reasonCodes.length > 0 ? (
            <DetailRow label="Razlozi" value={detail.recommendation.reasonCodes.map(formatDetailReasonCode).join(", ")} />
          ) : null}
        </section>
      ) : null}

      {detail.provenance ? (
        <section className="rounded-2xl border border-muted bg-surface-elevated p-4">
          <h3 className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">Poreklo i kvalitet podataka</h3>
          <DetailRow label="Traženi period" value={`${detail.provenance.requestedFromUtc ?? "Nije navedeno"} – ${detail.provenance.requestedToUtc ?? "Nije navedeno"}`} />
          <DetailRow label="Efektivni period" value={`${detail.provenance.effectiveFromUtc ?? "Nije navedeno"} – ${detail.provenance.effectiveToUtc ?? "Nije navedeno"}`} />
          <DetailRow label="Sezona" value={detail.provenance.season} />
          <DetailRow label="Objekat" value={detail.provenance.storeId == null ? "Svi objekti" : String(detail.provenance.storeId)} />
          <DetailRow label="Opseg podataka" value={detail.provenance.dataScope === "imported" ? "Uvezeni podaci" : detail.provenance.dataScope === "existing" ? "Postojeći podaci" : "Svi podaci"} />
          <DetailRow label="Izvor podataka" value={detail.provenance.sourceLabel} />
          <DetailRow label="Izvorne tabele" value={detail.provenance.sourceTables} />
          <DetailRow label="Posmatrana populacija" value={detail.provenance.observedPopulation} />
          <DetailRow label="Politika troška" value={detail.provenance.costPolicy} />
          <DetailRow label="Politika pre/post kohorte" value={detail.provenance.prePostPolicy} />
          {detail.table === "color-sales-stats" ? (
            <>
              <DetailRow label="Skor odluke (0–100)" value={decisionScoreValue == null ? "Nije dostupno" : `${decisionScoreValue.toLocaleString("sr-RS")} %`} />
              <DetailRow label="Jedinica skora odluke" value={detail.provenance.decisionScoreUnit} />
              <DetailRow label="Imenilac skora odluke" value={detail.provenance.decisionScoreDenominator} />
              <DetailRow label="Akcionalnost skora odluke" value={detail.provenance.decisionScoreActionability === "actionable" ? "Dozvoljeno" : detail.provenance.decisionScoreActionability === "blocked" ? "Blokirano" : "Nije dostupno"} />
            </>
          ) : null}
          <DetailRow label="Svežina" value={detail.provenance.freshness === "fresh" ? "Sveže" : detail.provenance.freshness} />
          <DetailRow label="Snimljeni trošak" value={detail.provenance.snapshotActive ? "Aktivan" : "Nije aktivan"} />
          <DetailRow label="Procena troška" value={detail.provenance.fallbackApplied ? "Korišćena" : "Nije korišćena"} />
        </section>
      ) : null}

      <section className="rounded-2xl border border-muted bg-surface-elevated p-4">
        <h3 className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">Polja</h3>
        {detail.fields.map((field) => (
          <DetailRow key={field.key} label={field.label} value={field.value} highlight={field.highlight} />
        ))}
      </section>

      {detail.metadata.length > 0 ? (
        <section className="rounded-2xl border border-muted bg-surface-elevated p-4">
          <h3 className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">Metapodaci</h3>
          {detail.metadata.map((field) => (
            <DetailRow key={field.key} label={field.label} value={field.value} />
          ))}
        </section>
      ) : null}
    </div>
  );
}
