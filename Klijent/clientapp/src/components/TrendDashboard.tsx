/**
 * TrendDashboard â€“ dark-mode leaderboard for the latest scoring run.
 *
 * Features
 *  â€¢ PAL dark-mode design system (consistent with rest of app)
 *  â€¢ KPI stat cards at top (totals, momentum distribution, avg score)
 *  â€¢ Search by brand / name
 *  â€¢ Filter by source (multi-select pill) + market + momentum direction
 *  â€¢ Sortable columns (score, momentum, price, appearances)
 *  â€¢ Expanded row shows full score breakdown cards
 *  â€¢ Limit selector: 10 / 20 / 50
 *
 * Data source: GET /api/dashboard/latest  (Python FastAPI â†’ PostgreSQL)
 */

import { useState, useEffect, useCallback, useMemo } from "react";
import { fetchDashboard, type DashboardItem, type DashboardRun } from "../services/scoringApi";

// â”€â”€ Design tokens â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

const PAL = {
    blue:          "#4F8EF7",
    green:         "#4CAF82",
    yellow:        "#F5C542",
    orange:        "#F97316",
    red:           "#E05C5C",
    purple:        "#9B72CF",
    cyan:          "#22D3EE",
    bg:            "var(--surface-default, #0D0F14)",
    card:          "var(--surface-elevated, #161A23)",
    cardHover:     "var(--surface-elevated, #1C2133)",
    border:        "var(--border-default, #2A3045)",
    borderLight:   "var(--border-default, #212840)",
    textPrimary:   "var(--text-primary, #E8ECF4)",
    textSecondary: "var(--text-secondary, #8A95B0)",
    textMuted:     "var(--text-muted, #4A5477)",
};

const TOP1_BG = "var(--surface-darker, #1A1800)";
const TOP2_BG = "var(--surface-elevated, #111822)";
const TOP3_BG = "var(--surface-default, #140F1F)";

// â”€â”€ Component score colors (dark-mode palette) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

const COMPONENT_COLORS: Record<string, string> = {
    base_score:         PAL.blue,
    cross_source_mult:  PAL.green,
    cross_market_mult:  PAL.cyan,
    entropy_bonus:      PAL.purple,
    price_bonus:        PAL.yellow,
    reliability_factor: PAL.red,
};

// â”€â”€ Source colors (dark-mode) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

const SOURCE_COLOR: Record<string, { bg: string; text: string; border: string }> = {
    zalando:   { bg: "#2A1700", text: "#F97316", border: "#7C3416" },
    aboutyou:  { bg: "#1E1230", text: "#C084FC", border: "#6D28D9" },
    deichmann: { bg: "#2A0A10", text: "#F87171", border: "#7C2D2D" },
    humanic:   { bg: "#0A1F14", text: "#4CAF82", border: "#1A5C35" },
};
const SOURCE_EMOJI: Record<string, string> = {
    zalando: "ðŸŸ ", aboutyou: "ðŸŸ£", deichmann: "ðŸ”´", humanic: "ðŸŸ¢",
};
const MARKET_FLAG: Record<string, string> = {
    DE: "ðŸ‡©ðŸ‡ª", AT: "ðŸ‡¦ðŸ‡¹", CH: "ðŸ‡¨ðŸ‡­", HU: "ðŸ‡­ðŸ‡º", RO: "ðŸ‡·ðŸ‡´",
};
const MEDAL = ["ðŸ¥‡", "ðŸ¥ˆ", "ðŸ¥‰"];
const COMPONENT_LABELS: Record<string, string> = {
    base_score:         "Base",
    cross_source_mult:  "Cross-src",
    cross_market_mult:  "Cross-mkt",
    entropy_bonus:      "Entropy",
    price_bonus:        "Price pos",
    reliability_factor: "Reliability",
};

// â”€â”€ Momentum helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

function momentumColor(mn: number | null): string {
    if (mn === null) return PAL.textMuted;
    if (mn > 0.3)  return PAL.green;
    if (mn > 0)    return "#86EFAC";
    if (mn < -0.3) return PAL.red;
    if (mn < 0)    return "#FCA5A5";
    return PAL.textMuted;
}

function momentumArrow(mn: number | null): string {
    if (mn === null) return "â—";
    if (mn > 0.3)  return "â–²â–²";
    if (mn > 0)    return "â–²";
    if (mn < -0.3) return "â–¼â–¼";
    if (mn < 0)    return "â–¼";
    return "â†’";
}

function momentumLabel(mn: number | null): string {
    if (mn === null) return "new";
    const pct = (Math.abs(mn) * 100).toFixed(1);
    return `${mn > 0 ? "+" : "âˆ’"}${pct}%`;
}

