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
        CLOSED: { bg: 'var(--success-10)', border: 'var(--success)', text: 'var(--success)', icon: '✅', label: 'Healthy' },
        OPEN: { bg: 'var(--error-10)', border: 'var(--error)', text: 'var(--error)', icon: '⛔', label: 'Unavailable' },
        HALF_OPEN: { bg: 'var(--warning-10)', border: 'var(--warning)', text: 'var(--warning)', icon: '⚠️', label: 'Recovering' },
    };

    const config = stateConfig[stats.state];

    return (
        <div className="fixed bottom-5 right-5 rounded-xl p-4 shadow-lg z-[9999] min-w-[300px] max-w-[400px] border-2" style={{ borderColor: config.border, background: config.bg }}>
            <div className="flex items-center justify-between mb-3">
                <div className="flex items-center gap-2">
                    <span className="text-2xl">{config.icon}</span>
                    <div>
                        <div className="font-bold text-base" style={{ color: config.text }}>
                            Circuit Breaker
                        </div>
                        <div className="text-xs text-muted">
                            {stats.name} - {config.label}
                        </div>
                    </div>
                </div>
                <span className="px-2 py-1 rounded text-white text-xs font-semibold" style={{ background: config.border }}>
                    {stats.state}
                </span>
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 text-sm mb-3">
                <div className="p-2 rounded surface-muted">
                    <div className="text-xs text-muted">Total Requests</div>
                    <div className="font-semibold text-foreground">{stats.totalRequests}</div>
                </div>
                <div className="p-2 rounded surface-muted">
                    <div className="text-xs text-muted">Failures</div>
                    <div className={stats.failures > 0 ? "font-semibold text-accent-error" : "font-semibold text-foreground"}>
                        {stats.failures}
                    </div>
                </div>
                <div className="p-2 rounded surface-muted">
                    <div className="text-xs text-muted">Rejected</div>
                    <div className={stats.rejectedRequests > 0 ? "font-semibold text-accent-warning" : "font-semibold text-foreground"}>
                        {stats.rejectedRequests}
                    </div>
                </div>
                {stats.state === "OPEN" && (
                    <div className="p-2 rounded surface-muted">
                        <div className="text-xs text-muted">Retry In</div>
                        <div className="font-semibold text-accent-error">{displayCountdown}s</div>
                    </div>
                )}
            </div>

            {stats.state === "OPEN" && (
                <div className="mb-3 rounded p-2 text-sm text-accent-error" style={{ background: "var(--surface-elevated)" }}>
                    ⛔ Backend is temporarily unavailable.
                </div>
            )}

            {stats.state === "HALF_OPEN" && (
                <div className="mb-3 rounded p-2 text-sm text-accent-warning" style={{ background: "var(--surface-elevated)" }}>
                    ⚠️ Testing backend availability...
                </div>
            )}

            {stats.state !== "CLOSED" && (
                <button onClick={() => apiCircuitBreaker.reset()} className="w-full py-2 rounded-md text-white font-semibold" style={{ background: config.border }}>
                    Reset Circuit Breaker
                </button>
            )}
        </div>
    );
}

export default CircuitBreakerStatus;
