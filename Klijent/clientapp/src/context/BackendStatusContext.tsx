import { createContext, useEffect, useMemo, useRef, useState } from "react";
import { usePingControl } from "./PingControlContext";
import { fetchWithTimeout } from "../utils/fetchWithTimeout";
import {
    BACKEND_REACHABLE_EVENT,
    type BackendReachabilityDetail,
    notifyBackendReachable,
} from "./backendReachabilityEvents";
import { apiUrl } from "../utils/apiUrl";

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
    const lastReachableAtRef = useRef<number | null>(null);
    const { apiPingEnabled } = usePingControl();

    const ONLINE_POLL_INTERVAL_MS = import.meta.env.DEV ? 30_000 : 60_000;
    const OFFLINE_POLL_INTERVAL_MS = import.meta.env.DEV ? 4_000 : 6_000;
    const HEALTH_TIMEOUT_MS = import.meta.env.DEV ? 5_000 : 8_000;
    const HEALTH_FAILURE_GRACE_MS = import.meta.env.DEV ? 15_000 : 45_000;

    useEffect(() => {
        onlineRef.current = online;
    }, [online]);

    useEffect(() => {
        const handleReachable = (event: Event) => {
            const detail = (event as CustomEvent<BackendReachabilityDetail>).detail;
            const checkedAt = detail?.checkedAt ?? Date.now();
            lastReachableAtRef.current = checkedAt;
            setOnline(true);
            setChecking(false);
            setLastCheckedAt(checkedAt);
        };

        window.addEventListener(BACKEND_REACHABLE_EVENT, handleReachable as EventListener);

        return () => {
            window.removeEventListener(BACKEND_REACHABLE_EVENT, handleReachable as EventListener);
        };
    }, []);

    useEffect(() => {
        let cancelled = false;
        let timeoutId: number | null = null;

        const healthUrl = apiUrl("/health");

        const pingBackend = async () => {
            if (cancelled) return;

            setChecking(true);
            try {
                const res = await fetchWithTimeout(healthUrl, undefined, HEALTH_TIMEOUT_MS);
                if (cancelled) return;
                const checkedAt = Date.now();
                notifyBackendReachable({
                    checkedAt,
                    source: "health",
                    status: res.status,
                    url: healthUrl,
                });
                lastReachableAtRef.current = checkedAt;
                setOnline(true);
                setLastCheckedAt(checkedAt);
            } catch {
                if (cancelled) return;
                const checkedAt = Date.now();
                const lastReachableAt = lastReachableAtRef.current;
                const hasRecentSuccessfulRequest =
                    lastReachableAt !== null && checkedAt - lastReachableAt < HEALTH_FAILURE_GRACE_MS;

                if (!hasRecentSuccessfulRequest) {
                    setOnline(false);
                }

                setLastCheckedAt(checkedAt);
            } finally {
                if (cancelled) return;
                setChecking(false);
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
