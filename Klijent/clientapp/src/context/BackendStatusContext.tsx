import { createContext, useEffect, useMemo, useRef, useState } from "react";
import { usePingControl } from "./PingControlContext";
import { fetchWithTimeout } from "../utils/fetchWithTimeout";
import {
    BACKEND_REACHABLE_EVENT,
    BACKEND_UNREACHABLE_EVENT,
    createBackendAvailabilityChannel,
    getLatestBackendAvailabilityPayload,
    type BackendAvailabilityStatus,
    type BackendReachabilityDetail,
    type BackendUnreachableDetail,
    notifyBackendReachable,
    notifyBackendUnreachable,
} from "./backendReachabilityEvents";
import { apiUrl } from "../utils/apiUrl";
import {
    API_HEALTH_FAILURE_GRACE_MS,
    API_HEALTH_TIMEOUT_MS,
} from "../utils/apiTimeouts";

export type BackendStatus = {
    status: BackendAvailabilityStatus;
    online: boolean;
    checking: boolean;
    lastCheckedAt: number | null;
    lastReachableAt: number | null;
    lastUnavailableAt: number | null;
    lastError: string | null;
    hadConfirmedOutage: boolean;
    recoveryNoticeVisible: boolean;
    recoveryNoticeAt: number | null;
};

export const BackendStatusContext = createContext<BackendStatus>({
    status: "unknown",
    online: true,
    checking: true,
    lastCheckedAt: null,
    lastReachableAt: null,
    lastUnavailableAt: null,
    lastError: null,
    hadConfirmedOutage: false,
    recoveryNoticeVisible: false,
    recoveryNoticeAt: null,
});