function scoreGrade(score: number): { label: string; color: string } {
    if (score > 2.0) return { label: "S", color: PAL.yellow };
    if (score > 1.5) return { label: "A", color: PAL.green };
    if (score > 1.0) return { label: "B", color: PAL.blue };
    if (score > 0.5) return { label: "C", color: PAL.textSecondary };
    return { label: "D", color: PAL.textMuted };
}

// â”€â”€ KPI stat card â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

function StatCard({ label, value, sub, color }: {
    label: string;
    value: string | number;
    sub?: string;
    color: string;
}) {
    return (
        <div style={{
            background: PAL.card,
            border: `1px solid ${PAL.border}`,
            borderRadius: 12,
            padding: "14px 18px",
            flex: "1 1 140px",
            minWidth: 130,
        }}>
            <div style={{ fontSize: 11, color: PAL.textSecondary, fontWeight: 600, textTransform: "uppercase", letterSpacing: "0.06em", marginBottom: 6 }}>
                {label}
            </div>
            <div style={{ fontSize: 26, fontWeight: 800, color, lineHeight: 1 }}>
                {value}
            </div>
            {sub && (
                <div style={{ fontSize: 11, color: PAL.textMuted, marginTop: 4 }}>
                    {sub}
                </div>
            )}
        </div>
    );
}

// â”€â”€ Score bar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

function ScoreBar({ components }: { components: Record<string, number> | null }) {
    if (!components) return null;
    const keys = Object.keys(components).filter(
        (k) => k !== "final_score" && components[k] != null && components[k] > 0
    );
    if (keys.length === 0) return null;
    const total = keys.reduce((s, k) => s + (components[k] || 0), 0);
    return (
        <div style={{ display: "flex", height: 4, borderRadius: 3, overflow: "hidden", gap: 1, marginTop: 5 }} title="Score breakdown">
            {keys.map((k) => {
                const pct = total > 0 ? (components[k] / total) * 100 : 0;
                return (
                    <div
                        key={k}
                        title={`${COMPONENT_LABELS[k] ?? k}: ${components[k].toFixed(4)}`}
                        style={{ flex: pct, background: COMPONENT_COLORS[k] ?? PAL.border, minWidth: 2 }}
                    />
                );
            })}
        </div>
    );
}

// â”€â”€ Item row â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

