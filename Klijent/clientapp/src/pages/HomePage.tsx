import { useNavigate } from "react-router-dom";

export default function HomePage() {
    const navigate = useNavigate();

    return (
        <div
            style={{
                minHeight: "100vh",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                background: "#f8fafc",
            }}
        >
            <div
                style={{
                    display: "grid",
                    gridTemplateColumns: "repeat(2, minmax(280px, 1fr))",
                    gap: "2rem",
                    maxWidth: 900,
                    width: "100%",
                    padding: "2rem",
                }}
            >
                {/* Kreiranje artikla */}
                <button
                    onClick={() => navigate("/artikli")}
                    style={{
                        height: 220,
                        fontSize: "1.8rem",
                        fontWeight: 600,
                        borderRadius: 16,
                        border: "none",
                        cursor: "pointer",
                        background: "linear-gradient(135deg, #2563eb, #1d4ed8)",
                        color: "white",
                        boxShadow: "0 10px 25px rgba(0,0,0,0.15)",
                    }}
                >
                    ➕<br />
                    Kreiraj artikal
                </button>

                {/* Prodaja */}
                <button
                    onClick={() => navigate("/prodaja")}
                    style={{
                        height: 220,
                        fontSize: "1.8rem",
                        fontWeight: 600,
                        borderRadius: 16,
                        border: "none",
                        cursor: "pointer",
                        background: "linear-gradient(135deg, #16a34a, #15803d)",
                        color: "white",
                        boxShadow: "0 10px 25px rgba(0,0,0,0.15)",
                    }}
                >
                    🧾<br />
                    Prodaja
                </button>
            </div>
        </div>
    );
}
