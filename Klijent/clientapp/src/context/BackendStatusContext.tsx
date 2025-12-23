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
                const res = await fetch("http://localhost:5230/health");
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
