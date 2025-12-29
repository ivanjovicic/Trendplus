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

function AppShell() {
    return (
        <Routes>
            <Route path="/" element={<HomePage />} />
            <Route path="/artikli" element={<ArtikliPage />} />
            <Route path="/artikli/lista" element={<ArtikliListPage />} />
            <Route path="/artikli/:id/edit" element={<ArtikalEditPage />} />
            <Route path="/unos-robe" element={<UnosRobePage />} />
            <Route path="/prodaja" element={<ProdajaPage />} />
            <Route path="/logs" element={<LogsPage />} />
            <Route path="/performance" element={<PerformanceDashboard />} />
        </Routes>
    );
}

export default function App() {
    return (
        <BrowserRouter>
            <AppLayout>
                <AppShell />
            </AppLayout>
        </BrowserRouter>
    );
}