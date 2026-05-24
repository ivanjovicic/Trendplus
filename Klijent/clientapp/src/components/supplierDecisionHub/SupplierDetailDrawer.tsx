import type {
  ArticleDecisionItem,
  SupplierDecisionDetailsResponse,
} from "../../services/supplierDecisionHubApi";
import {
  confidenceLabel,
  formatCurrency,
  formatDate,
  formatDateRange,
  formatInteger,
  formatRatioPercent,
  formatScore,
  getRecommendationMeta,
  signalQualityLabel,
  signalQualityReasonLabel,
} from "./utils";

type SupplierDetailDrawerProps = {
  open: boolean;
  loading?: boolean;
  error?: string | null;
  details: SupplierDecisionDetailsResponse | null;
  onClose: () => void;
};

type ArticleSectionProps = {
  title: string;
  emptyMessage: string;
  items: ArticleDecisionItem[];
};

function formatFeatureName(value: string) {
  const trimmed = value.trim();
  if (!trimmed) return "";
  return trimmed.replace(/_/g, " ");
}

function ArticleSection({ title, emptyMessage, items }: ArticleSectionProps) {
  return (
    <section className="supplier-decision-drawer-section">
      <h3>{title}</h3>
      {items.length === 0 ? (
        <div className="supplier-decision-empty">{emptyMessage}</div>
      ) : (
        <div className="supplier-decision-article-list">
          {items.map((item) => (
            <article key={`${title}-${item.articleId}`} className="supplier-decision-article-card">
              <div className="supplier-decision-article-head">
                <div>
                  <strong>{item.articleName}</strong>
                  <div className="supplier-decision-muted">
                    {item.sku} · {item.category}
                  </div>
                </div>
                <span className="supplier-decision-pill neutral">
                  {signalQualityLabel(item.signalQualityFlag)}
                </span>
              </div>
              <div className="supplier-decision-article-grid">
                <div>
                  <span>Pre prihod 30d</span>
                  <strong>{formatCurrency(item.preRevenue30d)}</strong>
                </div>
                <div>
                  <span>Post prihod 30d</span>
                  <strong>{formatCurrency(item.postRevenue30d)}</strong>
                </div>
                <div>
                  <span>Sell-through pre sniženja</span>
                  <strong>{formatRatioPercent(item.preSellthrough30d)}</strong>
                </div>
                <div>
                  <span>Marža pre sniženja</span>
                  <strong>{formatRatioPercent(item.preMargin30d)}</strong>
                </div>
                <div>
                  <span>Udeo prodaje na sniženju</span>
                  <strong>{formatRatioPercent(item.markdownRevenueShare)}</strong>
                </div>
                <div>
                  <span>Zaliha pre sniženja</span>
                  <strong>{formatInteger(item.stockBeforeMarkdown)}</strong>
                </div>
              </div>
              <div className="supplier-decision-article-foot">
                <span>Prvo sniženje: {formatDate(item.firstMarkdownDate)}</span>
                <span>Bez zaliha pre sniženja: {item.stockoutBeforeMarkdownFlag ? "Da" : "Ne"}</span>
              </div>
              {item.signalQualityReason ? (
                <p className="supplier-decision-article-reason">
                  {signalQualityReasonLabel(item.signalQualityReason)}
                </p>
              ) : null}
            </article>
          ))}
        </div>
      )}
    </section>
  );
}

