export type AnalyticsReliabilityContractCoverageKind = "adapter" | "reviewed_exception";

export interface AnalyticsReliabilityContractCoverageEntry {
  surface: string;
  adapterName?: string;
  kind: AnalyticsReliabilityContractCoverageKind;
  reason?: string;
}

/**
 * Screen-level adoption inventory for the shared reliability contract suite.
 * Route-level runtime validation remains owned by RQ511; this manifest proves
 * that each Tier-1 surface has a shared invariant adapter as well.
 */
export const TIER1_ANALYTICS_RELIABILITY_CONTRACT_COVERAGE: readonly AnalyticsReliabilityContractCoverageEntry[] = [
  { surface: "dashboard", adapterName: "Dashboard", kind: "adapter" },
  { surface: "product-decision-center", adapterName: "Product Decision Center", kind: "adapter" },
  { surface: "decision-board", adapterName: "Decision Board", kind: "adapter" },
  { surface: "data-quality", adapterName: "Data Quality", kind: "adapter" },
  { surface: "reports", adapterName: "Durable Reports", kind: "adapter" },
  { surface: "inventory", adapterName: "Inventory", kind: "adapter" },
  { surface: "analytics-actions", adapterName: "Actions", kind: "adapter" },
  { surface: "supplier", adapterName: "Supplier", kind: "adapter" },
  { surface: "decision-pulse", adapterName: "Decision Pulse", kind: "adapter" },
  { surface: "shoe-type", adapterName: "Shoe Type", kind: "adapter" },
  { surface: "color", adapterName: "Color", kind: "adapter" },
  { surface: "daily", adapterName: "Daily Sales", kind: "adapter" },
  { surface: "pre-post", adapterName: "Pre/Post", kind: "adapter" },
];
