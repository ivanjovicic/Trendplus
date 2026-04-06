import React, { useEffect, useState } from 'react';
import { fetchReleaseCalendar } from '../services/releaseApi';

export default function ReleaseCalendar() {
    const [items, setItems] = useState<any[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [gender, setGender] = useState<string>('mens');
    const [refreshToggle, setRefreshToggle] = useState<number>(0);

    useEffect(() => {
        let cancelled = false;
        setLoading(true);
        setError(null);

        fetchReleaseCalendar(gender)
            .then((data) => {
                if (cancelled) return;
                setItems(Array.isArray(data) ? data : (data?.items ?? []));
            })
            .catch((e) => {
                if (cancelled) return;
                setError((e as Error).message);
            })
            .finally(() => {
                if (!cancelled) setLoading(false);
            });

        return () => { cancelled = true; };
    }, [gender, refreshToggle]);

    return (
        <div className="card">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
                <h1 style={{ margin: 0 }}>Coming Soon Sneakers</h1>

                <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                    <label style={{ fontWeight: 600 }}>Gender</label>
                    <select value={gender} onChange={(e) => setGender(e.target.value)} style={{ padding: '8px 10px', borderRadius: 8 }}>
                        <option value="mens">Mens</option>
                        <option value="womens">Womens</option>
                        <option value="kids">Kids</option>
                    </select>

                    <button
                        onClick={() => setRefreshToggle((t) => t + 1)}
                        className="button-big"
                        style={{ background: 'var(--c-3b82f6, var(--theme-color-3b82f6, var(--theme-color-3b82f6, #3b82f6)))', marginLeft: 8 }}
                    >
                        🔄 Refresh
                    </button>
                </div>
            </div>

            {loading && <div>Loading...</div>}
            {error && <div style={{ color: 'var(--c-dc2626, var(--theme-color-dc2626, var(--theme-color-dc2626, #dc2626)))' }}>Error: {error}</div>}

            {!loading && !error && (
                <div style={{ display: 'grid', gap: 12 }}>
                    {items.map((p, i) => (
                        <div key={i} style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
                            {p.image && <img src={p.image} width={120} alt="" />}
                            <div>
                                <div style={{ fontWeight: 700 }}>{p.brand} - {p.name}</div>
                                <div>Price: {p.price}</div>
                                {p.coming_soon && <div style={{ color: 'var(--c-059669, var(--theme-color-059669, var(--theme-color-059669, #059669)))', fontWeight: 700 }}>Coming: {p.release_date}</div>}
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}
