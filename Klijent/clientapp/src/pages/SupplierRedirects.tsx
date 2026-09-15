import { Navigate, useSearchParams } from "react-router-dom";

type RedirectTab = "overview" | "scorecard" | "assortment";
type LegacySource = "operations-supplier-sales" | "operations-supplier-footwear";

function SupplierLegacyRedirect({ tab, source }: { tab: RedirectTab; source?: LegacySource }) {
  const [searchParams] = useSearchParams();
  const nextParams = new URLSearchParams(searchParams);
  nextParams.set("tab", tab);
  if (source) nextParams.set("legacySource", source);

  return <Navigate to={`/analytics/supplier?${nextParams.toString()}`} replace />;
}

export function SupplierSalesStatsRedirect() {
  return <SupplierLegacyRedirect tab="overview" source="operations-supplier-sales" />;
}

export function SupplierDecisionHubRedirect() {
  return <SupplierLegacyRedirect tab="scorecard" />;
}

export function SupplierFootwearAnalyticsRedirect() {
  return <SupplierLegacyRedirect tab="assortment" source="operations-supplier-footwear" />;
}
