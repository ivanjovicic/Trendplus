import { createSale } from "../api/posApi";
import { useEffect, useState } from "react";
import { getProducts, type PosProduct } from "../api/posApi";

type CartItem = {
    productId: number;
    name: string;
    price: number;
    qty: number;
};


export default function PosPage() {
    const [products, setProducts] = useState<PosProduct[]>([]);
    const [cart, setCart] = useState<CartItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        let isMounted = true;
        
        setLoading(true);
        getProducts()
            .then((products) => {
                if (isMounted) {
                    setProducts(products);
                }
            })
            .catch((err) => {
                if (isMounted) {
                    console.error(err);
                    setError("Greška pri učitavanju artikala. Proverite da li backend radi.");
                }
            })
            .finally(() => {
                if (isMounted) {
                    setLoading(false);
                }
            });

        return () => {
            isMounted = false;
        };
    }, []);

    function addToCart(p: PosProduct) {
        setCart(c =>
            c.some(i => i.productId === p.id)
                ? c.map(i =>
                    i.productId === p.id
                        ? { ...i, qty: i.qty + 1 }
                        : i
                )
                : [...c, { productId: p.id, name: p.name, price: p.price, qty: 1 }]
        );
    }

    function removeFromCart(productId: number) {
        setCart(c => c.filter(i => i.productId !== productId));
    }

    function updateQty(productId: number, delta: number) {
        setCart(c => c.map(i => {
            if (i.productId === productId) {
                const newQty = i.qty + delta;
                return newQty > 0 ? { ...i, qty: newQty } : i;
            }
            return i;
        }));
    }

    async function pay() {
        try {
            await createSale(
                cart.map(i => ({
                    productId: i.productId,
                    qty: i.qty
                }))
            );

            alert("Prodaja uspešna ✔");
            setCart([]);
        } catch (err) {
            console.error(err);
            alert(`Greška pri prodaji: ${(err as Error).message}`);
        }
    }

    const total = cart.reduce(
        (sum, item) => sum + item.price * item.qty,
        0
    );

    if (loading) {
        return (
            <div style={{ display: "flex", justifyContent: "center", alignItems: "center", height: "100vh" }}>
                <h2>Učitavanje artikala...</h2>
            </div>
        );
    }

    if (error) {
        return (
            <div style={{ display: "flex", justifyContent: "center", alignItems: "center", height: "100vh", flexDirection: "column", gap: 20 }}>
                <h2 style={{ color: "red" }}>⚠️ {error}</h2>
                <button onClick={() => window.location.reload()} style={{ padding: "12px 24px", fontSize: 18 }}>
                    🔄 Pokušaj ponovo
                </button>
            </div>
        );
    }

    return (
        <div style={{ 
            display: "flex", 
            height: "100vh",
            flexDirection: window.innerWidth < 768 ? "column" : "row"
        }}>
            {/* Artikli */}
            <div style={{ 
                flex: window.innerWidth < 768 ? "1" : "2", 
                padding: 20, 
                overflowY: "auto",
                maxHeight: window.innerWidth < 768 ? "60vh" : "100vh"
            }}>
                <h2>Artikli ({products.length})</h2>
                <div style={{ 
                    display: "grid", 
                    gridTemplateColumns: window.innerWidth < 480 
                        ? "1fr" 
                        : window.innerWidth < 768 
                            ? "repeat(2, 1fr)" 
                            : "repeat(auto-fill, minmax(180px, 1fr))", 
                    gap: 12 
                }}>
                    {products.map(p => (
                        <button
                            key={p.id}
                            onClick={() => addToCart(p)}
                            disabled={p.kolicina === 0}
                            style={{
                                padding: 16,
                                fontSize: 14,
                                background: p.kolicina === 0 ? "#ccc" : "#3b82f6",
                                color: "white",
                                border: "none",
                                borderRadius: 8,
                                cursor: p.kolicina === 0 ? "not-allowed" : "pointer",
                                display: "flex",
                                flexDirection: "column",
                                gap: 6,
                                minHeight: 100
                            }}
                        >
                            <div style={{ fontWeight: 600, fontSize: 16 }}>{p.name}</div>
                            <div style={{ fontSize: 18, color: "#fbbf24" }}>{p.price.toFixed(2)} RSD</div>
                            <div style={{ fontSize: 12, opacity: 0.8 }}>
                                {p.kolicina !== undefined ? `Stanje: ${p.kolicina}` : ""}
                            </div>
                        </button>
                    ))}
                </div>
            </div>

            {/* Korpa */}
            <div style={{ 
                flex: "1", 
                minWidth: window.innerWidth < 768 ? "100%" : "320px",
                maxWidth: window.innerWidth < 768 ? "100%" : "500px",
                padding: 20, 
                background: "#1f2937", 
                color: "white", 
                display: "flex", 
                flexDirection: "column",
                overflowY: "auto"
            }}>
                <h2 style={{ marginBottom: 20 }}>🛒 Korpa</h2>

                <div style={{ flex: 1, overflowY: "auto", marginBottom: 20 }}>
                    {cart.length === 0 ? (
                        <p style={{ textAlign: "center", color: "#9ca3af", marginTop: 40 }}>Korpa je prazna</p>
                    ) : (
                        cart.map(i => (
                            <div
                                key={i.productId}
                                style={{
                                    padding: 12,
                                    marginBottom: 8,
                                    background: "#374151",
                                    borderRadius: 8,
                                }}
                            >
                                <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 8 }}>
                                    <div style={{ fontWeight: 600, fontSize: 16 }}>{i.name}</div>
                                    <button
                                        onClick={() => removeFromCart(i.productId)}
                                        style={{
                                            background: "#dc2626",
                                            color: "white",
                                            border: "none",
                                            borderRadius: 4,
                                            padding: "4px 8px",
                                            cursor: "pointer",
                                            fontSize: 12
                                        }}
                                    >
                                        ✕
                                    </button>
                                </div>
                                
                                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                                    <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                                        <button
                                            onClick={() => updateQty(i.productId, -1)}
                                            style={{
                                                background: "#4b5563",
                                                color: "white",
                                                border: "none",
                                                borderRadius: 4,
                                                width: 30,
                                                height: 30,
                                                cursor: "pointer",
                                                fontSize: 18,
                                                fontWeight: 700,
                                                display: "flex",
                                                alignItems: "center",
                                                justifyContent: "center"
                                            }}
                                        >
                                            -
                                        </button>
                                        <span style={{ 
                                            fontSize: 18, 
                                            fontWeight: 600,
                                            minWidth: 30,
                                            textAlign: "center"
                                        }}>
                                            {i.qty}
                                        </span>
                                        <button
                                            onClick={() => updateQty(i.productId, 1)}
                                            style={{
                                                background: "#4b5563",
                                                color: "white",
                                                border: "none",
                                                borderRadius: 4,
                                                width: 30,
                                                height: 30,
                                                cursor: "pointer",
                                                fontSize: 18,
                                                fontWeight: 700,
                                                display: "flex",
                                                alignItems: "center",
                                                justifyContent: "center"
                                            }}
                                        >
                                            +
                                        </button>
                                    </div>
                                    <div style={{ fontSize: 18, fontWeight: 600, color: "#fbbf24" }}>
                                        {(i.price * i.qty).toFixed(2)} RSD
                                    </div>
                                </div>
                                
                                <div style={{ fontSize: 12, color: "#9ca3af", marginTop: 4 }}>
                                    {i.price.toFixed(2)} RSD × {i.qty}
                                </div>
                            </div>
                        ))
                    )}
                </div>

                <hr style={{ margin: "20px 0", borderColor: "#4b5563" }} />
                
                <div style={{ 
                    fontSize: window.innerWidth < 480 ? 24 : 28, 
                    fontWeight: 700, 
                    marginBottom: 20, 
                    textAlign: "right" 
                }}>
                    Total: {total.toFixed(2)} RSD
                </div>

                <button
                    onClick={pay}
                    disabled={cart.length === 0}
                    style={{
                        width: "100%",
                        padding: window.innerWidth < 480 ? 16 : 20,
                        fontSize: window.innerWidth < 480 ? 18 : 22,
                        fontWeight: 700,
                        background: cart.length === 0 ? "#6b7280" : "#059669",
                        color: "white",
                        border: "none",
                        borderRadius: 8,
                        cursor: cart.length === 0 ? "not-allowed" : "pointer"
                    }}
                >
                    💳 NAPLATI
                </button>

                {cart.length > 0 && (
                    <button
                        onClick={() => setCart([])}
                        style={{
                            width: "100%",
                            padding: 12,
                            fontSize: 16,
                            fontWeight: 600,
                            background: "transparent",
                            color: "#dc2626",
                            border: "2px solid #dc2626",
                            borderRadius: 8,
                            cursor: "pointer",
                            marginTop: 12
                        }}
                    >
                        🗑️ Očisti korpu
                    </button>
                )}
            </div>
        </div>
    );
}
