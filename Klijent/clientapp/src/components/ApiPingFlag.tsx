import { usePingControl } from "../context/PingControlContext";

export default function ApiPingFlag() {
  const { apiPingEnabled, toggleApiPing } = usePingControl();

  return (
    <div className="flex items-center gap-2">
      <span
        className={`px-3 py-1 rounded-full text-[10px] font-bold uppercase transition-colors border ${
          apiPingEnabled 
            ? "bg-success/10 border-success text-success" 
            : "bg-surface-darker border-muted text-muted"
        }`}
        title="Kontrola periodicnog pingovanja API-ja iz frontenda"
      >
        API ping: {apiPingEnabled ? "ON" : "OFF"}
      </span>
      <button
        type="button"
        onClick={toggleApiPing}
        className="px-2 py-1 rounded-lg border border-muted bg-surface-elevated text-contrast text-[10px] font-semibold hover:bg-surface-darker transition-colors"
      >
        {apiPingEnabled ? "Stop" : "Start"}
      </button>
    </div>
  );
}

