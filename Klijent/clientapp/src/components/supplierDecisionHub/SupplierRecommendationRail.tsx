import type { SummarySupplierItem } from "../../services/supplierDecisionHubApi";
import { dataQualityStatusLabel, formatReliability } from "../../utils/analyticsQuality";
import {
  confidenceLabel,
  formatCurrency,
  formatScore,
  getRecommendationMeta,
} from "./utils";

type SupplierRecommendationRailProps = {
  topGrowSuppliers: SummarySupplierItem[];
  topRiskSuppliers: SummarySupplierItem[];
  onSelectSupplier: (supplierId: number) => void;
};

type SupplierColumnProps = {
  title: string;
  emptyMessage: string;
  items: SummarySupplierItem[];
  onSelectSupplier: (supplierId: number) => void;
};

function SupplierColumn({
  title,
  emptyMessage,
  items,
  onSelectSupplier,
}: SupplierColumnProps) {
  return (
    <div className="supplier-decision-rail-column">
      <div className="supplier-decision-rail-title">{title}</div>
      {items.length === 0 ? (
        <div className="supplier-decision-empty">{emptyMessage}</div>
      ) : (
        <div className="supplier-decision-rail-list">
          {items.map((item) => {
            const recommendation = getRecommendationMeta(item.recommendationCode);
            return (
              <button
                key={item.supplierId}
                type="button"
                className={`supplier-decision-reco-card tone-${recommendation.ton}`}
                onClick={() => onSelectSupplier(item.supplierId)}
              >
                <div className="supplier-decision-reco-head">
                  <span className={`supplier-decision-pill tone-${recommendation.ton}`}>
                    {recommendation.label}
                  </span>
                  <span className="supplier-decision-reco-confidence">
                    {confidenceLabel(item.confidenceScore)}
                  </span>
                </div>
                <div className="supplier-decision-reco-grid">
                  <div>
                    <div className="supplier-decision-reco-label">Dobavljač</div>
                    <strong>{item.supplierName}</strong>
                  </div>
                  <div>
                    <div className="supplier-decision-reco-label">Ključna metrika</div>
                    <strong>{formatScore(item.supplierQualityIndex)}</strong>
                  </div>
                </div>
                <div className="supplier-decision-reco-copy">
                  <div className="supplier-decision-reco-label">Razlog preporuke</div>
                  <p>{recommendation.razlog}</p>
                </div>
                <div className="supplier-decision-reco-copy">
                  <div className="supplier-decision-reco-label">Trust signala</div>
                  <p>
                    Pouzdanost: {formatReliability(item.reliabilityPct, 0)} · Kvalitet:{" "}
                    {dataQualityStatusLabel(item.dataQualityStatus)}
                  </p>
                  <p>{item.statusReason || "Nema dodatnog obrazloženja."}</p>
                </div>
                <div className="supplier-decision-reco-foot">
                  <span>Prihod: {formatCurrency(item.revenue)}</span>
                  <span>Nivo pouzdanosti: {confidenceLabel(item.confidenceScore)}</span>
                </div>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}

export default function SupplierRecommendationRail({
  topGrowSuppliers,
  topRiskSuppliers,
  onSelectSupplier,
}: SupplierRecommendationRailProps) {
  return (
    <div className="supplier-decision-rail">
      <SupplierColumn
        title="Povećati saradnju"
        emptyMessage="Trenutno nema jasnog kandidata za širenje saradnje."
        items={topGrowSuppliers}
        onSelectSupplier={onSelectSupplier}
      />
      <SupplierColumn
        title="Rizik / smanjiti nabavku"
        emptyMessage="Trenutno nema dobavljača sa izraženim rizikom."
        items={topRiskSuppliers}
        onSelectSupplier={onSelectSupplier}
      />
    </div>
  );
}
