import { BrowserRouter, Routes, Route } from "react-router-dom";
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
import SezonaPage from "./pages/SezonaPage";
import TipObucePage from "./pages/TipObucePage";
import DobavljaciPage from "./pages/DobavljaciPage";
import { ErrorBoundary } from "./components/ErrorBoundary";
import { ToastProvider } from "./components/Toast";

function AppShell() {
    return (
        <Routes>
            <Route path="/" element={<HomePage />} />
            <Route path="/artikli" element={<ArtikliPage />} />
            <Route path="/artikli/lista" element={<ArtikliListPage />} />
            <Route path="/artikli/:id/edit" element={<ArtikalEditPage />} />
            <Route path="/unos-robe" element={<UnosRobePage />} />
            <Route path="/prodaja" element={<ProdajaPage />} />
            <Route path="/nivelacija" element={<NivelacijaCenaPage />} />
            <Route path="/logs" element={<LogsPage />} />
            <Route path="/performance" element={<PerformanceDashboard />} />
            <Route path="/outbox" element={<OutboxDashboard />} />
            <Route path="/outbox/messages" element={<OutboxMessagesPage />} />
            <Route path="/nivelacije" element={<NivelacijePage />} />
            <Route path="/sezone" element={<SezonaPage />} />
            <Route path="/tipovi-obuce" element={<TipObucePage />} />
            <Route path="/dobavljaci" element={<DobavljaciPage />} />
        </Routes>
    );
}

export default function App() {
    return (
        <ErrorBoundary>
            <ToastProvider>
                <BrowserRouter>
                    <AppLayout>
                        <AppShell />
                    </AppLayout>
                </BrowserRouter>
            </ToastProvider>
        </ErrorBoundary>
    );
}