function DashboardRow({
    item, rank, globalRank,
}: {
    item: DashboardItem;
    rank: number;
    globalRank: number;
}) {
    const [expanded, setExpanded] = useState(false);
    const mn = item.momentumNormalized;
    const isTop3 = globalRank <= 3;
    const medal = MEDAL[globalRank - 1];
    const grade = scoreGrade(item.finalScore);

    const rowBg = isTop3
        ? globalRank === 1 ? TOP1_BG
        : globalRank === 2 ? TOP2_BG
        : TOP3_BG
        : PAL.card;

    return (
        <>
            <tr
                onClick={() => setExpanded((x) => !x)}
                style={{ cursor: "pointer", transition: "background .1s" }}
                onMouseEnter={(e) => { (e.currentTarget as HTMLTableRowElement).style.background = PAL.cardHover; }}
                onMouseLeave={(e) => { (e.currentTarget as HTMLTableRowElement).style.background = rowBg; }}
            >
                {/* â”€â”€ Rank â”€â”€ */}
                <td style={{ width: 48, textAlign: "center", padding: "10px 4px", background: rowBg, borderBottom: `1px solid ${PAL.borderLight}` }}>
                    {medal
                        ? <span style={{ fontSize: 20 }}>{medal}</span>
                        : <span style={{ color: PAL.textMuted, fontWeight: 700, fontSize: 13 }}>#{globalRank}</span>
                    }
                </td>

                {/* â”€â”€ Thumbnail â”€â”€ */}
                <td style={{ width: 56, padding: "6px 4px", background: rowBg, borderBottom: `1px solid ${PAL.borderLight}` }}>
                    <div style={{
                        width: 46, height: 46, borderRadius: 8,
                        overflow: "hidden",
                        background: "var(--surface-default)",
                        border: `1px solid ${PAL.border}`,
                        display: "flex", alignItems: "center", justifyContent: "center",
                    }}>
                        {item.imageUrl ? (
                            <img
                                src={item.imageUrl}
                                alt={item.name ?? ""}
                                style={{ width: "100%", height: "100%", objectFit: "contain" }}
                                onError={(e) => { (e.target as HTMLImageElement).style.display = "none"; }}
                            />
                        ) : (
                            <span style={{ fontSize: 22 }}>ðŸ‘Ÿ</span>
                        )}
                    </div>
                </td>

                {/* â”€â”€ Brand + Name â”€â”€ */}
                <td style={{ padding: "8px 8px 8px 4px", minWidth: 200, background: rowBg, borderBottom: `1px solid ${PAL.borderLight}` }}>
                    <div style={{ fontSize: 10, color: PAL.textMuted, fontWeight: 700, textTransform: "uppercase", letterSpacing: "0.06em" }}>
                        {item.brand ?? "â€”"}
                    </div>
                    <div style={{ fontWeight: 700, fontSize: 13, color: PAL.textPrimary, lineHeight: 1.3, marginTop: 1 }}>
                        {item.name ?? "â€”"}
                    </div>
                    {item.category && (
                        <div style={{ fontSize: 10, color: PAL.textSecondary, marginTop: 2 }}>{item.category}</div>
                    )}
                    <ScoreBar components={item.scoreComponents} />
                </td>

                {/* â”€â”€ Score + Grade â”€â”€ */}
                <td style={{ padding: "8px 10px", textAlign: "center", whiteSpace: "nowrap", background: rowBg, borderBottom: `1px solid ${PAL.borderLight}` }}>
                    <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 3 }}>
                        <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                            <span style={{
                                width: 22, height: 22, borderRadius: 6,
                                background: grade.color + "22",
                                border: `1px solid ${grade.color}55`,
                                color: grade.color,
                                fontWeight: 800, fontSize: 11,
                                display: "flex", alignItems: "center", justifyContent: "center",
                            }}>{grade.label}</span>
                            <span style={{ fontWeight: 800, fontSize: 15, color: PAL.textPrimary }}>
                                {item.finalScore.toFixed(3)}
                            </span>
                        </div>
                        {item.prevFinalScore != null && (
                            <span style={{ fontSize: 9, color: PAL.textMuted }}>
                                prev {item.prevFinalScore.toFixed(3)}
                            </span>
                        )}
                    </div>
                </td>

                {/* â”€â”€ Momentum â”€â”€ */}
                <td style={{ padding: "8px 8px", textAlign: "center", whiteSpace: "nowrap", background: rowBg, borderBottom: `1px solid ${PAL.borderLight}` }}>
                    <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 2 }}>
                        <span style={{ fontSize: 18, color: momentumColor(mn), fontWeight: 900, lineHeight: 1 }}>
                            {momentumArrow(mn)}
                        </span>
                        <span style={{ fontSize: 10, color: momentumColor(mn), fontWeight: 600 }}>
                            {momentumLabel(mn)}
                        </span>
                    </div>
                </td>

                {/* â”€â”€ Coverage pills â”€â”€ */}
                <td style={{ padding: "8px 6px", background: rowBg, borderBottom: `1px solid ${PAL.borderLight}` }}>
                    <div style={{ display: "flex", flexWrap: "wrap", gap: 3, maxWidth: 260 }}>
                        <span style={{ fontSize: 10, background: "var(--surface-default)", color: PAL.textSecondary, borderRadius: 5, padding: "1px 6px", fontWeight: 600, border: `1px solid ${PAL.border}` }}>
                            ðŸ”„ {item.appearanceCount}Ã—
                        </span>
                        {item.totalRunAppearances > 1 && (
                            <span style={{ fontSize: 10, background: "var(--surface-default)", color: PAL.green, borderRadius: 5, padding: "1px 6px", border: "1px solid var(--success)" }} title="Appeared in N runs">
                                ðŸ“ˆ {item.totalRunAppearances} runs
                            </span>
                        )}
                        {(item.sources ?? []).map((src) => {
                            const c = SOURCE_COLOR[src] ?? { bg: "#1A2235", text: PAL.textSecondary, border: PAL.border };
                            return (
                                <span key={src} style={{ fontSize: 10, background: c.bg, color: c.text, border: `1px solid ${c.border}`, borderRadius: 5, padding: "1px 6px", fontWeight: 600 }}>
                                    {SOURCE_EMOJI[src] ?? "ðŸ›"} {src}
                                </span>
                            );
                        })}
                        {(item.markets ?? []).map((m) => (
                            <span key={m} style={{ fontSize: 10, background: "var(--surface-default)", color: PAL.textSecondary, borderRadius: 5, padding: "1px 5px", border: `1px solid ${PAL.borderLight}` }}>
                                {MARKET_FLAG[m] ?? "ðŸŒ"} {m}
                            </span>
                        ))}
                    </div>
                </td>

                {/* â”€â”€ Price â”€â”€ */}
                <td style={{ padding: "8px 8px", textAlign: "right", whiteSpace: "nowrap", background: rowBg, borderBottom: `1px solid ${PAL.borderLight}` }}>
                    {item.minPrice != null ? (
                        <span style={{ fontWeight: 700, color: PAL.green, fontSize: 13 }}>
                            {item.minPrice.toFixed(0)}
                            {item.maxPrice != null && item.maxPrice !== item.minPrice ? `â€“${item.maxPrice.toFixed(0)}` : ""}
                            <span style={{ fontSize: 10, color: PAL.textMuted, marginLeft: 2 }}>â‚¬</span>
                        </span>
                    ) : (
                        <span style={{ color: PAL.textMuted }}>â€”</span>
                    )}
                </td>

                {/* â”€â”€ Expand toggle â”€â”€ */}
                <td style={{ padding: "8px 10px", textAlign: "center", color: PAL.textMuted, fontSize: 12, background: rowBg, borderBottom: `1px solid ${PAL.borderLight}` }}>
                    {expanded ? "â–²" : "â–¼"}
                </td>
            </tr>

            {/* â”€â”€ Expanded score breakdown â”€â”€ */}
            {expanded && item.scoreComponents && (
                <tr>
                    <td colSpan={8} style={{
                        background: "var(--surface-default)",
                        borderBottom: `1px solid ${PAL.border}`,
                        padding: "14px 16px 18px 72px",
                    }}>
                        <div style={{ fontSize: 11, fontWeight: 700, color: PAL.textMuted, marginBottom: 10, textTransform: "uppercase", letterSpacing: "0.06em" }}>
                            Score Breakdown
                        </div>
                        <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
                            {Object.entries(item.scoreComponents)
                                .filter(([k]) => k !== "final_score")
                                .sort(([, a], [, b]) => b - a)
                                .map(([key, val]) => {
                                    const col = COMPONENT_COLORS[key] ?? PAL.textSecondary;
                                    return (
                                        <div key={key} style={{
                                            background: PAL.card,
                                            border: `1px solid ${col}44`,
                                            borderRadius: 10,
                                            padding: "8px 13px",
                                            minWidth: 100,
                                        }}>
                                            <div style={{ fontSize: 10, color: PAL.textMuted, marginBottom: 3 }}>
                                                {COMPONENT_LABELS[key] ?? key}
                                            </div>
                                            <div style={{ fontWeight: 800, color: col, fontSize: 15 }}>
                                                {typeof val === "number" ? val.toFixed(4) : val}
                                            </div>
                                            <div style={{ marginTop: 5, height: 3, borderRadius: 2, background: PAL.border }}>
                                                <div style={{
                                                    height: "100%", borderRadius: 2, background: col,
                                                    width: `${Math.min(100, (val / (item.finalScore || 1)) * 100)}%`,
                                                }} />
                                            </div>
                                        </div>
                                    );
                                })
                            }
                        </div>
                        {item.prevFinalScore != null && (
                            <div style={{ marginTop: 10, fontSize: 12, color: PAL.textSecondary }}>
                                Prethodni run: <strong style={{ color: PAL.textPrimary }}>{item.prevFinalScore.toFixed(4)}</strong>
                                {" â†’ "}
                                <strong style={{ color: PAL.blue }}>{item.finalScore.toFixed(4)}</strong>
                                {" "}
                                <span style={{ color: momentumColor(mn) }}>
                                    ({momentumArrow(mn)} {momentumLabel(mn)})
                                </span>
                            </div>
                        )}
                    </td>
                </tr>
            )}
        </>
    );
}

