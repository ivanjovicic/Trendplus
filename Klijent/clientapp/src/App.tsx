import { lazy, Suspense } from "react";
import { BrowserRouter, Route, Routes, useLocation, useNavigate, useParams, type Location } from "react-router-dom";
import AppLayout from "./layout/AppLayout";
import { ErrorBoundary } from "./components/ErrorBoundary";
import { ToastProvider } from "./components/Toast";
import { CircuitBreakerStatus } from "./components/CircuitBreakerStatus";
import Modal from "./components/Modal";
import { ThemeProvider } from "./context/ThemeContext";
import {
  SupplierSalesStatsRedirect,
  SupplierDecisionHubRedirect,
  SupplierFootwearAnalyticsRedirect,
} from "./pages/SupplierRedirects";

const HomePage = lazy(() => import("./pages/HomePage"));
const ArtikliPage = lazy(() => import("./pages/ArtikliPage"));
const ArtikliListPage = lazy(() => import("./pages/ArtikliListPage"));
const ArtikalEditPage = lazy(() => import("./pages/ArtikalEditPage"));
const ProdajaPage = lazy(() => import("./pages/ProdajaPage"));
const LogsPage = lazy(() => import("./pages/LogsPage"));
const UnosRobePage = lazy(() => import("./pages/UnosRobePage"));
const PerformanceDashboard = lazy(() => import("./pages/PerformanceDashboard"));
const OutboxDashboard = lazy(() => import("./pages/OutboxDashboard"));
const OutboxMessagesPage = lazy(() => import("./pages/OutboxMessagesPage"));
const NivelacijaCenaPage = lazy(() => import("./pages/NivelacijaCenaPage"));
const NivelacijePage = lazy(() => import("./pages/NivelacijePage"));
const NivelacijaRepairPage = lazy(() => import("./pages/NivelacijaRepairPage"));
const DnevnikPromenaPage = lazy(() => import("./pages/DnevnikPromenaPage"));
const SezonaPage = lazy(() => import("./pages/SezonaPage"));
const TipObucePage = lazy(() => import("./pages/TipObucePage"));
const DobavljaciPage = lazy(() => import("./pages/DobavljaciPage"));
const PovracajPage = lazy(() => import("./pages/PovracajPage"));
const AnalyticsDashboard = lazy(() => import("./pages/AnalyticsDashboard"));
const AnalyticsDetails = lazy(() => import("./pages/AnalyticsDetails"));
const ImageUploadTestPage = lazy(() => import("./pages/ImageUploadTestPage"));
const GlobalTrendsPage = lazy(() => import("./pages/GlobalTrendsPage"));
const ZalandoProducts = lazy(() => import("./pages/ZalandoProducts"));
const ReleaseCalendar = lazy(() => import("./pages/ReleaseCalendar"));
const DeichmannPage = lazy(() => import("./pages/DeichmannPage"));
const CommonProductsPage = lazy(() => import("./pages/CommonProductsPage"));
const AboutYouPage = lazy(() => import("./pages/AboutYouPage"));
const HumanicPage = lazy(() => import("./pages/HumanicPage"));
const ScraperHubPage = lazy(() => import("./pages/ScraperHubPage"));
const TrendDashboardPage = lazy(() => import("./pages/TrendDashboardPage"));
const AmazonShoesTrendsPage = lazy(() => import("./pages/AmazonShoesTrendsPage"));
const EbayShoesTrendsPage = lazy(() => import("./pages/EbayShoesTrendsPage"));
const GoogleShoppingTrendsPage = lazy(() => import("./pages/GoogleShoppingTrendsPage"));
const OpenTrainingPage = lazy(() => import("./pages/OpenTrainingPage"));
const RuntimeScoringPage = lazy(() => import("./pages/RuntimeScoringPage"));
const AccessImportPage = lazy(() => import("./pages/AccessImportPage"));
const TransferPage = lazy(() => import("./pages/TransferPage"));
const ProdajaPrePostNivelacijePage = lazy(() => import("./pages/ProdajaPrePostNivelacijePage"));
const InsightStudioPage = lazy(() => import("./pages/InsightStudioPage"));
const PreNivelacijaPriorityPage = lazy(() => import("./pages/PreNivelacijaPriorityPage"));
const SupplierConsolidatedPage = lazy(() => import("./pages/SupplierConsolidatedPage"));
const ShoeTypeSalesStatsPage = lazy(() => import("./pages/ShoeTypeSalesStatsPage"));
const DailySalesStatsPage = lazy(() => import("./pages/DailySalesStatsPage"));
const ColorSalesStatsPage = lazy(() => import("./pages/ColorSalesStatsPage"));
const InventoryPage = lazy(() => import("./pages/InventoryPage"));
const UnosHubPage = lazy(() => import("./pages/UnosHubPage"));
const DataQualityPage = lazy(() => import("./pages/DataQualityPage"));
const AnalyticsDetailPage = lazy(() => import("./pages/AnalyticsDetailPage"));
const AnalyticsPrintPage = lazy(() => import("./pages/AnalyticsPrintPage"));
const ThemeSettingsPage = lazy(() => import("./pages/ThemeSettingsPage"));

function RouteFallback() {
    return <div className="page-loading">Učitavanje...</div>;
}

function AppShell() {
    const location = useLocation();
    const routeState = location.state as { backgroundLocation?: Location } | undefined;
    const backgroundLocation = routeState?.backgroundLocation;

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
                    <Route path="/analytics/supplier" element={<SupplierConsolidatedPage />} />
                    <Route path="/analytics/supplier-sales-stats" element={<SupplierSalesStatsRedirect />} />
                    <Route path="/analytics/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
                    <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
                    <Route path="/analytics/nivelacije-pre-post" element={<ProdajaPrePostNivelacijePage />} />
                    <Route path="/analytics/inventory" element={<InventoryPage />} />
                    <Route path="/analytics/color-sales-stats" element={<ColorSalesStatsPage />} />
                    <Route path="/analytics/data-quality" element={<DataQualityPage />} />
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
                    <Route path="/settings/themes" element={<ThemeSettingsPage />} />
                    <Route path="/settings/themes" element={<ThemeSettingsPage />} />
                </Routes>
            </Suspense>

            {backgroundLocation ? (
                <Suspense fallback={<RouteFallback />}>
                    <Routes>
                        <Route path="/analitika/:table/:id" element={<AnalyticsDetailModalRoute />} />
                    </Routes>
                </Suspense>
            ) : null}
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
