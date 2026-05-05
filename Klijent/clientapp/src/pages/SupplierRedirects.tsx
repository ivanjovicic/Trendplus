import { Navigate, useSearchParams } from "react-router-dom";

type RedirectTab = "overview" | "scorecard" | "assortment";

function SupplierLegacyRedirect({ tab }: { tab: RedirectTab }) {
  const [searchParams] = useSearchParams();
  const nextParams = new URLSearchParams(searchParams);
  nextParams.set("tab", tab);

  return <Navigate to={`/analytics/supplier?${nextParams.toString()}`} replace />;
}

export function SupplierSalesStatsRedirect() {
  return <SupplierLegacyRedirect tab="overview" />;
}

export function SupplierDecisionHubRedirect() {
  return <SupplierLegacyRedirect tab="scorecard" />;
}

export function SupplierFootwearAnalyticsRedirect() {
  return <SupplierLegacyRedirect tab="assortment" />;
}