export const BackendStatusProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [status, setStatus] = useState<BackendAvailabilityStatus>("unknown");
    const [checking, setChecking] = useState(true);
    const [lastCheckedAt, setLastCheckedAt] = useState<number | null>(null);
    const [lastReachableAt, setLastReachableAt] = useState<number | null>(null);
    const [lastUnavailableAt, setLastUnavailableAt] = useState<number | null>(null);
    const [lastError, setLastError] = useState<string | null>(null);
    const [hadConfirmedOutage, setHadConfirmedOutage] = useState(false);
    const [recoveryNoticeVisible, setRecoveryNoticeVisible] = useState(false);
    const [recoveryNoticeAt, setRecoveryNoticeAt] = useState<number | null>(null);
    const statusRef = useRef<BackendAvailabilityStatus>("unknown");
    const lastReachableAtRef = useRef<number | null>(null);
    const hadConfirmedOutageRef = useRef(false);
    const recoveryHideTimerRef = useRef<number | null>(null);
    const { apiPingEnabled } = usePingControl();

    const ONLINE_POLL_INTERVAL_MS = import.meta.env.DEV ? 30_000 : 60_000;
    const OFFLINE_POLL_INTERVAL_MS = import.meta.env.DEV ? 4_000 : 6_000;

    useEffect(() => {
        statusRef.current = status;
    }, [status]);

    useEffect(() => {
        hadConfirmedOutageRef.current = hadConfirmedOutage;
    }, [hadConfirmedOutage]);

    useEffect(() => () => {
        if (recoveryHideTimerRef.current !== null) {
            window.clearTimeout(recoveryHideTimerRef.current);
        }
    }, []);

    useEffect(() => {
        const hideRecoveryNoticeLater = () => {
            if (recoveryHideTimerRef.current !== null) {
                window.clearTimeout(recoveryHideTimerRef.current);
            }

            recoveryHideTimerRef.current = window.setTimeout(() => {
                setRecoveryNoticeVisible(false);
                recoveryHideTimerRef.current = null;
            }, 1800);
        };

        const markReachable = (detail?: BackendReachabilityDetail) => {
            const checkedAt = detail?.checkedAt ?? Date.now();
            const previousStatus = statusRef.current;
            const shouldShowRecoveryNotice =
                hadConfirmedOutageRef.current && (previousStatus === "down" || previousStatus === "recovering");

            lastReachableAtRef.current = checkedAt;
            setStatus("up");
            setChecking(false);
            setLastCheckedAt(checkedAt);
            setLastReachableAt(checkedAt);
            setLastError(null);

            if (shouldShowRecoveryNotice) {
                hadConfirmedOutageRef.current = false;
                setRecoveryNoticeVisible(true);
                setRecoveryNoticeAt(checkedAt);
                setHadConfirmedOutage(false);
                hideRecoveryNoticeLater();
            }
        };

        const markUnreachable = (detail?: BackendUnreachableDetail) => {
            const checkedAt = detail?.checkedAt ?? Date.now();
            const lastReachable = lastReachableAtRef.current;
            const hasRecentSuccessfulRequest =
                lastReachable !== null && checkedAt - lastReachable < API_HEALTH_FAILURE_GRACE_MS;

            setLastCheckedAt(checkedAt);
            setLastUnavailableAt(checkedAt);

            if (hasRecentSuccessfulRequest) {
                return;
            }

            setStatus("down");
            setChecking(false);
            setLastError(detail?.message ?? (detail?.status ? `HTTP ${detail.status}` : detail?.reason ?? null));
            hadConfirmedOutageRef.current = true;
            if (recoveryHideTimerRef.current !== null) {
                window.clearTimeout(recoveryHideTimerRef.current);
                recoveryHideTimerRef.current = null;
            }
            setHadConfirmedOutage(true);
            setRecoveryNoticeVisible(false);
        };

        const handleReachable = (event: Event) => {
            markReachable((event as CustomEvent<BackendReachabilityDetail>).detail);
        };

        const handleUnreachable = (event: Event) => {
            markUnreachable((event as CustomEvent<BackendUnreachableDetail>).detail);
        };

        const latestSignal = getLatestBackendAvailabilityPayload();
        if (latestSignal?.type === "reachable") {
            markReachable(latestSignal.detail);
        } else if (latestSignal?.type === "unreachable") {
            markUnreachable(latestSignal.detail);
        }

        window.addEventListener(BACKEND_REACHABLE_EVENT, handleReachable as EventListener);
        window.addEventListener(BACKEND_UNREACHABLE_EVENT, handleUnreachable as EventListener);
        const channel = createBackendAvailabilityChannel();
        if (channel) {
            channel.onmessage = (event: MessageEvent) => {
                if (event.data?.type === "reachable") {
                    markReachable(event.data.detail as BackendReachabilityDetail);
                }
                if (event.data?.type === "unreachable") {
                    markUnreachable(event.data.detail as BackendUnreachableDetail);
                }
            };
        }

        return () => {
            window.removeEventListener(BACKEND_REACHABLE_EVENT, handleReachable as EventListener);
            window.removeEventListener(BACKEND_UNREACHABLE_EVENT, handleUnreachable as EventListener);
            channel?.close();
        };
    }, []);

    useEffect(() => {
        let cancelled = false;
        let timeoutId: number | null = null;

        const pingBackend = async () => {
            if (cancelled) return;

            setChecking(true);
            try {
                const healthUrl = apiUrl("/health");
                const res = await fetchWithTimeout(healthUrl, undefined, API_HEALTH_TIMEOUT_MS);
                if (cancelled) return;
                const checkedAt = Date.now();
                if (res.ok) {
                    notifyBackendReachable({
                        checkedAt,
                        source: "health",
                        status: res.status,
                        url: healthUrl,
                    });
                } else if (res.status >= 500) {
                    notifyBackendUnreachable({
                        checkedAt,
                        source: "health",
                        reason: "server-error",
                        status: res.status,
                        url: healthUrl,
                    });
                }
            } catch {
                if (cancelled) return;
                const checkedAt = Date.now();
                notifyBackendUnreachable({
                    checkedAt,
                    source: "health",
                    reason: "timeout",
                    message: "Health check failed",
                    url: apiUrl("/health"),
                });
            } finally {
                if (cancelled) return;
                if (statusRef.current !== "up") {
                    setChecking(false);
                }
            }
        };

        const scheduleNextPing = () => {
            if (cancelled || !apiPingEnabled) {
                return;
            }

            const delay = statusRef.current === "down" || statusRef.current === "recovering"
                ? OFFLINE_POLL_INTERVAL_MS
                : ONLINE_POLL_INTERVAL_MS;
            timeoutId = window.setTimeout(() => {
                void run();
            }, delay);
        };

        const run = async () => {
            await pingBackend();
            scheduleNextPing();
        };

        void run();

        const runWhenVisible = () => {
            if (!document.hidden && (statusRef.current === "down" || statusRef.current === "unknown")) {
                void run();
            }
        };

        const runWhenOnline = () => {
            if (statusRef.current === "down" || statusRef.current === "unknown") {
                void run();
            }
        };

        window.addEventListener("online", runWhenOnline);
        document.addEventListener("visibilitychange", runWhenVisible);

        return () => {
            cancelled = true;
            if (timeoutId !== null) {
                window.clearTimeout(timeoutId);
            }
            window.removeEventListener("online", runWhenOnline);
            document.removeEventListener("visibilitychange", runWhenVisible);
        };
    }, [OFFLINE_POLL_INTERVAL_MS, ONLINE_POLL_INTERVAL_MS, apiPingEnabled]);

    const online = status === "up" || status === "unknown";
    const value = useMemo<BackendStatus>(
        () => ({
            status,
            online,
            checking,
            lastCheckedAt,
            lastReachableAt,
            lastUnavailableAt,
            lastError,
            hadConfirmedOutage,
            recoveryNoticeVisible,
            recoveryNoticeAt,
        }),
        [
            checking,
            hadConfirmedOutage,
            lastCheckedAt,
            lastError,
            lastReachableAt,
            lastUnavailableAt,
            online,
            recoveryNoticeAt,
            recoveryNoticeVisible,
            status,
        ]
    );

    return (
        <BackendStatusContext.Provider value={value}>
            {children}
        </BackendStatusContext.Provider>
    );
};
