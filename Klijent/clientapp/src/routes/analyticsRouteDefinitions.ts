export type AnalyticsSmokeRouteDefinition = {
    path: string;
    label: string;
    isDurableReport: boolean;
    legacyAliases: string[];
};

export const CORE_ANALYTICS_ROUTE_DEFINITIONS: AnalyticsSmokeRouteDefinition[] = [
    {
        path: "/analytics",
        label: "Analytics dashboard",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/products",
        label: "Product decision center",
        isDurableReport: false,
        legacyAliases: ["/analytics/product-decision-center"],
    },
    {
        path: "/analytics/supplier",
        label: "Supplier consolidated",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/inventory",
        label: "Inventory analytics",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/data-quality",
        label: "Data quality",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/actions",
        label: "Action queue",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/supplier/report?fromDate=2026-06-01&toDate=2026-06-30",
        label: "Supplier decision durable report",
        isDurableReport: true,
        legacyAliases: [],
    },
    {
        path: "/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30",
        label: "Pilot intake durable report",
        isDurableReport: true,
        legacyAliases: ["/analytics/data-quality/pilot-intake-report"],
    },
];

export const CORE_ANALYTICS_SMOKE_ROUTES = CORE_ANALYTICS_ROUTE_DEFINITIONS.map((route) => route.path);

export const CORE_ANALYTICS_LEGACY_ALIASES = {
    productDecisionCenter: "/analytics/product-decision-center",
    pilotIntakeReport: "/analytics/data-quality/pilot-intake-report",
} as const;
