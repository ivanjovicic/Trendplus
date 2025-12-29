import { BrowserRouter, Routes, Route } from "react-router-dom";
import { useContext } from "react";
import HomePage from "./pages/HomePage";
import AppLayout from "./layout/AppLayout";
import ArtikliPage from "./pages/ArtikliPage";
import ArtikliListPage from "./pages/ArtikliListPage";
import ArtikalEditPage from "./pages/ArtikalEditPage";
import ProdajaPage from "./pages/ProdajaPage";
import LogsPage from "./pages/LogsPage";
<<<<<<< HEAD
import UnosRobePage from "./pages/UnosRobePage";
=======
import PerformanceDashboard from "./pages/PerformanceDashboard";
>>>>>>> d02157d9ed8b2a830e03a0a23003aa43726588f7
import { BackendStatusContext } from "./context/BackendStatusContext";

function AppShell() {
    const { online } = useContext(BackendStatusContext);

    return (
        <div style={{ minHeight: "100vh", background: "#f3f4f6" }}>
            {/* Global header sa backend statusom */}
            <header
                style={{
                    background: "#111827",
                    color: "white",
                    padding: "0.5rem 1rem",
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                }}
            >
                <span style={{ fontWeight: 600 }}>Trendplus</span>
                <span style={{ fontSize: 13 }}>
                    Backend:{" "}
                    <span style={{ color: online ? "#6ee7b7" : "#fecaca" }}>
                        {online ? "ONLINE" : "OFFLINE"}
                    </span>
                </span>
            </header>

            {/* Ovde ide routing sadržaj */}
            <main style={{ maxWidth: 1400, margin: "1.5rem auto", padding: "0 1rem", width: '100%' }}>
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
            </main>
        </div>
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