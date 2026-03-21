import { useState, useEffect, useMemo } from "react";
import { apiCircuitBreaker, CircuitBreakerStats, CircuitState } from "../utils/circuitBreaker";

interface CircuitBreakerStatusProps {
    showAlways?: boolean;
}

export function CircuitBreakerStatus({ showAlways = false }: CircuitBreakerStatusProps) {
    const [stats, setStats] = useState<CircuitBreakerStats>(apiCircuitBreaker.getStats());
    const [countdown, setCountdown] = useState(0);

    useEffect(() => {
        return apiCircuitBreaker.subscribe(setStats);
    }, []);

    useEffect(() => {
        if (stats.state !== "OPEN") {
            return;
        }

        const updateCountdown = () => {
            const remaining = apiCircuitBreaker.getRemainingCooldown();
            setCountdown(Math.ceil(remaining / 1000));
        };

        updateCountdown();
        const interval = setInterval(updateCountdown, 1000);
        return () => clearInterval(interval);
    }, [stats.state, stats.lastFailureTime]);

    // Calculate display countdown - 0 when not OPEN
    const displayCountdown = useMemo(() => {
        return stats.state === "OPEN" ? countdown : 0;
    }, [stats.state, countdown]);

    if (!showAlways && stats.state === "CLOSED") return null;

    const stateConfig: Record<CircuitState, { bg: string; border: string; text: string; icon: string; label: string }> = {
        CLOSED: { bg: "#f0fdf4", border: "#059669", text: "#059669", icon: "??", label: "Healthy" },
        OPEN: { bg: "#fef2f2", border: "#dc2626", text: "#dc2626", icon: "??", label: "Unavailable" },
        HALF_OPEN: { bg: "#fef3c7", border: "#f59e0b", text: "#f59e0b", icon: "??", label: "Recovering" },
    };

    const config = stateConfig[stats.state];

    return (
        <div
            style={{
                position: "fixed",
                bottom: "20px",
                right: "20px",
                background: config.bg,
                border: `2px solid ${config.border}`,
                borderRadius: "12px",
                padding: "16px",
                boxShadow: "0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06)",
                zIndex: 9999,
                minWidth: "300px",
                maxWidth: "400px",
            }}
        >
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "12px" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
                    <span style={{ fontSize: "1.5rem" }}>{config.icon}</span>
                    <div>
                        <div style={{ fontWeight: 700, color: config.text, fontSize: "1rem" }}>
                            Circuit Breaker
                        </div>
                        <div className="text-xs text-muted">
                            {stats.name} - {config.label}
                        </div>
                    </div>
                </div>
                <span
                    style={{
                        padding: "4px 8px",
                        borderRadius: "4px",
                        background: config.border,
                        color: "white",
                        fontSize: "0.75rem",
                        fontWeight: 600,
                    }}
                >
                    {stats.state}
                </span>
            </div>

            <div
                style={{
                    display: "grid",
                    gridTemplateColumns: "1fr 1fr",
                    gap: "8px",
                    fontSize: "0.875rem",
                    marginBottom: "12px",
                }}
            >
                <div style={{ background: "rgba(0,0,0,0.05)", padding: "8px", borderRadius: "6px" }}>
                    <div className="text-xs text-muted">Total Requests</div>
                    <div className="font-semibold text-foreground">{stats.totalRequests}</div>
                </div>
                <div style={{ background: "rgba(0,0,0,0.05)", padding: "8px", borderRadius: "6px" }}>
                    <div className="text-xs text-muted">Failures</div>
                    <div className={stats.failures > 0 ? "font-semibold text-accent-error" : "font-semibold text-foreground"}>
                        {stats.failures}
                    </div>
                </div>
                <div style={{ background: "rgba(0,0,0,0.05)", padding: "8px", borderRadius: "6px" }}>
                    <div className="text-xs text-muted">Rejected</div>
                    <div className={stats.rejectedRequests > 0 ? "font-semibold text-accent-warning" : "font-semibold text-foreground"}>
                        {stats.rejectedRequests}
                    </div>
                </div>
                {stats.state === "OPEN" && (
                    <div style={{ background: "rgba(0,0,0,0.05)", padding: "8px", borderRadius: "6px" }}>
                        <div className="text-xs text-muted">Retry In</div>
                        <div className="font-semibold text-accent-error">{displayCountdown}s</div>
                    </div>
                )}
            </div>

            {stats.state === "OPEN" && (
                <div
                    style={{
                        background: "#fef2f2",
                        padding: "8px 12px",
                        borderRadius: "6px",
                        marginBottom: "12px",
                        fontSize: "0.875rem",
                    }}
                    className="text-accent-error"
                >
                    ?? Backend is temporarily unavailable.
                </div>
            )}

            {stats.state === "HALF_OPEN" && (
                <div
                    style={{
                        background: "#fef3c7",
                        padding: "8px 12px",
                        borderRadius: "6px",
                        marginBottom: "12px",
                        fontSize: "0.875rem",
                    }}
                    className="text-accent-warning"
                >
                    ?? Testing backend availability...
                </div>
            )}

            {stats.state !== "CLOSED" && (
                <button
                    onClick={() => apiCircuitBreaker.reset()}
                    style={{
                        width: "100%",
                        padding: "10px 16px",
                        background: config.border,
                        color: "white",
                        border: "none",
                        borderRadius: "6px",
                        cursor: "pointer",
                        fontWeight: 600,
                        fontSize: "0.875rem",
                    }}
                >
                    ?? Reset Circuit Breaker
                </button>
            )}
        </div>
    );
}

export default CircuitBreakerStatus;
