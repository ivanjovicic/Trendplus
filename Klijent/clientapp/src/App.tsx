import { BrowserRouter, Routes, Route } from "react-router-dom";
import { useBackendStatus } from "./context/useBackendStatus";

import HomePage from "./pages/HomePage";
import ArtikliPage from "./pages/ArtikliPage";
import ProdajaPage from "./pages/ProdajaPage";

export default function App() {
    const { online } = useBackendStatus();

    return (
        <BrowserRouter>
            {/* Global layout */}
            <div className="min-h-screen bg-gray-100 p-6">
                {/* Backend offline banner */}
                {!online && (
                    <div
                        role="alert"
                        aria-live="assertive"
                        style={{
                            position: "fixed",
                            top: 0,
                            left: 0,
                            width: "100%",
                            backgroundColor: "#b91c1c",
                            color: "#ffffff",
                            textAlign: "center",
                            padding: "16px 8px",
                            fontSize: "1.05rem",
                            fontWeight: 700,
                            boxShadow: "0 8px 20px rgba(0,0,0,0.35)",
                            borderBottom: "4px solid #7f1d1d",
                            zIndex: 9999,
                        }}
                    >
                        ⚠️ Servis trenutno nije dostupan
                    </div>
                )}

                {/* Page container */}
                <div className="max-w-4xl mx-auto mt-12">
                    <Routes>
                        <Route path="/" element={<HomePage />} />
                        <Route path="/artikli" element={<ArtikliPage />} />
                        <Route path="/prodaja" element={<ProdajaPage />} />
                        <Route path="*" element={<HomePage />} />
                    </Routes>
                </div>
            </div>
        </BrowserRouter>
    );
}