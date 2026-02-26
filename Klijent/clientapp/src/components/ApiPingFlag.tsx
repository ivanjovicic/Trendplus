import { usePingControl } from "../context/PingControlContext";

export default function ApiPingFlag() {
  const { apiPingEnabled, toggleApiPing } = usePingControl();

  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
      <span
        style={{
          padding: "4px 8px",
          borderRadius: 999,
          border: `1px solid ${apiPingEnabled ? "#34d399" : "#9ca3af"}`,
          background: apiPingEnabled ? "#064e3b" : "#1f2937",
          color: apiPingEnabled ? "#d1fae5" : "#e5e7eb",
          fontSize: 12,
          fontWeight: 700,
          whiteSpace: "nowrap",
        }}
        title="Kontrola periodicnog pingovanja API-ja iz frontenda"
      >
        API ping: {apiPingEnabled ? "UKLJUCEN" : "ISKLJUCEN"}
      </span>
      <button
        type="button"
        onClick={toggleApiPing}
        style={{
          padding: "5px 10px",
          borderRadius: 8,
          border: "1px solid #4b5563",
          background: "#1f2937",
          color: "white",
          fontSize: 12,
          fontWeight: 600,
          cursor: "pointer",
        }}
      >
        {apiPingEnabled ? "Stop ping" : "Start ping"}
      </button>
    </div>
  );
}

