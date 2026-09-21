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
        path: "/analytics/decision-board",
        label: "Izvršni board",
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
        path: "/analytics/shoe-type-sales-stats",
        label: "Prodaja po tipu obuće",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/daily-sales",
        label: "Dnevna prodaja",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/nivelacije-pre-post",
        label: "Pre/Post nivelacija",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/color-sales-stats",
        label: "Prodaja po boji",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/pre-nivelacija-prioriteti",
        label: "Prioriteti Pre-Nivelacije",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/supplier-sales-stats",
        label: "Dobavljači — prodaja (legacy)",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/dobavljaci-tipovi-obuce",
        label: "Dobavljači — tipovi obuće (legacy)",
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