export default function SupplierDetailDrawer({
  open,
  loading = false,
  error,
  details,
  onClose,
}: SupplierDetailDrawerProps) {
  if (!open) return null;

  const recommendation = details
    ? getRecommendationMeta(details.supplierHeader.recommendationCode)
    : getRecommendationMeta("HOLD");

  return (
    <div className="supplier-decision-drawer-backdrop" onClick={onClose}>
      <aside
        className="supplier-decision-drawer"
        onClick={(event) => event.stopPropagation()}
        aria-label="Detalji dobavljača"
      >
        <div className="supplier-decision-drawer-head">
          <div>
            <div className="supplier-decision-overline">Pregled dobavljača</div>
            <h2>{details?.supplierHeader.supplierName ?? "Dobavljač"}</h2>
            <p>
              {details
                ? formatDateRange(
                    details.supplierHeader.periodFrom,
                    details.supplierHeader.periodTo
                  )
                : "Učitavanje detalja..."}
            </p>
          </div>
          <button type="button" onClick={onClose} aria-label="Zatvori detalje">
            Zatvori
          </button>
        </div>

        {error ? <div className="supplier-decision-error">{error}</div> : null}

        {loading && !details ? (
          <div className="supplier-decision-empty">Učitavanje detalja dobavljača...</div>
        ) : details ? (
          <div className="supplier-decision-drawer-body">
            <section className="supplier-decision-drawer-section">
              <div className="supplier-decision-detail-hero">
                <div className={`supplier-decision-pill tone-${recommendation.ton}`}>
                  {recommendation.label}
                </div>
                <div className="supplier-decision-detail-stats">
                  <span>AI procena dobavljača: {formatScore(details.supplierHeader.mlSupplierScore)}</span>
                  <span>Indeks kvaliteta: {formatScore(details.supplierHeader.supplierQualityIndex)}</span>
                  <span>Pouzdanost: {confidenceLabel(details.supplierHeader.confidenceScore)}</span>
                </div>
              </div>
              {details.supplierHeader.aiExplanation ? (
                <p className="supplier-decision-muted">
                  AI signal: {details.supplierHeader.aiExplanation}
                </p>
              ) : null}
              {[details.supplierHeader.topFeature1, details.supplierHeader.topFeature2, details.supplierHeader.topFeature3].some(Boolean) ? (
                <div className="supplier-decision-detail-stats">
                  <span>AI signal 1: {formatFeatureName(details.supplierHeader.topFeature1)}</span>
                  <span>AI signal 2: {formatFeatureName(details.supplierHeader.topFeature2)}</span>
                  <span>AI signal 3: {formatFeatureName(details.supplierHeader.topFeature3)}</span>
                </div>
              ) : null}
              <div className="supplier-decision-detail-kpis">
                <article>
                  <span>Prihod</span>
                  <strong>{formatCurrency(details.kpis.revenue)}</strong>
                </article>
                <article>
                  <span>Komadi</span>
                  <strong>{formatInteger(details.kpis.units)}</strong>
                </article>
                <article>
                  <span>Udeo bez sniženja</span>
                  <strong>{formatRatioPercent(details.kpis.fullPriceRevenueShare)}</strong>
                </article>
                <article>
                  <span>Sell-through bez sniženja</span>
                  <strong>{formatRatioPercent(details.kpis.fullPriceSellthrough)}</strong>
                </article>
                <article>
                  <span>Marža pre sniženja</span>
                  <strong>{formatRatioPercent(details.kpis.preMarkdownMarginPct)}</strong>
                </article>
                <article>
                  <span>Kapital u riziku</span>
                  <strong>{formatCurrency(details.kpis.capitalAtRisk)}</strong>
                </article>
              </div>
            </section>

            <section className="supplier-decision-drawer-section">
              <h3>Rezultati po kategorijama</h3>
              <div className="supplier-decision-table-wrap">
                <table className="supplier-decision-table compact">
                  <thead>
                    <tr>
                      <th>Kategorija</th>
                      <th>Prihod</th>
                      <th>Komadi</th>
                      <th>Udeo bez sniženja</th>
                      <th>Sell-through pre sniženja</th>
                      <th>Udeo sniženja</th>
                      <th>Dead stock</th>
                      <th>Pobednički artikli</th>
                    </tr>
                  </thead>
                  <tbody>
                    {details.categoryBreakdown.length === 0 ? (
                      <tr>
                        <td colSpan={8}>
                          <div className="supplier-decision-empty">Nema podataka po kategorijama.</div>
                        </td>
                      </tr>
                    ) : (
                      details.categoryBreakdown.map((item) => (
                        <tr key={item.category}>
                          <td>{item.category}</td>
                          <td>{formatCurrency(item.revenue)}</td>
                          <td>{formatInteger(item.units)}</td>
                          <td>{formatRatioPercent(item.fullPriceRevenueShare)}</td>
                          <td>{formatRatioPercent(item.fullPriceSellthrough)}</td>
                          <td>{formatRatioPercent(item.markdownRevenueShare)}</td>
                          <td>{formatRatioPercent(item.deadStockRate)}</td>
                          <td>{formatRatioPercent(item.repeatWinnerRate)}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </section>

            <ArticleSection
              title="Najprodavaniji artikli bez sniženja"
              emptyMessage="Nema artikala koji trenutno odskaču po prodaji pre sniženja."
              items={details.winningArticles}
            />
            <ArticleSection
              title="Artikli koji zavise od sniženja"
              emptyMessage="Nema artikala sa izraženom zavisnošću od sniženja."
              items={details.markdownDependentArticles}
            />
            <ArticleSection
              title="Artikli koji su delovali loše zbog nedostatka zaliha"
              emptyMessage="Nema artikala kod kojih je nedostatak zaliha jasno iskrivio rezultat."
              items={details.blockedByOosArticles}
            />

            <section className="supplier-decision-drawer-section">
              <h3>Istorija preporuka</h3>
              {details.recommendationHistory.length === 0 ? (
                <div className="supplier-decision-empty">Nema istorije preporuka za izabrani period.</div>
              ) : (
                <div className="supplier-decision-history-list">
                  {details.recommendationHistory.map((item) => {
                    const itemRecommendation = getRecommendationMeta(item.recommendationCode);
                    return (
                      <article key={item.periodStart} className="supplier-decision-history-card">
                        <div className="supplier-decision-history-head">
                          <strong>{formatDate(item.periodStart)}</strong>
                          <span className={`supplier-decision-pill tone-${itemRecommendation.ton}`}>
                            {itemRecommendation.label}
                          </span>
                        </div>
                        <p>{itemRecommendation.razlog}</p>
                        <div className="supplier-decision-history-grid">
                          <span>Prihod: {formatCurrency(item.revenue)}</span>
                          <span>Udeo bez sniženja: {formatRatioPercent(item.fullPriceRevenueShare)}</span>
                          <span>Udeo sniženja: {formatRatioPercent(item.markdownRevenueShare)}</span>
                          <span>Marža pre sniženja: {formatRatioPercent(item.preMarkdownMarginPct)}</span>
                        </div>
                      </article>
                    );
                  })}
                </div>
              )}
            </section>
          </div>
        ) : null}
      </aside>
    </div>
  );
}
