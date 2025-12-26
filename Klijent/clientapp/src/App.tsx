import { BrowserRouter, Routes, Route } from "react-router-dom";
import AppLayout from "./layout/AppLayout";
import HomePage from "./pages/HomePage";
import ArtikliPage from "./pages/ArtikliPage";
import ArtikliListPage from "./pages/ArtikliListPage";
import ArtikalEditPage from "./pages/ArtikalEditPage";
import ProdajaPage from "./pages/ProdajaPage";

export default function App() {
    return (
        <BrowserRouter>
            <AppLayout>
                <Routes>
                    <Route path="/" element={<HomePage />} />
                    <Route path="/artikli" element={<ArtikliPage />} />
                    <Route path="/artikli/lista" element={<ArtikliListPage />} />
                    <Route path="/artikli/:id/edit" element={<ArtikalEditPage />} />
                    <Route path="/prodaja" element={<ProdajaPage />} />
                </Routes>
            </AppLayout>
        </BrowserRouter>
    );
}