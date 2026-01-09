import { createContext, useEffect, useState } from "react";

export type BackendStatus = {
    online: boolean;
};

export const BackendStatusContext = createContext<BackendStatus>({ online: true });

export const BackendStatusProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [online, setOnline] = useState(true);
    
    useEffect(() => {
        const pingBackend = async () => {
            try {
                // Use /health endpoint (proxied in dev, direct in prod)
                const url = import.meta.env.DEV 
                    ? "/health"  // Proxied to localhost:8080
                    : `${import.meta.env.VITE_API_BASE_URL}/health`;  // Direct to Render
                
                const res = await fetch(url);
                setOnline(res.ok);
            } catch {
                setOnline(false);
            }
        };

        pingBackend();
        const interval = setInterval(pingBackend, 30000);
        return () => clearInterval(interval);
    }, []);

    return (
        <BackendStatusContext.Provider value={{ online }}>
            {children}
        </BackendStatusContext.Provider>
    );
};
