import { Suspense } from "react";
import { lazyWithChunkRecovery } from "./utils/chunkLoadRecovery";
import { BrowserRouter, Navigate, Route, Routes, useLocation, useNavigate, useParams, type Location } from "react-router-dom";
import AppLayout from "./layout/AppLayout";
import { ErrorBoundary } from "./components/ErrorBoundary";
import { ToastProvider } from "./components/Toast";
import { CircuitBreakerStatus } from "./components/CircuitBreakerStatus";
import Modal from "./components/Modal";
import { ThemeProvider } from "./context/ThemeContext";
import { CORE_ANALYTICS_LEGACY_ALIASES } from "./routes/analyticsRouteDefinitions";
import {
    SupplierDecisionHubRedirect,
    SupplierFootwearAnalyticsRedirect,
    SupplierSalesStatsRedirect,
} from "./pages/SupplierRedirects";

const HomePage = lazyWithChunkRecovery(() => import("./pages/HomePage"));
const ArtikliPage = lazyWithChunkRecovery(() => import("./pages/ArtikliPage"));
const ArtikliListPage = lazyWithChunkRecovery(() => import("./pages/ArtikliListPage"));
const ArtikalEditPage = lazyWithChunkRecovery(() => import("./pages/ArtikalEditPage"));
const ProdajaPage = lazyWithChunkRecovery(() => import("./pages/ProdajaPage"));
const LogsPage = lazyWithChunkRecovery(() => import("./pages/LogsPage"));
const UnosRobePage = lazyWithChunkRecovery(() => import("./pages/UnosRobePage"));
const PerformanceDashboard = lazyWithChunkRecovery(() => import("./pages/PerformanceDashboard"));
const OutboxDashboard = lazyWithChunkRecovery(() => import("./pages/OutboxDashboard"));
const OutboxMessagesPage = lazyWithChunkRecovery(() => import("./pages/OutboxMessagesPage"));
const NivelacijaCenaPage = lazyWithChunkRecovery(() => import("./pages/NivelacijaCenaPage"));
const NivelacijePage = lazyWithChunkRecovery(() => import("./pages/NivelacijePage"));
const NivelacijaRepairPage = lazyWithChunkRecovery(() => import("./pages/NivelacijaRepairPage"));
const DnevnikPromenaPage = lazyWithChunkRecovery(() => import("./pages/DnevnikPromenaPage"));
const SezonaPage = lazyWithChunkRecovery(() => import("./pages/SezonaPage"));
const TipObucePage = lazyWithChunkRecovery(() => import("./pages/TipObucePage"));
const DobavljaciPage = lazyWithChunkRecovery(() => import("./pages/DobavljaciPage"));
const PovracajPage = lazyWithChunkRecovery(() => import("./pages/PovracajPage"));
const AnalyticsDashboard = lazyWithChunkRecovery(() => import("./pages/AnalyticsDashboard"));
const AnalyticsDetails = lazyWithChunkRecovery(() => import("./pages/AnalyticsDetails"));
const ImageUploadTestPage = lazyWithChunkRecovery(() => import("./pages/ImageUploadTestPage"));
const GlobalTrendsPage = lazyWithChunkRecovery(() => import("./pages/GlobalTrendsPage"));
const ZalandoProducts = lazyWithChunkRecovery(() => import("./pages/ZalandoProducts"));
const ReleaseCalendar = lazyWithChunkRecovery(() => import("./pages/ReleaseCalendar"));
const DeichmannPage = lazyWithChunkRecovery(() => import("./pages/DeichmannPage"));
const CommonProductsPage = lazyWithChunkRecovery(() => import("./pages/CommonProductsPage"));
const AboutYouPage = lazyWithChunkRecovery(() => import("./pages/AboutYouPage"));
const HumanicPage = lazyWithChunkRecovery(() => import("./pages/HumanicPage"));
const ScraperHubPage = lazyWithChunkRecovery(() => import("./pages/ScraperHubPage"));
const TrendDashboardPage = lazyWithChunkRecovery(() => import("./pages/TrendDashboardPage"));
const AmazonShoesTrendsPage = lazyWithChunkRecovery(() => import("./pages/AmazonShoesTrendsPage"));
const EbayShoesTrendsPage = lazyWithChunkRecovery(() => import("./pages/EbayShoesTrendsPage"));
const GoogleShoppingTrendsPage = lazyWithChunkRecovery(() => import("./pages/GoogleShoppingTrendsPage"));
const OpenTrainingPage = lazyWithChunkRecovery(() => import("./pages/OpenTrainingPage"));
const RuntimeScoringPage = lazyWithChunkRecovery(() => import("./pages/RuntimeScoringPage"));
const AccessImportPage = lazyWithChunkRecovery(() => import("./pages/AccessImportPage"));
const TransferPage = lazyWithChunkRecovery(() => import("./pages/TransferPage"));
const ProdajaPrePostNivelacijePage = lazyWithChunkRecovery(() => import("./pages/ProdajaPrePostNivelacijePage"));
const InsightStudioPage = lazyWithChunkRecovery(() => import("./pages/InsightStudioPage"));
const PreNivelacijaPriorityPage = lazyWithChunkRecovery(() => import("./pages/PreNivelacijaPriorityPage"));
const ShoeTypeSalesStatsPage = lazyWithChunkRecovery(() => import("./pages/ShoeTypeSalesStatsPage"));
const DailySalesStatsPage = lazyWithChunkRecovery(() => import("./pages/DailySalesStatsPage"));
const ColorSalesStatsPage = lazyWithChunkRecovery(() => import("./pages/ColorSalesStatsPage"));
const InventoryPage = lazyWithChunkRecovery(() => import("./pages/InventoryPage"));
const UnosHubPage = lazyWithChunkRecovery(() => import("./pages/UnosHubPage"));
const DataQualityPage = lazyWithChunkRecovery(() => import("./pages/DataQualityPage"));
const AnalyticsDetailPage = lazyWithChunkRecovery(() => import("./pages/AnalyticsDetailPage"));
const AnalyticsPrintPage = lazyWithChunkRecovery(() => import("./pages/AnalyticsPrintPage"));
const ThemeSettingsPage = lazyWithChunkRecovery(() => import("./pages/ThemeSettingsPage"));
const ConfigurationPage = lazyWithChunkRecovery(() => import("./pages/ConfigurationPage"));
const ProductDecisionCenterPage = lazyWithChunkRecovery(() => import("./pages/ProductDecisionCenterPage"));
const SupplierConsolidatedPage = lazyWithChunkRecovery(() => import("./pages/SupplierConsolidatedPage"));
const AnalyticsActionsPage = lazyWithChunkRecovery(() => import("./pages/AnalyticsActionsPage"));
const DecisionPulsePage = lazyWithChunkRecovery(() => import("./pages/DecisionPulsePage"));
const ExecutiveDecisionBoardPage = lazyWithChunkRecovery(() => import("./pages/ExecutiveDecisionBoardPage"));
const SupplierDecisionReportPage = lazyWithChunkRecovery(() => import("./pages/SupplierDecisionReportPage"));
const PilotIntakeReportPage = lazyWithChunkRecovery(() => import("./pages/PilotIntakeReportPage"));
const PilotReadinessPage = lazyWithChunkRecovery(() => import("./pages/PilotReadinessPage"));

