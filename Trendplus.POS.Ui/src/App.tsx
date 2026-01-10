import { useState } from "react";
import PosPage from "./pages/PosPage.tsx";
import SalesHistoryPage from "./pages/SalesHistoryPage.tsx";

type Page = "pos" | "history";

export default function App() {
    const [currentPage, setCurrentPage] = useState<Page>("pos");

    return (
        <div>
            {/* Navigation Header */}
            <nav style={{
                background: "#111827",
                padding: "12px 20px",
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center"
            }}>
                <div style={{ 
                    color: "white", 
                    fontWeight: 700, 
                    fontSize: 20 
                }}>
                    🛒 Trendplus POS
                </div>
                
                <div style={{ display: "flex", gap: 12 }}>
                    <button
                        onClick={() => setCurrentPage("pos")}
                        style={{
                            padding: "10px 20px",
                            fontSize: 16,
                            fontWeight: 600,
                            background: currentPage === "pos" ? "#2563eb" : "transparent",
                            color: "white",
                            border: currentPage === "pos" ? "none" : "2px solid #4b5563",
                            borderRadius: 8,
                            cursor: "pointer"
                        }}
                    >
                        💳 Prodaja
                    </button>
                    
                    <button
                        onClick={() => setCurrentPage("history")}
                        style={{
                            padding: "10px 20px",
                            fontSize: 16,
                            fontWeight: 600,
                            background: currentPage === "history" ? "#2563eb" : "transparent",
                            color: "white",
                            border: currentPage === "history" ? "none" : "2px solid #4b5563",
                            borderRadius: 8,
                            cursor: "pointer"
                        }}
                    >
                        📊 Istorija
                    </button>
                </div>
            </nav>

            {/* Page Content */}
            {currentPage === "pos" && <PosPage />}
            {currentPage === "history" && <SalesHistoryPage />}
        </div>
    );
}
