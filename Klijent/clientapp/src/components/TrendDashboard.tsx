/**
 * TrendDashboard – reads the latest scoring run from the DB and renders
 * a ranked leaderboard with momentum indicators, score breakdown bars,
 * source/market pills, and price range chips.
 *
 * Data source: GET /api/dashboard/latest   (Python FastAPI → PostgreSQL)
 *
 * Momentum legend
 *   ▲▲  > +30 %   strong rise   (emerald)
 *   ▲   > 0 %     rise          (green)
 *   ●   null       first time    (gray)
 *   ▼   < 0 %     drop          (orange-red)
 *   ▼▼  < -30 %   strong drop   (red)
 */

import { useState, useEffect, useCallback } from "react";
import { fetchDashboard, type DashboardItem, type DashboardRun } from "../services/scoringApi";

// ── Constants ────────────────────────────────────────────────────────────────

const SOURCE_COLOR: Record<string, { bg: string; text: string; border: string }> = {
    zalando:   { bg: "#fff7ed", text: "#c2410c", border: "#fed7aa" },
    aboutyou:  { bg: "#faf5ff", text: "#7e22ce", border: "#e9d5ff" },
    deichmann: { bg: "#fff1f2", text: "#be123c", border: "#fecdd3" },
    humanic:   { bg: "#f0fdf4", text: "#15803d", border: "#bbf7d0" },
};
const SOURCE_EMOJI: Record<string, string> = {
    zalando: "🟠", aboutyou: "🟣", deichmann: "🔴", humanic: "🟢",
};
const MARKET_FLAG: Record<string, string> = {
    DE: "🇩🇪", AT: "🇦🇹", CH: "🇨🇭", HU: "🇭🇺", RO: "🇷🇴",
};
const MEDAL = ["🥇", "🥈", "🥉"];

const COMPONENT_COLORS: Record<string, string> = {
    base_score:        "#4f46e5",
    cross_source_mult: "#059669",
    cross_market_mult: "#0891b2",
    entropy_bonus:     "#7c3aed",
    price_bonus:       "#d97706",
    reliability_factor:"#be123c",
    final_score:       "#111827",
};
const COMPONENT_LABELS: Record<string, string> = {
    base_score:        "Base",
    cross_source_mult: "Cross-src",
    cross_market_mult: "Cross-mkt",
    entropy_bonus:     "Entropy",
    price_bonus:       "Price pos",
    reliability_factor:"Reliability",
};

// ── Momentum helpers ─────────────────────────────────────────────────────────

function momentumColor(mn: number | null): string {
    if (mn === null) return "#9ca3af";
    if (mn > 0.3)  return "#059669";
    if (mn > 0)    return "#4ade80";
    if (mn < -0.3) return "#dc2626";
    if (mn < 0)    return "#f87171";
    return "#9ca3af";
}

function momentumArrow(mn: number | null): string {
    if (mn === null) return "●";
    if (mn > 0.3)  return "▲▲";
    if (mn > 0)    return "▲";
    if (mn < -0.3) return "▼▼";
    if (mn < 0)    return "▼";
    return "→";
}

function momentumLabel(mn: number | null): string {
    if (mn === null) return "new";
    const pct = (mn * 100).toFixed(1);
    return `${mn > 0 ? "+" : ""}${pct}%`;
}

// ── Score bar ────────────────────────────────────────────────────────────────

function ScoreBar({ components }: { components: Record<string, number> | null }) {
    if (!components) return null;
    const keys = Object.keys(components).filter(
        (k) => k !== "final_score" && components[k] != null && components[k] > 0
    );
    if (keys.length === 0) return null;
    const total = keys.reduce((s, k) => s + (components[k] || 0), 0);
    return (
        <div style={{ display: "flex", height: 5, borderRadius: 4, overflow: "hidden", gap: 1, marginTop: 5 }} title="Score breakdown">
            {keys.map((k) => {
                const pct = total > 0 ? (components[k] / total) * 100 : 0;
                return (
                    <div
                        key={k}
                        title={`${COMPONENT_LABELS[k] ?? k}: ${components[k].toFixed(4)}`}
                        style={{ flex: pct, background: COMPONENT_COLORS[k] ?? "#e5e7eb", minWidth: 2 }}
                    />
                );
            })}
        </div>
    );
}

