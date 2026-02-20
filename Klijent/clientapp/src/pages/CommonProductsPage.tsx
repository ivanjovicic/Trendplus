import React, { useState } from "react";
import { fetchCommonProducts } from "../services/commonProductsApi";

type ProductSide = {
  name: string;
  price: number | string;
  image?: string | null;
  url?: string | null;
};

type CommonProductItemDto = {
  score: number;
  brand: string;
  type: string;
  zalando: ProductSide;
  deichmann: ProductSide;
};

const defaultFilters: Record<string, any> = {
  gender: "women",
  category: "sneakers",
  brand: "",
  priceMin: 0,
  priceMax: 200,
  sort: "popularity",
  sale: false,
  isNew: false,
  pages: 3,
  minScore: 60,
};

export default function CommonProductsPage() {
  const [filters, setFilters] = useState<any>(defaultFilters);
  const [items, setItems] = useState<CommonProductItemDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const loadData = async () => {
    try {
      setLoading(true);
      setError("");

      const payload: Record<string, any> = { ...filters };
      if (payload.pages === undefined || payload.pages === null || payload.pages === "") delete payload.pages;

      const res = await fetchCommonProducts(payload);
      // ensure array
      const arr = Array.isArray(res) ? res : res.items ?? [];
      setItems(arr);
    } catch (err: any) {
      console.error(err);
      setError(err.message || "Greška pri učitavanju podataka");
    } finally {
      setLoading(false);
    }
  };

  // Do not auto-load on mount — search only when user clicks the button

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const target = e.target as HTMLInputElement;
    const { name, value, type, checked } = target;

    setFilters((prev: any) => ({
      ...prev,
      [name]: type === "checkbox" ? checked : (type === "number" ? (value === "" ? undefined : Number(value)) : value),
    }));
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    loadData();
  };

  return (
    <div style={{ padding: "24px" }}>
      <h1>Zajednički artikli – Zalando & Deichmann</h1>

      {/* Compact filter bar (Deichmann-style) */}
      <form onSubmit={handleSubmit} style={{ marginBottom: 16 }}>
        <div style={{
          display: 'flex',
          gap: 12,
          alignItems: 'flex-end',
          flexWrap: 'wrap',
          padding: 12,
          borderRadius: 12,
          background: '#fff',
          border: '1px solid #e5e7eb'
        }}>
          <div style={{ minWidth: 160 }}>
            <label className="field-label">Pol</label>
            <select name="gender" value={filters.gender} onChange={handleChange} className="input-big">
              <option value="">Unisex</option>
              <option value="women">Žene</option>
              <option value="men">Muškarci</option>
            </select>
          </div>

          <div style={{ minWidth: 160 }}>
            <label className="field-label">Kategorija</label>
            <select name="category" value={filters.category} onChange={handleChange} className="input-big">
              <option value="sneakers">Patike</option>
              <option value="shoes">Cipele</option>
              <option value="boots">Čizme</option>
            </select>
          </div>

          <div style={{ minWidth: 220 }}>
            <label className="field-label">Brend</label>
            <input name="brand" value={filters.brand} onChange={handleChange} className="input-big" placeholder="npr. rieker" />
          </div>

          <div style={{ minWidth: 120 }}>
            <label className="field-label">Min cena (€)</label>
            <input type="number" name="priceMin" value={filters.priceMin ?? ""} onChange={handleChange} className="input-big" />
          </div>

          <div style={{ minWidth: 120 }}>
            <label className="field-label">Max cena (€)</label>
            <input type="number" name="priceMax" value={filters.priceMax ?? ""} onChange={handleChange} className="input-big" />
          </div>

          <div style={{ minWidth: 120 }}>
            <label className="field-label">Strane</label>
            <input type="number" name="pages" className="input-big" min={1} value={filters.pages ?? ""} onChange={handleChange} />
          </div>

          <div style={{ minWidth: 140 }}>
            <label className="field-label">Min score</label>
            <input type="number" name="minScore" min={0} max={100} value={filters.minScore ?? 60} onChange={handleChange} className="input-big" />
          </div>

          <div style={{ marginLeft: 'auto' }}>
            <button type="submit" disabled={loading} className="button-big" style={{ minWidth: 160 }}>
              {loading ? '⏳ Pretraga...' : '🔍 Pretraži'}
            </button>
          </div>
        </div>
      </form>

      {error && <div style={{ color: 'red', marginBottom: 8 }}>{error}</div>}

      {/* Results grid */}
      {!loading && items?.length > 0 && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(340px, 1fr))', gap: 20 }}>
          {items.map((m, idx) => (
            <div key={idx} style={{ border: '1px solid #e5e7eb', borderRadius: 12, padding: 16, background: '#fff' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
                <div style={{ fontWeight: 700 }}>{m.brand} • {m.type}</div>
                <div style={{ fontSize: 12, color: '#6b7280' }}>Score: <b>{m.score}</b>/100</div>
              </div>

              <div style={{ height: 8, background: '#f3f4f6', borderRadius: 6, overflow: 'hidden', marginBottom: 12 }}>
                <div style={{ width: `${m.score}%`, height: '100%', background: m.score > 80 ? '#16a34a' : '#f97316' }} />
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
                <div>
                  {m.zalando.image ? (
                    <img src={m.zalando.image} alt={m.zalando.name} style={{ width: '100%', borderRadius: 8 }} onError={(e) => { (e.target as HTMLImageElement).src = 'https://via.placeholder.com/400x300?text=No+Image' }} />
                  ) : null}
                  <div style={{ fontWeight: 600, marginTop: 8 }}>{m.zalando.name}</div>
                  <div style={{ color: '#059669', fontWeight: 700 }}>{typeof m.zalando.price === 'number' ? `€${(m.zalando.price as number).toFixed(2)}` : m.zalando.price}</div>
                  {m.zalando.url && <a href={m.zalando.url} target="_blank" rel="noreferrer" style={{ color: '#2563eb' }}>Open Zalando →</a>}
                </div>

                <div>
                  {m.deichmann.image ? (
                    <img src={m.deichmann.image} alt={m.deichmann.name} style={{ width: '100%', borderRadius: 8 }} onError={(e) => { (e.target as HTMLImageElement).src = 'https://via.placeholder.com/400x300?text=No+Image' }} />
                  ) : null}
                  <div style={{ fontWeight: 600, marginTop: 8 }}>{m.deichmann.name}</div>
                  <div style={{ color: '#059669', fontWeight: 700 }}>{typeof m.deichmann.price === 'number' ? `€${(m.deichmann.price as number).toFixed(2)}` : m.deichmann.price}</div>
                  {m.deichmann.url && <a href={m.deichmann.url} target="_blank" rel="noreferrer" style={{ color: '#2563eb' }}>Open Deichmann →</a>}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {!loading && !error && items?.length === 0 && (
        <div>Nema zajedničkih artikala za zadate filtere.</div>
      )}
    </div>
  );
}
