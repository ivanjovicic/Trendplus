import React, { createContext, useContext, useMemo, useState } from "react";

type PingControlContextValue = {
  apiPingEnabled: boolean;
  setApiPingEnabled: (enabled: boolean) => void;
  toggleApiPing: () => void;
};

const STORAGE_KEY = "trendplus:api-ping-enabled";

const PingControlContext = createContext<PingControlContextValue>({
  apiPingEnabled: true,
  setApiPingEnabled: () => {},
  toggleApiPing: () => {},
});

export const PingControlProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [apiPingEnabled, setApiPingEnabledState] = useState<boolean>(() => {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw == null ? true : raw === "1";
    } catch {
      return true;
    }
  });

  const setApiPingEnabled = (enabled: boolean) => {
    setApiPingEnabledState(enabled);
    try {
      localStorage.setItem(STORAGE_KEY, enabled ? "1" : "0");
    } catch {
      // no-op
    }
  };

  const value = useMemo<PingControlContextValue>(
    () => ({
      apiPingEnabled,
      setApiPingEnabled,
      toggleApiPing: () => setApiPingEnabled(!apiPingEnabled),
    }),
    [apiPingEnabled]
  );

  return <PingControlContext.Provider value={value}>{children}</PingControlContext.Provider>;
};

export function usePingControl(): PingControlContextValue {
  return useContext(PingControlContext);
}

