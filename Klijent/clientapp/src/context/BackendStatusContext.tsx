import { createContext, useEffect, useState } from "react";

export type BackendStatus = {
    online: boolean;
};

export const BackendStatusContext = createContext<BackendStatus>({ online: true });

export const BackendStatusProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [online, setOnline] = useState(true);
    const API = import.meta.env.VITE_API_BASE_URL;
    useEffect(() => {
        const pingBackend = async () => {
            try {
                const res = await fetch(`${API}/health`);
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