// ── Item row ─────────────────────────────────────────────────────────────────

function DashboardRow({ item, rank }: { item: DashboardItem; rank: number }) {
    const [expanded, setExpanded] = useState(false);
    const mn = item.momentumNormalized;
    const isTop3 = rank <= 3;
    const medal = MEDAL[rank - 1];

    const scoreDisplayColor =
        item.finalScore > 1.5 ? "#059669" :
        item.finalScore > 0.8 ? "#4f46e5" :
        "#6b7280";

    return (
        <>
            <tr
                style={{
                    background: isTop3 ? (rank === 1 ? "#fffbeb" : rank === 2 ? "#f8faff" : "#fdf4ff") : "white",
                    borderBottom: "1px solid #f3f4f6",
                    cursor: "pointer",
                    transition: "background .12s",
                }}
                onClick={() => setExpanded((x) => !x)}
                onMouseEnter={(e) => { (e.currentTarget as HTMLTableRowElement).style.background = "#f9fafb"; }}
                onMouseLeave={(e) => { (e.currentTarget as HTMLTableRowElement).style.background = isTop3 ? (rank === 1 ? "#fffbeb" : rank === 2 ? "#f8faff" : "#fdf4ff") : "white"; }}
            >
                {/* Rank */}
                <td style={{ width: 44, textAlign: "center", padding: "10px 4px" }}>
                    {medal
                        ? <span style={{ fontSize: 20 }}>{medal}</span>
                        : <span style={{ color: "#9ca3af", fontWeight: 700, fontSize: 13 }}>#{rank}</span>
                    }
                </td>

                {/* Thumbnail */}
                <td style={{ width: 56, padding: "6px 4px" }}>
                    <div style={{ width: 48, height: 48, borderRadius: 8, overflow: "hidden", background: "#f3f4f6", display: "flex", alignItems: "center", justifyContent: "center" }}>
                        {item.imageUrl ? (
                            <img
                                src={item.imageUrl}
                                alt={item.name ?? ""}
                                style={{ width: "100%", height: "100%", objectFit: "contain" }}
                                onError={(e) => { (e.target as HTMLImageElement).style.display = "none"; }}
                            />
                        ) : (
                            <span style={{ fontSize: 22 }}>👟</span>
                        )}
                    </div>
                </td>

                {/* Brand + Name */}
                <td style={{ padding: "8px 8px 8px 2px", minWidth: 180 }}>
                    <div style={{ fontSize: 11, color: "#9ca3af", fontWeight: 600, textTransform: "uppercase", letterSpacing: "0.04em" }}>
                        {item.brand ?? "—"}
                    </div>
                    <div style={{ fontWeight: 700, fontSize: 14, color: "#111827", lineHeight: 1.3 }}>
                        {item.name ?? "—"}
                    </div>
                    {item.category && (
                        <div style={{ fontSize: 10, color: "#6b7280", marginTop: 1 }}>{item.category}</div>
                    )}
                    <ScoreBar components={item.scoreComponents} />
                </td>

                {/* Score */}
                <td style={{ padding: "8px 6px", textAlign: "center", whiteSpace: "nowrap" }}>
                    <span style={{
                        background: "#f0f0fe",
                        color: scoreDisplayColor,
                        borderRadius: 8,
                        padding: "4px 9px",
                        fontWeight: 800,
                        fontSize: 14,
                    }}>
                        ★ {item.finalScore.toFixed(3)}
                    </span>
                </td>

                {/* Momentum */}
                <td style={{ padding: "8px 6px", textAlign: "center", whiteSpace: "nowrap" }}>
                    <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 2 }}>
                        <span style={{
                            fontSize: 18,
                            color: momentumColor(mn),
                            fontWeight: 900,
                            lineHeight: 1,
                        }}>
                            {momentumArrow(mn)}
                        </span>
                        <span style={{ fontSize: 10, color: momentumColor(mn), fontWeight: 600 }}>
                            {momentumLabel(mn)}
                        </span>
                    </div>
                </td>

                {/* Appearances + Sources + Markets */}
                <td style={{ padding: "8px 6px" }}>
                    <div style={{ display: "flex", flexWrap: "wrap", gap: 3, maxWidth: 280 }}>
                        {/* Appearances pill */}
                        <span style={{ fontSize: 10, background: "#f3f4f6", color: "#374151", borderRadius: 5, padding: "1px 6px", fontWeight: 600 }}>
                            🔄 {item.appearanceCount}×
                        </span>
                        {/* Run appearances */}
                        {item.totalRunAppearances > 1 && (
                            <span style={{ fontSize: 10, background: "#ecfdf5", color: "#065f46", borderRadius: 5, padding: "1px 6px" }} title="Appeared in N runs">
                                📈 {item.totalRunAppearances} runs
                            </span>
                        )}
                        {/* Source pills */}
                        {(item.sources ?? []).map((src) => {
                            const c = SOURCE_COLOR[src] ?? { bg: "#f3f4f6", text: "#374151", border: "#e5e7eb" };
                            return (
                                <span key={src} style={{ fontSize: 10, background: c.bg, color: c.text, border: `1px solid ${c.border}`, borderRadius: 5, padding: "1px 6px", fontWeight: 600 }}>
                                    {SOURCE_EMOJI[src] ?? "🛍"} {src}
                                </span>
                            );
                        })}
                        {/* Market flags */}
                        {(item.markets ?? []).map((m) => (
                            <span key={m} style={{ fontSize: 10, background: "#f8faff", color: "#374151", borderRadius: 5, padding: "1px 5px" }}>
                                {MARKET_FLAG[m] ?? "🌍"} {m}
                            </span>
                        ))}
                    </div>
                </td>

                {/* Price */}
                <td style={{ padding: "8px 6px", textAlign: "right", whiteSpace: "nowrap" }}>
                    {item.minPrice != null ? (
                        <span style={{ fontWeight: 700, color: "#059669", fontSize: 13 }}>
                            {item.minPrice.toFixed(0)}{item.maxPrice != null && item.maxPrice !== item.minPrice ? `–${item.maxPrice.toFixed(0)}` : ""}
                        </span>
                    ) : (
                        <span style={{ color: "#d1d5db" }}>—</span>
                    )}
                </td>

                {/* Expand indicator */}
                <td style={{ padding: "8px 8px", textAlign: "center", color: "#9ca3af", fontSize: 12 }}>
                    {expanded ? "▲" : "▼"}
                </td>
            </tr>

            {/* Expanded score breakdown */}
            {expanded && item.scoreComponents && (
                <tr style={{ background: "#f9fafb" }}>
                    <td colSpan={8} style={{ padding: "10px 16px 14px 68px" }}>
                        <div style={{ fontSize: 11, fontWeight: 700, color: "#6b7280", marginBottom: 8, textTransform: "uppercase", letterSpacing: "0.05em" }}>
                            Score Breakdown
                        </div>
                        <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
                            {Object.entries(item.scoreComponents)
                                .filter(([k]) => k !== "final_score")
                                .sort(([, a], [, b]) => b - a)
                                .map(([key, val]) => (
                                    <div key={key} style={{
                                        background: "white",
                                        border: `1.5px solid ${COMPONENT_COLORS[key] ?? "#e5e7eb"}`,
                                        borderRadius: 8,
                                        padding: "5px 10px",
                                        minWidth: 90,
                                    }}>
                                        <div style={{ fontSize: 10, color: "#9ca3af", marginBottom: 2 }}>{COMPONENT_LABELS[key] ?? key}</div>
                                        <div style={{ fontWeight: 800, color: COMPONENT_COLORS[key] ?? "#111827", fontSize: 14 }}>
                                            {typeof val === "number" ? val.toFixed(4) : val}
                                        </div>
                                    </div>
                                ))
                            }
                        </div>
                        {item.prevFinalScore != null && (
                            <div style={{ marginTop: 8, fontSize: 12, color: "#6b7280" }}>
                                Previous run score: <strong>{item.prevFinalScore.toFixed(4)}</strong>
                                {" → "}
                                <strong>{item.finalScore.toFixed(4)}</strong>
                                {" "}
                                <span style={{ color: momentumColor(item.momentumNormalized) }}>
                                    ({momentumArrow(item.momentumNormalized)} {momentumLabel(item.momentumNormalized)})
                                </span>
                            </div>
                        )}
                    </td>
                </tr>
            )}
        </>
    );
}

