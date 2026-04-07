import { BrowserRouter, Route, Routes, useLocation, useNavigate, useParams, type Location } from "react-router-dom";
import HomePage from "./pages/HomePage";
import AppLayout from "./layout/AppLayout";
import ArtikliPage from "./pages/ArtikliPage";
import ArtikliListPage from "./pages/ArtikliListPage";
import ArtikalEditPage from "./pages/ArtikalEditPage";
import ProdajaPage from "./pages/ProdajaPage";
import LogsPage from "./pages/LogsPage";
import UnosRobePage from "./pages/UnosRobePage";
import PerformanceDashboard from "./pages/PerformanceDashboard";
import OutboxDashboard from "./pages/OutboxDashboard";
import OutboxMessagesPage from "./pages/OutboxMessagesPage";
import NivelacijaCenaPage from "./pages/NivelacijaCenaPage";
import NivelacijePage from "./pages/NivelacijePage";
import DnevnikPromenaPage from "./pages/DnevnikPromenaPage";
import SezonaPage from "./pages/SezonaPage";
import TipObucePage from "./pages/TipObucePage";
import DobavljaciPage from "./pages/DobavljaciPage";
import { ErrorBoundary } from "./components/ErrorBoundary";
import { ToastProvider } from "./components/Toast";
import { CircuitBreakerStatus } from "./components/CircuitBreakerStatus";
import PovracajPage from "./pages/PovracajPage";
import AnalyticsDashboard from "./pages/AnalyticsDashboard";
import AnalyticsDetails from "./pages/AnalyticsDetails";
import ImageUploadTestPage from "./pages/ImageUploadTestPage";
import GlobalTrendsPage from "./pages/GlobalTrendsPage";
import ZalandoProducts from "./pages/ZalandoProducts";
import ReleaseCalendar from "./pages/ReleaseCalendar";
import DeichmannPage from "./pages/DeichmannPage";
import CommonProductsPage from "./pages/CommonProductsPage";
import AboutYouPage from "./pages/AboutYouPage";
import HumanicPage from "./pages/HumanicPage";
import ScraperHubPage from "./pages/ScraperHubPage";
import TrendDashboardPage from "./pages/TrendDashboardPage";
import AmazonShoesTrendsPage from "./pages/AmazonShoesTrendsPage";
import EbayShoesTrendsPage from "./pages/EbayShoesTrendsPage";
import GoogleShoppingTrendsPage from "./pages/GoogleShoppingTrendsPage";
import OpenTrainingPage from "./pages/OpenTrainingPage";
import RuntimeScoringPage from "./pages/RuntimeScoringPage";
import AccessImportPage from "./pages/AccessImportPage";
import TransferPage from "./pages/TransferPage";
import ProdajaPrePostNivelacijePage from "./pages/ProdajaPrePostNivelacijePage";
import InsightStudioPage from "./pages/InsightStudioPage";
import PreNivelacijaPriorityPage from "./pages/PreNivelacijaPriorityPage";
import SupplierFootwearAnalyticsPage from "./pages/SupplierFootwearAnalyticsPage";
import SupplierDecisionHubPage from "./pages/SupplierDecisionHubPage";
import SupplierSalesStatsPage from "./pages/SupplierSalesStatsPage";
import ShoeTypeSalesStatsPage from "./pages/ShoeTypeSalesStatsPage";
import DailySalesStatsPage from "./pages/DailySalesStatsPage";
import ColorSalesStatsPage from "./pages/ColorSalesStatsPage";
import InventoryPage from "./pages/InventoryPage";
import UnosHubPage from "./pages/UnosHubPage";
import DataQualityPage from "./pages/DataQualityPage";
import Modal from "./components/Modal";
import AnalyticsDetailPage from "./pages/AnalyticsDetailPage";
import AnalyticsPrintPage from "./pages/AnalyticsPrintPage";
import ThemeSettingsPage from "./pages/ThemeSettingsPage";
import { ThemeProvider } from "./context/ThemeContext";

function AppShell() {
    const location = useLocation();
    const routeState = location.state as { backgroundLocation?: Location } | undefined;
    const backgroundLocation = routeState?.backgroundLocation;

    return (
        <>
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
                <Route path="/analytics/supplier-sales-stats" element={<SupplierSalesStatsPage />} />
                <Route path="/analytics/shoe-type-sales-stats" element={<ShoeTypeSalesStatsPage />} />
                <Route path="/analytics/daily-sales" element={<DailySalesStatsPage />} />
                <Route path="/analytics/color-sales-stats" element={<ColorSalesStatsPage />} />
                <Route path="/analytics/data-quality" element={<DataQualityPage />} />
                <Route path="/analytics/nivelacije-pre-post" element={<ProdajaPrePostNivelacijePage />} />
                <Route path="/analytics/insight-studio" element={<InsightStudioPage />} />
                <Route path="/analytics/pre-nivelacija-prioriteti" element={<PreNivelacijaPriorityPage />} />
                <Route path="/analytics/dobavljaci-tipovi-obuce" element={<SupplierFootwearAnalyticsPage />} />
                <Route path="/analytics/supplier-decision-hub" element={<SupplierDecisionHubPage />} />
                <Route path="/analytics/inventory" element={<InventoryPage />} />
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
                <Route path="/settings/themes" element={<ThemeSettingsPage />} />
                <Route path="/settings/themes" element={<ThemeSettingsPage />} />
            </Routes>

            {backgroundLocation ? (
                <Routes>
                    <Route path="/analitika/:table/:id" element={<AnalyticsDetailModalRoute />} />
                </Routes>
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
            <ThemeProvider defaultTheme="inventory-dark">
                <ToastProvider>
                    <BrowserRouter>
                        <AppRouterContent />
                    </BrowserRouter>
                </ToastProvider>
            </ThemeProvider>
        </ErrorBoundary>
    );
}
