import { RadioTower } from "lucide-react";
import { usePingControl } from "../context/PingControlContext";

export default function ApiPingFlag() {
  const { apiPingEnabled, toggleApiPing } = usePingControl();

  return (
    <div className="inline-flex flex-wrap items-center gap-2 rounded-2xl border border-muted bg-[var(--surface-light)] px-3 py-2">
      <span
        className={`inline-flex items-center gap-1.5 rounded-xl border px-2 py-1 text-xs font-semibold transition-colors ${
          apiPingEnabled
            ? "border-[var(--success)]/50 bg-success-soft text-[var(--success)]"
            : "border-muted bg-[var(--surface-darker)] text-muted"
        }`}
        title="Ova postavka utiče samo na periodične provere iz ovog pregledača; ne zaustavlja API servis."
      >
        <RadioTower size={12} />
        Provera API-ja u ovom pregledaču: {apiPingEnabled ? "aktivna" : "pauzirana"}
      </span>
      <button
        type="button"
        onClick={toggleApiPing}
        aria-label={apiPingEnabled ? "Pauziraj proveru API-ja u ovom pregledaču" : "Nastavi proveru API-ja u ovom pregledaču"}
        className="rounded-xl border border-muted bg-[var(--surface-elevated)] px-3 py-2 text-sm font-semibold text-contrast transition-colors hover:border-[var(--info)] hover:bg-[var(--surface-darker)]"
      >
        {apiPingEnabled ? "Pauziraj proveru" : "Nastavi proveru"}
      </button>
    </div>
  );
}
