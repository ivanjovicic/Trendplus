export type AnalyticsSmokeRouteDefinition = {
    path: string;
    label: string;
    isDurableReport: boolean;
    legacyAliases: string[];
};

export const CORE_ANALYTICS_ROUTE_DEFINITIONS: AnalyticsSmokeRouteDefinition[] = [
    {
        path: "/analytics",
        label: "Pregled poslovanja",
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
        label: "Izvršni board odluka",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/decision-pulse",
        label: "Puls odluka",
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
        label: "Prodaja po dobavljačima",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/inventory",
        label: "Analitika zaliha",
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
        label: "Prodaja po smenama",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/nivelacije-pre-post",
        label: "Pre/Posle nivelacije",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/color-sales-stats",
        label: "Prodaja po boji artikla",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/pre-nivelacija-prioriteti",
        label: "Prioriteti nivelacije",
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
        label: "Pregled zdravlja podataka",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/actions",
        label: "Akcije i preporuke",
        isDurableReport: false,
        legacyAliases: [],
    },
    {
        path: "/analytics/supplier/report?fromDate=2026-06-01&toDate=2026-06-30&scope=all",
        label: "Trendplus izveštaj dobavljača",
        isDurableReport: true,
        legacyAliases: [],
    },
    {
        path: "/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&scope=all",
        label: "Pilot izveštaj kvaliteta podataka",
        isDurableReport: true,
        legacyAliases: ["/analytics/data-quality/pilot-intake-report"],
    },
];

export const CORE_ANALYTICS_SMOKE_ROUTES = CORE_ANALYTICS_ROUTE_DEFINITIONS.map((route) => route.path);

export const CORE_ANALYTICS_LEGACY_ALIASES = {
    productDecisionCenter: "/analytics/product-decision-center",
    pilotIntakeReport: "/analytics/data-quality/pilot-intake-report",
} as const;