// â”€â”€ Filter types â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

type MomentumFilter = "all" | "rising" | "dropping" | "new";
type SortKey = "score" | "momentum" | "price" | "appearances";
type SortDir = "desc" | "asc";

const ALL_SOURCES = ["zalando", "aboutyou", "deichmann", "humanic"];
const ALL_MARKETS = ["DE", "AT", "CH", "HU", "RO"];

// â”€â”€ Main component â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

export function TrendDashboard() {
    const [data, setData] = useState<{ run: DashboardRun | null; items: DashboardItem[]; message?: string } | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [lastRefreshed, setLastRefreshed] = useState<Date | null>(null);

    // â”€â”€ Filters & sort â”€â”€
    const [search, setSearch] = useState("");
    const [activeSources, setActiveSources] = useState<Set<string>>(new Set());
    const [activeMarkets, setActiveMarkets] = useState<Set<string>>(new Set());
    const [momentumFilter, setMomentumFilter] = useState<MomentumFilter>("all");
    const [sortKey, setSortKey] = useState<SortKey>("score");
    const [sortDir, setSortDir] = useState<SortDir>("desc");
    const [limit, setLimit] = useState(20);

    const load = useCallback(() => {
        setLoading(true);
        setError(null);
        fetchDashboard(50)
            .then((d) => { setData(d); setLastRefreshed(new Date()); })
            .catch((e) => {
                setError(e instanceof Error ? e.message : String(e));
                setData({ run: null, items: [] });
            })
            .finally(() => setLoading(false));
    }, []);

    useEffect(() => { load(); }, [load]);

    const allItems = data?.items ?? [];
    const run = data?.run;

    // â”€â”€ KPI stats (computed from unfiltered items) â”€â”€
    const kpi = useMemo(() => {
        const rising    = allItems.filter((i) => (i.momentumNormalized ?? 0) > 0).length;
        const dropping  = allItems.filter((i) => (i.momentumNormalized ?? 0) < 0).length;
        const isNew     = allItems.filter((i) => i.momentumNormalized == null).length;
        const avgScore  = allItems.length > 0
            ? allItems.reduce((s, i) => s + i.finalScore, 0) / allItems.length
            : 0;
        const topScore  = allItems.length > 0 ? Math.max(...allItems.map((i) => i.finalScore)) : 0;
        return { rising, dropping, isNew, avgScore, topScore };
    }, [allItems]);

    // â”€â”€ Filtered + sorted items â”€â”€
    const filteredItems = useMemo(() => {
        let items = [...allItems];

        // search
        const q = search.trim().toLowerCase();
        if (q) {
            items = items.filter((i) =>
                (i.name ?? "").toLowerCase().includes(q) ||
                (i.brand ?? "").toLowerCase().includes(q) ||
                (i.category ?? "").toLowerCase().includes(q)
            );
        }

        // source filter
        if (activeSources.size > 0) {
            items = items.filter((i) =>
                (i.sources ?? []).some((s) => activeSources.has(s))
            );
        }

        // market filter
        if (activeMarkets.size > 0) {
            items = items.filter((i) =>
                (i.markets ?? []).some((m) => activeMarkets.has(m))
            );
        }

        // momentum filter
        if (momentumFilter === "rising")   items = items.filter((i) => (i.momentumNormalized ?? 0) > 0);
        if (momentumFilter === "dropping") items = items.filter((i) => (i.momentumNormalized ?? 0) < 0);
        if (momentumFilter === "new")      items = items.filter((i) => i.momentumNormalized == null);

        // sort
        items.sort((a, b) => {
            let diff = 0;
            if (sortKey === "score")       diff = a.finalScore - b.finalScore;
            if (sortKey === "momentum")    diff = (a.momentumNormalized ?? 0) - (b.momentumNormalized ?? 0);
            if (sortKey === "price")       diff = (a.minPrice ?? 0) - (b.minPrice ?? 0);
            if (sortKey === "appearances") diff = a.totalRunAppearances - b.totalRunAppearances;
            return sortDir === "desc" ? -diff : diff;
        });

        return items.slice(0, limit);
    }, [allItems, search, activeSources, activeMarkets, momentumFilter, sortKey, sortDir, limit]);

    // â”€â”€ Sort handler â”€â”€
    function handleSort(key: SortKey) {
        if (key === sortKey) {
            setSortDir((d) => (d === "desc" ? "asc" : "desc"));
        } else {
            setSortKey(key);
            setSortDir("desc");
        }
    }

    function sortIndicator(key: SortKey) {
        if (sortKey !== key) return <span style={{ color: PAL.textMuted, fontSize: 10 }}> â‡…</span>;
        return <span style={{ color: PAL.blue, fontSize: 10 }}> {sortDir === "desc" ? "â†“" : "â†‘"}</span>;
    }

    function toggleSource(src: string) {
        setActiveSources((prev) => {
            const next = new Set(prev);
            next.has(src) ? next.delete(src) : next.add(src);
            return next;
        });
    }

    function toggleMarket(m: string) {
        setActiveMarkets((prev) => {
            const next = new Set(prev);
            next.has(m) ? next.delete(m) : next.add(m);
            return next;
        });
    }

    const hasActiveFilters = search || activeSources.size > 0 || activeMarkets.size > 0 || momentumFilter !== "all";

    // â”€â”€ Render â”€â”€
    return (
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>

            {/* â•â• KPI cards â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â• */}
            {allItems.length > 0 && (
                <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
                    <StatCard label="Total items" value={allItems.length} sub={run ? `Run #${run.runId}` : undefined} color={PAL.blue} />
                    <StatCard label="Rising" value={kpi.rising} sub={`${((kpi.rising / allItems.length) * 100).toFixed(0)}% of items`} color={PAL.green} />
                    <StatCard label="Dropping" value={kpi.dropping} sub={`${((kpi.dropping / allItems.length) * 100).toFixed(0)}% of items`} color={PAL.red} />
                    <StatCard label="New entries" value={kpi.isNew} sub="no prior run" color={PAL.textSecondary} />
                    <StatCard label="Avg score" value={kpi.avgScore.toFixed(3)} sub={`top ${kpi.topScore.toFixed(3)}`} color={PAL.yellow} />
                </div>
            )}

            {/* â•â• Main card â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â• */}
            <div style={{
                background: PAL.card,
                border: `1px solid ${PAL.border}`,
                borderRadius: 16,
                overflow: "hidden",
            }}>

                {/* â”€â”€ Header â”€â”€ */}
                <div style={{
                    padding: "16px 20px",
                    borderBottom: `1px solid ${PAL.border}`,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    flexWrap: "wrap",
                    gap: 10,
                }}>
                    <div>
                        <h2 style={{ margin: 0, fontSize: 17, fontWeight: 800, color: PAL.textPrimary, display: "flex", alignItems: "center", gap: 8 }}>
                            ðŸ“Š Trend Leaderboard
                            {loading && (
                                <span style={{ fontSize: 11, background: "var(--surface-default)", color: PAL.blue, borderRadius: 6, padding: "2px 8px", fontWeight: 600 }}>
                                    Loadingâ€¦
                                </span>
                            )}
                        </h2>
                        <div style={{ fontSize: 12, color: PAL.textSecondary, marginTop: 3 }}>
                            {run
                                ? <>Run <strong style={{ color: PAL.textPrimary }}>#{run.runId}</strong> Â· {new Date(run.startedAt).toLocaleString("sr-Latn")} Â· <strong style={{ color: PAL.textPrimary }}>{run.totalItems}</strong> items</>
                                : "Poslednji scoring run iz baze"
                            }
                            {lastRefreshed && (
                                <span style={{ marginLeft: 10, color: PAL.textMuted }}>
                                    Â· refreshed {lastRefreshed.toLocaleTimeString("sr-Latn")}
                                </span>
                            )}
                        </div>
                    </div>

                    <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                        {/* Limit selector */}
                        <select
                            value={limit}
                            onChange={(e) => setLimit(Number(e.target.value))}
                            style={{
                                background: "var(--surface-default)",
                                border: `1px solid ${PAL.border}`,
                                borderRadius: 8,
                                color: PAL.textPrimary,
                                fontSize: 12,
                                padding: "6px 10px",
                                cursor: "pointer",
                            }}
                        >
                            <option value={10}>Top 10</option>
                            <option value={20}>Top 20</option>
                            <option value={50}>Top 50</option>
                        </select>

                        <button
                            onClick={load}
                            disabled={loading}
                            style={{
                                padding: "7px 16px",
                                borderRadius: 8,
                                border: `1.5px solid ${loading ? PAL.border : PAL.blue}`,
                                background: loading ? "#1A2235" : PAL.blue + "22",
                                color: loading ? PAL.textMuted : PAL.blue,
                                fontWeight: 700,
                                fontSize: 13,
                                cursor: loading ? "not-allowed" : "pointer",
                                transition: "all .15s",
                            }}
                        >
                            {loading ? "â³ Loadingâ€¦" : "ðŸ”„ Refresh"}
                        </button>
                    </div>
                </div>

                {/* â”€â”€ Filter toolbar â”€â”€ */}
                <div style={{
                    padding: "12px 20px",
                    borderBottom: `1px solid ${PAL.borderLight}`,
                    display: "flex",
                    flexWrap: "wrap",
                    gap: 10,
                    alignItems: "center",
                    background: "var(--surface-default)",
                }}>
                    {/* Search */}
                    <input
                        type="text"
                        placeholder="ðŸ” PretraÅ¾i brand / nazivâ€¦"
                        value={search}
                        onChange={(e) => setSearch(e.target.value)}
                        style={{
                            background: PAL.card,
                            border: `1px solid ${PAL.border}`,
                            borderRadius: 8,
                            color: PAL.textPrimary,
                            fontSize: 12,
                            padding: "6px 12px",
                            width: 200,
                            outline: "none",
                        }}
                    />

                    {/* Source pills */}
                    <div style={{ display: "flex", gap: 5, alignItems: "center" }}>
                        <span style={{ fontSize: 11, color: PAL.textMuted, fontWeight: 600 }}>Source:</span>
                        {ALL_SOURCES.map((src) => {
                            const c = SOURCE_COLOR[src] ?? { bg: PAL.card, text: PAL.textSecondary, border: PAL.border };
                            const active = activeSources.has(src);
                            return (
                                <button
                                    key={src}
                                    onClick={() => toggleSource(src)}
                                    style={{
                                        fontSize: 11,
                                        background: active ? c.bg : "transparent",
                                        color: active ? c.text : PAL.textMuted,
                                        border: `1px solid ${active ? c.border : PAL.borderLight}`,
                                        borderRadius: 6,
                                        padding: "2px 8px",
                                        cursor: "pointer",
                                        fontWeight: active ? 700 : 400,
                                        transition: "all .1s",
                                    }}
                                >
                                    {SOURCE_EMOJI[src]} {src}
                                </button>
                            );
                        })}
                    </div>

                    {/* Market pills */}
                    <div style={{ display: "flex", gap: 5, alignItems: "center" }}>
                        <span style={{ fontSize: 11, color: PAL.textMuted, fontWeight: 600 }}>TrÅ¾iÅ¡te:</span>
                        {ALL_MARKETS.map((m) => {
                            const active = activeMarkets.has(m);
                            return (
                                <button
                                    key={m}
                                    onClick={() => toggleMarket(m)}
                                    style={{
                                        fontSize: 11,
                                        background: active ? "#1A2A40" : "transparent",
                                        color: active ? PAL.blue : PAL.textMuted,
                                        border: `1px solid ${active ? PAL.blue + "55" : PAL.borderLight}`,
                                        borderRadius: 6,
                                        padding: "2px 7px",
                                        cursor: "pointer",
                                        fontWeight: active ? 700 : 400,
                                        transition: "all .1s",
                                    }}
                                >
                                    {MARKET_FLAG[m]} {m}
                                </button>
                            );
                        })}
                    </div>

                    {/* Momentum filter */}
                    <div style={{ display: "flex", gap: 5, alignItems: "center" }}>
                        <span style={{ fontSize: 11, color: PAL.textMuted, fontWeight: 600 }}>Momentum:</span>
                        {(["all", "rising", "dropping", "new"] as MomentumFilter[]).map((f) => {
                            const active = momentumFilter === f;
                            const colors: Record<MomentumFilter, string> = {
                                all: PAL.blue, rising: PAL.green, dropping: PAL.red, new: PAL.textSecondary,
                            };
                            const labels: Record<MomentumFilter, string> = {
                                all: "Svi", rising: "â–² Raste", dropping: "â–¼ Pada", new: "â— Novi",
                            };
                            return (
                                <button
                                    key={f}
                                    onClick={() => setMomentumFilter(f)}
                                    style={{
                                        fontSize: 11,
                                        background: active ? colors[f] + "22" : "transparent",
                                        color: active ? colors[f] : PAL.textMuted,
                                        border: `1px solid ${active ? colors[f] + "55" : PAL.borderLight}`,
                                        borderRadius: 6,
                                        padding: "2px 8px",
                                        cursor: "pointer",
                                        fontWeight: active ? 700 : 400,
                                        transition: "all .1s",
                                    }}
                                >
                                    {labels[f]}
                                </button>
                            );
                        })}
                    </div>

                    {/* Clear filters */}
                    {hasActiveFilters && (
                        <button
                            onClick={() => {
                                setSearch("");
                                setActiveSources(new Set());
                                setActiveMarkets(new Set());
                                setMomentumFilter("all");
                            }}
                            style={{
                                fontSize: 11,
                                background: "transparent",
                                color: PAL.red,
                                border: `1px solid ${PAL.red}44`,
                                borderRadius: 6,
                                padding: "2px 8px",
                                cursor: "pointer",
                                marginLeft: "auto",
                            }}
                        >
                            âœ• ObriÅ¡i filtere
                        </button>
                    )}

                    {/* Result count */}
                    <span style={{ fontSize: 11, color: PAL.textMuted, marginLeft: hasActiveFilters ? 0 : "auto" }}>
                        {filteredItems.length} / {allItems.length} prikazano
                    </span>
                </div>

                {/* â”€â”€ Error state â”€â”€ */}
                {error && (
                    <div style={{ padding: "14px 20px", background: "var(--surface-darker)", borderBottom: `1px solid ${PAL.red}44` }}>
                        <div style={{ fontWeight: 700, color: PAL.orange, fontSize: 13 }}>âš ï¸ Python servis nije dostupan</div>
                        <div style={{ color: PAL.textSecondary, fontSize: 12, marginTop: 4 }}>{error}</div>
                        <div style={{ color: PAL.textMuted, fontSize: 11, marginTop: 6 }}>
                            Pokreni <code style={{ background: "var(--surface-darker)", padding: "1px 5px", borderRadius: 4 }}>cd Python &amp;&amp; start_api.bat</code> da bi pokrenuo Python API servis.
                        </div>
                    </div>
                )}

                {/* â”€â”€ Empty state â”€â”€ */}
                {!loading && filteredItems.length === 0 && (
                    <div style={{ padding: 48, textAlign: "center", color: PAL.textMuted }}>
                        <div style={{ fontSize: 40, marginBottom: 12 }}>
                            {hasActiveFilters ? "ðŸ”" : "ðŸ“­"}
                        </div>
                        <div style={{ fontWeight: 600, fontSize: 15, color: PAL.textSecondary }}>
                            {hasActiveFilters
                                ? "Nema rezultata za odabrane filtere"
                                : data?.message?.includes("not yet initialized")
                                    ? "Scoring tabele nisu joÅ¡ inicijalozovane"
                                    : "Nema run-ova u bazi"
                            }
                        </div>
                        {!hasActiveFilters && (
                            <div style={{ fontSize: 13, marginTop: 8, color: PAL.textMuted }}>
                                {data?.message ?? "Podesi skrejpere, pokreni ih i rezultati Ä‡e se automatski pojaviti."}
                            </div>
                        )}
                        {!hasActiveFilters && data?.message?.includes("not yet initialized") && (
                            <div style={{ marginTop: 14, fontSize: 12, background: "#0A1F14", border: `1px solid #1A5C35`, borderRadius: 8, padding: "8px 16px", color: PAL.green, display: "inline-block" }}>
                                Pokreni: <code>Database/Analytics/004_AddScraperScoringTables.sql</code>
                            </div>
                        )}
                    </div>
                )}

                {/* â”€â”€ Table â”€â”€ */}
                {filteredItems.length > 0 && (
                    <div style={{ overflowX: "auto" }}>
                        <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 13 }}>
                            <thead>
                                <tr style={{ background: "#0F1320" }}>
                                    <th style={{ padding: "9px 4px", fontWeight: 700, color: PAL.textMuted, fontSize: 11, textAlign: "center", borderBottom: `1px solid ${PAL.border}` }}>#</th>
                                    <th style={{ padding: "9px 4px", borderBottom: `1px solid ${PAL.border}` }}></th>
                                    <th style={{ padding: "9px 6px", fontWeight: 700, color: PAL.textMuted, fontSize: 11, textAlign: "left", borderBottom: `1px solid ${PAL.border}` }}>
                                        Proizvod
                                    </th>
                                    <th
                                        onClick={() => handleSort("score")}
                                        style={{ padding: "9px 10px", fontWeight: 700, color: sortKey === "score" ? PAL.blue : PAL.textMuted, fontSize: 11, textAlign: "center", cursor: "pointer", borderBottom: `1px solid ${PAL.border}`, whiteSpace: "nowrap" }}>
                                        Score {sortIndicator("score")}
                                    </th>
                                    <th
                                        onClick={() => handleSort("momentum")}
                                        style={{ padding: "9px 8px", fontWeight: 700, color: sortKey === "momentum" ? PAL.blue : PAL.textMuted, fontSize: 11, textAlign: "center", cursor: "pointer", borderBottom: `1px solid ${PAL.border}`, whiteSpace: "nowrap" }}>
                                        Momentum {sortIndicator("momentum")}
                                    </th>
                                    <th
                                        onClick={() => handleSort("appearances")}
                                        style={{ padding: "9px 6px", fontWeight: 700, color: sortKey === "appearances" ? PAL.blue : PAL.textMuted, fontSize: 11, textAlign: "left", cursor: "pointer", borderBottom: `1px solid ${PAL.border}`, whiteSpace: "nowrap" }}>
                                        Pokrivenost {sortIndicator("appearances")}
                                    </th>
                                    <th
                                        onClick={() => handleSort("price")}
                                        style={{ padding: "9px 8px", fontWeight: 700, color: sortKey === "price" ? PAL.blue : PAL.textMuted, fontSize: 11, textAlign: "right", cursor: "pointer", borderBottom: `1px solid ${PAL.border}`, whiteSpace: "nowrap" }}>
                                        Cena {sortIndicator("price")}
                                    </th>
                                    <th style={{ width: 28, borderBottom: `1px solid ${PAL.border}` }}></th>
                                </tr>
                            </thead>
                            <tbody>
                                {filteredItems.map((item, idx) => (
                                    <DashboardRow
                                        key={item.itemId ?? `${item.canonicalKey}-${idx}`}
                                        item={item}
                                        rank={idx + 1}
                                        globalRank={item.rank ?? idx + 1}
                                    />
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}

                {/* â”€â”€ Legend â”€â”€ */}
                {filteredItems.length > 0 && (
                    <div style={{
                        padding: "10px 20px",
                        borderTop: `1px solid ${PAL.borderLight}`,
                        display: "flex",
                        gap: 14,
                        flexWrap: "wrap",
                        alignItems: "center",
                        background: "#0F1320",
                    }}>
                        <span style={{ fontSize: 11, color: PAL.textMuted, fontWeight: 600 }}>Score bar:</span>
                        {Object.entries(COMPONENT_LABELS).map(([k, label]) => (
                            <span key={k} style={{ fontSize: 11, color: PAL.textSecondary, display: "flex", alignItems: "center", gap: 4 }}>
                                <span style={{ display: "inline-block", width: 10, height: 10, borderRadius: 3, background: COMPONENT_COLORS[k] ?? PAL.border }} />
                                {label}
                            </span>
                        ))}
                        <span style={{ fontSize: 11, color: PAL.textMuted, marginLeft: "auto" }}>
                            Klikni red za detaljan score breakdown
                        </span>
                    </div>
                )}
            </div>
        </div>
    );
}
