import { createContext, useEffect, useMemo, useRef, useState } from "react";
import { usePingControl } from "./PingControlContext";
import { fetchWithTimeout } from "../utils/fetchWithTimeout";

export type BackendStatus = {
    online: boolean;
    checking: boolean;
    lastCheckedAt: number | null;
};

export const BackendStatusContext = createContext<BackendStatus>({
    online: true,
    checking: true,
    lastCheckedAt: null,
});

export const BackendStatusProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [online, setOnline] = useState(true);
    const [checking, setChecking] = useState(true);
    const [lastCheckedAt, setLastCheckedAt] = useState<number | null>(null);
    const onlineRef = useRef(true);
    const { apiPingEnabled } = usePingControl();

    const ONLINE_POLL_INTERVAL_MS = import.meta.env.DEV ? 30_000 : 60_000;
    const OFFLINE_POLL_INTERVAL_MS = import.meta.env.DEV ? 8_000 : 12_000;
    const HEALTH_TIMEOUT_MS = import.meta.env.DEV ? 8_000 : 12_000;

    useEffect(() => {
        onlineRef.current = online;
    }, [online]);

    useEffect(() => {
        let cancelled = false;
        let timeoutId: number | null = null;

        const apiBase = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/+$/, "");
        const healthUrl = import.meta.env.DEV || !apiBase ? "/health" : `${apiBase}/health`;

        const pingBackend = async () => {
            if (cancelled) return;

            setChecking(true);
            try {
                const res = await fetchWithTimeout(healthUrl, undefined, HEALTH_TIMEOUT_MS);
                if (cancelled) return;
                setOnline(res.ok);
            } catch {
                if (cancelled) return;
                setOnline(false);
            } finally {
                if (cancelled) return;
                setChecking(false);
                setLastCheckedAt(Date.now());
            }
        };

        const scheduleNextPing = () => {
            if (cancelled || !apiPingEnabled) {
                return;
            }

            const delay = onlineRef.current ? ONLINE_POLL_INTERVAL_MS : OFFLINE_POLL_INTERVAL_MS;
            timeoutId = window.setTimeout(() => {
                void run();
            }, delay);
        };

        const run = async () => {
            await pingBackend();
            scheduleNextPing();
        };

        void run();

        return () => {
            cancelled = true;
            if (timeoutId !== null) {
                window.clearTimeout(timeoutId);
            }
        };
    }, [OFFLINE_POLL_INTERVAL_MS, ONLINE_POLL_INTERVAL_MS, HEALTH_TIMEOUT_MS, apiPingEnabled]);

    const value = useMemo<BackendStatus>(
        () => ({
            online,
            checking,
            lastCheckedAt,
        }),
        [checking, lastCheckedAt, online]
    );

    return (
        <BackendStatusContext.Provider value={value}>
            {children}
        </BackendStatusContext.Provider>
    );
};
