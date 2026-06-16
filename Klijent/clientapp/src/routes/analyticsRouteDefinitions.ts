export type AnalyticsSmokeRouteDefinition = {
    path: string;
    label: string;
    isDurableReport: boolean;
    legacyAliases: string[];
};

export const CORE_ANALYTICS_ROUTE_DEFINITIONS: AnalyticsSmokeRouteDefinition[] = [
    {
        path: "/analytics",
        label: "Trendplus pregled",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/pilot-readiness",
        label: "Pilot spremnost",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/products",
        label: "Odluke o proizvodima",
        isDurableReport: false,
        legacyAliases: ["/analytics/product-decision-center"],
    },
    {
        path: "/analytics/supplier",
        label: "Pregled dobavljača",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/inventory",
        label: "Zalihe i dopuna",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/data-quality",
        label: "Kvalitet podataka",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/actions",
        label: "Centralne akcije",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/supplier/report?fromDate=2026-06-01&toDate=2026-06-30&scope=all",
        label: "Izveštaj dobavljača",
        isDurableReport: true,
        legacyAliases: [],
    },
    {
        path: "/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&scope=all",
        label: "Pilot intake izveštaj",
        isDurableReport: true,
        legacyAliases: ["/analytics/data-quality/pilot-intake-report"],
    },
];

export const CORE_ANALYTICS_SMOKE_ROUTES = CORE_ANALYTICS_ROUTE_DEFINITIONS.map((route) => route.path);

export const CORE_ANALYTICS_LEGACY_ALIASES = {
    productDecisionCenter: "/analytics/product-decision-center",
    pilotIntakeReport: "/analytics/data-quality/pilot-intake-report",
} as const;
