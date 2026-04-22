import React, { useEffect, useState } from "react";
import { apiUrl } from "../utils/apiUrl";

type ProductItem = {
  id: number;
  name: string;
  brand?: string | null;
  price?: number | null;
  imageUrl?: string | null;
  url?: string | null;
};

export default function ZalandoProducts() {
  const [items, setItems] = useState<ProductItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let aborted = false;

    const fetchItems = async () => {
      setLoading(true);
      setError(null);
      try {
        const res = await fetch(apiUrl("/api/products?source=zalando"));
        if (!res.ok) {
          const text = await res.text().catch(() => null);
          throw new Error(text ?? `HTTP ${res.status}`);
        }
        const data: ProductItem[] = await res.json();
        if (!aborted) setItems(data ?? []);
      } catch (e: unknown) {
        if (!aborted) setError((e as Error)?.message ?? "Greška pri učitavanju proizvoda");
      } finally {
        if (!aborted) setLoading(false);
      }
    };

    fetchItems();
    return () => {
      aborted = true;
    };
  }, []);

  if (loading) return <div className="card">Učitavanje Zalando proizvoda...</div>;
  if (error) return <div className="card">Greška: {error}</div>;
  if (!items.length) return <div className="card">Nema proizvoda za prikaz.</div>;

  return (
    <div className="card" style={{ margin: "1rem auto", maxWidth: 1200 }}>
      <h2 style={{ marginBottom: 12 }}>Zalando proizvodi ({items.length})</h2>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))", gap: 20 }}>
        {items.map((p) => (
          <div key={p.id} style={{ border: "1px solid var(--c-e5e7eb, var(--theme-color-e5e7eb, #e5e7eb))", padding: 10, borderRadius: 8 }}>
            {p.imageUrl ? (
              <img src={p.imageUrl} alt={p.name} style={{ width: "100%", borderRadius: 8, objectFit: "cover", height: 180 }} />
            ) : (
              <div style={{ width: "100%", height: 180, background: "var(--gray-100)", borderRadius: 8 }} />
            )}

            <div style={{ paddingTop: 8 }}>
              <h3 style={{ margin: 0, fontSize: "1rem" }}>{p.brand}</h3>
              <p style={{ margin: "6px 0", fontWeight: 600 }}>{p.name}</p>
              <p style={{ margin: "6px 0", color: "var(--c-059669, var(--theme-color-059669, #059669))", fontWeight: 700 }}>
                {p.price != null ? `${p.price} RSD` : "-"}
              </p>
              {p.url && (
                <a href={p.url} target="_blank" rel="noopener noreferrer" style={{ color: "var(--c-2563eb, var(--theme-color-2563eb, #2563eb))" }}>
                  Pogledaj
                </a>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
