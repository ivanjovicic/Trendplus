import { createContext, useEffect, useState } from "react";
import { usePingControl } from "./PingControlContext";
import { fetchWithTimeout } from "../utils/fetchWithTimeout";

export type BackendStatus = {
    online: boolean;
};

export const BackendStatusContext = createContext<BackendStatus>({ online: true });

export const BackendStatusProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [online, setOnline] = useState(true);
    const { apiPingEnabled } = usePingControl();
    const HEALTH_POLL_INTERVAL_MS = import.meta.env.DEV ? 30000 : 120000;
    
    useEffect(() => {
        if (!apiPingEnabled) {
            return;
        }

        const pingBackend = async () => {
            try {
                // Use /health endpoint (proxied in dev, direct in prod)
                const url = import.meta.env.DEV 
                    ? "/health"  // Proxied to localhost:8080
                    : `${import.meta.env.VITE_API_BASE_URL}/health`;  // Direct to Render
                
                const res = await fetchWithTimeout(url, undefined, 10_000);
                setOnline(res.ok);
            } catch {
                setOnline(false);
            }
        };

        pingBackend();
        const interval = setInterval(pingBackend, HEALTH_POLL_INTERVAL_MS);
        return () => clearInterval(interval);
    }, [HEALTH_POLL_INTERVAL_MS, apiPingEnabled]);

    return (
        <BackendStatusContext.Provider value={{ online }}>
            {children}
        </BackendStatusContext.Provider>
    );
};