// ── Main component ───────────────────────────────────────────────────────────

export function TrendDashboard() {
    const [data, setData] = useState<{ run: DashboardRun | null; items: DashboardItem[] } | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [lastRefreshed, setLastRefreshed] = useState<Date | null>(null);

    const load = useCallback(() => {
        setLoading(true);
        setError(null);
        fetchDashboard(20)
            .then((d) => {
                setData(d);
                setLastRefreshed(new Date());
            })
            .catch((e) => setError(e instanceof Error ? e.message : String(e)))
            .finally(() => setLoading(false));
    }, []);

    useEffect(() => { load(); }, [load]);

    const run = data?.run;
    const items = data?.items ?? [];

    const risingCount  = items.filter((i) => (i.momentumNormalized ?? 0) > 0).length;
    const droppingCount = items.filter((i) => (i.momentumNormalized ?? 0) < 0).length;
    const newCount     = items.filter((i) => i.momentumNormalized == null).length;

    return (
        <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 16, boxShadow: "0 2px 12px rgba(0,0,0,.07)", overflow: "hidden", marginBottom: 28 }}>

            {/* ── Header ── */}
            <div style={{ padding: "16px 20px", borderBottom: "1px solid #f3f4f6", display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 10 }}>
                <div>
                    <h2 style={{ margin: 0, fontSize: 18, fontWeight: 800, color: "#111827", display: "flex", alignItems: "center", gap: 8 }}>
                        📊 Trend Dashboard
                        {loading && <span style={{ fontSize: 12, background: "#eff6ff", color: "#2563eb", borderRadius: 6, padding: "2px 8px", fontWeight: 600 }}>Loading…</span>}
                    </h2>
                    <div style={{ fontSize: 12, color: "#6b7280", marginTop: 2 }}>
                        {run
                            ? <>Run <strong>#{run.runId}</strong> · started {new Date(run.startedAt).toLocaleString()} · <strong>{run.totalItems}</strong> items</>
                            : "Latest scoring run from database"
                        }
                        {lastRefreshed && <span style={{ marginLeft: 10, color: "#d1d5db" }}>refreshed {lastRefreshed.toLocaleTimeString()}</span>}
                    </div>
                </div>

                <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                    {/* Momentum summary pills */}
                    {items.length > 0 && (
                        <div style={{ display: "flex", gap: 6 }}>
                            <span style={{ fontSize: 12, background: "#ecfdf5", color: "#059669", border: "1px solid #a7f3d0", borderRadius: 8, padding: "3px 10px", fontWeight: 700 }}>
                                ▲ {risingCount} rising
                            </span>
                            {droppingCount > 0 && (
                                <span style={{ fontSize: 12, background: "#fff1f2", color: "#be123c", border: "1px solid #fecdd3", borderRadius: 8, padding: "3px 10px", fontWeight: 700 }}>
                                    ▼ {droppingCount} dropping
                                </span>
                            )}
                            {newCount > 0 && (
                                <span style={{ fontSize: 12, background: "#f3f4f6", color: "#6b7280", border: "1px solid #e5e7eb", borderRadius: 8, padding: "3px 10px", fontWeight: 700 }}>
                                    ● {newCount} new
                                </span>
                            )}
                        </div>
                    )}

                    <button
                        onClick={load}
                        disabled={loading}
                        style={{
                            padding: "7px 16px", borderRadius: 8, border: "1.5px solid #4f46e5",
                            background: loading ? "#f3f4f6" : "#4f46e5", color: loading ? "#9ca3af" : "white",
                            fontWeight: 700, fontSize: 13, cursor: loading ? "not-allowed" : "pointer",
                            transition: "all .15s",
                        }}
                    >
                        {loading ? "⏳ Loading…" : "🔄 Refresh"}
                    </button>
                </div>
            </div>

            {/* ── Error state ── */}
            {error && (
                <div style={{ padding: "20px 24px", background: "#fff1f2", borderBottom: "1px solid #fecdd3" }}>
                    <div style={{ fontWeight: 700, color: "#be123c", fontSize: 13 }}>❌ DB not reachable</div>
                    <div style={{ color: "#6b7280", fontSize: 12, marginTop: 4 }}>{error}</div>
                    <div style={{ color: "#9ca3af", fontSize: 11, marginTop: 4 }}>
                        Run scrapers first to populate the DB, or check that the Python API is running and the analytics DB is configured.
                    </div>
                </div>
            )}

            {/* ── No data ── */}
            {!loading && !error && items.length === 0 && (
                <div style={{ padding: 40, textAlign: "center", color: "#9ca3af" }}>
                    <div style={{ fontSize: 40, marginBottom: 10 }}>📭</div>
                    <div style={{ fontWeight: 600, fontSize: 15, color: "#6b7280" }}>No scored runs in the database yet</div>
                    <div style={{ fontSize: 13, marginTop: 6 }}>Configure scrapers above, run them, and the results will appear here automatically.</div>
                </div>
            )}

            {/* ── Table ── */}
            {items.length > 0 && (
                <div style={{ overflowX: "auto" }}>
                    <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 13 }}>
                        <thead>
                            <tr style={{ borderBottom: "2px solid #f3f4f6", background: "#f9fafb" }}>
                                <th style={{ padding: "8px 4px", fontWeight: 700, color: "#6b7280", fontSize: 11, textAlign: "center" }}>#</th>
                                <th style={{ padding: "8px 4px", fontWeight: 700, color: "#6b7280", fontSize: 11 }}></th>
                                <th style={{ padding: "8px 6px", fontWeight: 700, color: "#6b7280", fontSize: 11, textAlign: "left" }}>Product</th>
                                <th style={{ padding: "8px 6px", fontWeight: 700, color: "#6b7280", fontSize: 11, textAlign: "center" }}>Score</th>
                                <th style={{ padding: "8px 6px", fontWeight: 700, color: "#6b7280", fontSize: 11, textAlign: "center" }}>Momentum</th>
                                <th style={{ padding: "8px 6px", fontWeight: 700, color: "#6b7280", fontSize: 11, textAlign: "left" }}>Coverage</th>
                                <th style={{ padding: "8px 6px", fontWeight: 700, color: "#6b7280", fontSize: 11, textAlign: "right" }}>Price</th>
                                <th style={{ width: 26 }}></th>
                            </tr>
                        </thead>
                        <tbody>
                            {items.map((item, idx) => (
                                <DashboardRow key={item.itemId ?? `${item.canonicalKey}-${idx}`} item={item} rank={idx + 1} />
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            {/* ── Legend ── */}
            {items.length > 0 && (
                <div style={{ padding: "10px 20px", borderTop: "1px solid #f3f4f6", display: "flex", gap: 16, flexWrap: "wrap", alignItems: "center" }}>
                    <span style={{ fontSize: 11, color: "#9ca3af", fontWeight: 600 }}>Score bar:</span>
                    {Object.entries(COMPONENT_LABELS).map(([k, label]) => (
                        <span key={k} style={{ fontSize: 11, color: "#6b7280", display: "flex", alignItems: "center", gap: 4 }}>
                            <span style={{ display: "inline-block", width: 10, height: 10, borderRadius: 2, background: COMPONENT_COLORS[k] ?? "#e5e7eb" }} />
                            {label}
                        </span>
                    ))}
                    <span style={{ fontSize: 11, color: "#d1d5db", marginLeft: "auto" }}>Click any row to expand score breakdown</span>
                </div>
            )}
        </div>
    );
}