function RouteFallback() {
    return <div className="page-loading">Učitavanje...</div>;
}

function AppShell() {
    const location = useLocation();
    const routeState = location.state as { backgroundLocation?: Location } | undefined;
    const isAnalyticsDetailRoute = /^\/analitika\/[^/]+\/[^/]+$/.test(location.pathname);
    const backgroundLocation = isAnalyticsDetailRoute ? routeState?.backgroundLocation : undefined;

    return (
        <>
            <Suspense fallback={<RouteFallback />}>
                <Routes location={backgroundLocation ?? location}>
                    <Route path="/" element={<HomePage />} />
                    <Route path="/zalando" element={<ZalandoProducts />} />
                    <Route path="/unos" element={<UnosHubPage />} />
                    <Route path="/artikli" element={<ArtikliPage />} />
                    <Route path="/artikli/lista" element={<ArtikliListPage />} />
                    <Route path="/artikli/:id/edit" element={<ArtikalEditPage />} />
                    <Route path="/unos-robe" element={<UnosRobePage />} />
                    <Route path="/prodaja" element={<ProdajaPage />} />
                    <Route path="/nivelacija" element={<NivelacijaCenaPage />} />
                    <Route path="/logs" element={<LogsPage />} />
                    <Route path="/performance" element={<PerformanceDashboard />} />
                    <Route path="/analytics" element={<AnalyticsDashboard />} />
                    <Route path="/analytics/pilot-readiness" element={<PilotReadinessPage />} />
                    <Route path="/analytics/products" element={<ProductDecisionCenterPage />} />
                    <Route path={CORE_ANALYTICS_LEGACY_ALIASES.productDecisionCenter} element={<Navigate to="/analytics/products" replace />} />
                    <Route path="/analytics/supplier" element={<SupplierConsolidatedPage />} />
                    <Route path="/analytics/supplier-sales-stats" element={<SupplierSalesStatsRedirect />} />
                    <Route path="/analytics/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
                    <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
                    <Route path="/analytics/nivelacije-pre-post" element={<ProdajaPrePostNivelacijePage />} />
                    <Route path="/analytics/inventory" element={<InventoryPage />} />
                    <Route path="/analytics/color-sales-stats" element={<ColorSalesStatsPage />} />
                    <Route path="/analytics/data-quality" element={<DataQualityPage />} />
                    <Route path="/analytics/actions" element={<AnalyticsActionsPage />} />
                    <Route path="/analytics/decision-pulse" element={<DecisionPulsePage />} />
                    <Route path="/analytics/decision-board" element={<ExecutiveDecisionBoardPage />} />
                    <Route path="/analytics/supplier/report" element={<SupplierDecisionReportPage />} />
                    <Route path="/analytics/reports/pilot-intake" element={<PilotIntakeReportPage />} />
                    <Route path={CORE_ANALYTICS_LEGACY_ALIASES.pilotIntakeReport} element={<PilotIntakeReportPage />} />
                    <Route path="/analytics/insight-studio" element={<InsightStudioPage />} />
                    <Route path="/analytics/pre-nivelacija-prioriteti" element={<PreNivelacijaPriorityPage />} />
                    <Route path="/analytics/dobavljaci-tipovi-obuce" element={<SupplierFootwearAnalyticsRedirect />} />
                    <Route path="/analytics/supplier-decision-hub" element={<SupplierDecisionHubRedirect />} />
                    <Route path="/analytics-details" element={<AnalyticsDetails />} />
                    <Route path="/analitika/:table/:id" element={<AnalyticsDetailPage />} />
                    <Route path="/print/analytics/:table" element={<AnalyticsPrintPage />} />
                    <Route path="/outbox" element={<OutboxDashboard />} />
                    <Route path="/outbox/messages" element={<OutboxMessagesPage />} />
                    <Route path="/nivelacije" element={<NivelacijePage />} />
                    <Route path="/dnevnik-promena" element={<DnevnikPromenaPage />} />
                    <Route path="/dnevnik-promena/:id" element={<DnevnikPromenaPage />} />
                    <Route path="/sezone" element={<SezonaPage />} />
                    <Route path="/tipovi-obuce" element={<TipObucePage />} />
                    <Route path="/dobavljaci" element={<DobavljaciPage />} />
                    <Route path="/povracaj" element={<PovracajPage />} />
                    <Route path="/image-upload-test" element={<ImageUploadTestPage />} />
                    <Route path="/global-trends" element={<GlobalTrendsPage />} />
                    <Route path="/release-calendar" element={<ReleaseCalendar />} />
                    <Route path="/deichmann" element={<DeichmannPage />} />
                    <Route path="/aboutyou" element={<AboutYouPage />} />
                    <Route path="/humanic" element={<HumanicPage />} />
                    <Route path="/scraper-hub" element={<ScraperHubPage />} />
                    <Route path="/trend-dashboard" element={<TrendDashboardPage />} />
                    <Route path="/amazon-shoes" element={<AmazonShoesTrendsPage />} />
                    <Route path="/ebay-shoes" element={<EbayShoesTrendsPage />} />
                    <Route path="/google-shopping" element={<GoogleShoppingTrendsPage />} />
                    <Route path="/open-training" element={<OpenTrainingPage />} />
                    <Route path="/runtime-scoring" element={<RuntimeScoringPage />} />
                    <Route path="/access-import" element={<AccessImportPage />} />
                    <Route path="/transfers" element={<TransferPage />} />
                    <Route path="/admin/common-products" element={<CommonProductsPage />} />
                    <Route path="/admin/nivelacija-repair" element={<NivelacijaRepairPage />} />
                    <Route path="/admin/configuration" element={<ConfigurationPage />} />
                    <Route path="/configuration" element={<Navigate to="/admin/configuration" replace />} />
                    <Route path="/settings/themes" element={<ThemeSettingsPage />} />
                </Routes>

                {backgroundLocation ? (
                    <Routes>
                        <Route path="/analitika/:table/:id" element={<AnalyticsDetailModalRoute />} />
                    </Routes>
                ) : null}
            </Suspense>
        </>
    );
}

function AnalyticsDetailModalRoute() {
    const navigate = useNavigate();
    const params = useParams<{ table?: string; id?: string }>();

    return (
        <Modal
            isOpen={true}
            onClose={() => navigate(-1)}
            title={params.table ? `Detalj ${params.table}` : "Detalj analitike"}
            size="lg"
        >
            <AnalyticsDetailPage standalone={false} />
        </Modal>
    );
}

function AppRouterContent() {
    const location = useLocation();
    const isPrintRoute = location.pathname.startsWith("/print/analytics/");
    const shell = <AppShell />;

    if (isPrintRoute) {
        return shell;
    }

    return (
        <>
            <AppLayout>{shell}</AppLayout>
            <CircuitBreakerStatus />
        </>
    );
}

export default function App() {
    return (
        <ErrorBoundary>
            <ThemeProvider defaultTheme="neon-dark">
                <ToastProvider>
                    <BrowserRouter>
                        <AppRouterContent />
                    </BrowserRouter>
                </ToastProvider>
            </ThemeProvider>
        </ErrorBoundary>
    );
}
