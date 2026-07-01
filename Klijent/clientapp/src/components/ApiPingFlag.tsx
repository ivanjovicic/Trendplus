import { RadioTower } from "lucide-react";
import { usePingControl } from "../context/PingControlContext";

export default function ApiPingFlag() {
  const { apiPingEnabled, toggleApiPing } = usePingControl();

  return (
    <div className="inline-flex shrink-0 items-center gap-1 rounded-2xl border border-muted bg-[var(--surface-light)] px-1.5 py-1">
      <span
        className={`inline-flex items-center gap-1.5 rounded-xl border px-2 py-1 text-[11px] font-bold uppercase tracking-wide transition-colors ${
          apiPingEnabled
            ? "border-[var(--success)]/50 bg-success-soft text-[var(--success)]"
            : "border-muted bg-[var(--surface-darker)] text-muted"
        }`}
        title="Kontrola periodičnog pingovanja API-ja iz frontenda"
      >
        <RadioTower size={12} />
        API {apiPingEnabled ? "ON" : "OFF"}
      </span>
      <button
        type="button"
        onClick={toggleApiPing}
        className="rounded-xl border border-muted bg-[var(--surface-elevated)] px-2 py-1 text-[11px] font-semibold text-contrast transition-colors hover:border-[var(--info)] hover:bg-[var(--surface-darker)]"
      >
        {apiPingEnabled ? "Stop" : "Start"}
      </button>
    </div>
  );
}
