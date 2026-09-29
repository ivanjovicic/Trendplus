type AnalyticsFilterLoadNoticeProps = {
  onRetry: () => void;
  message?: string;
  stale?: boolean;
};

export default function AnalyticsFilterLoadNotice({ onRetry, message, stale = false }: AnalyticsFilterLoadNoticeProps) {
  return (
    <div
      role="alert"
      aria-live="polite"
      className="mb-4 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-amber-500/40 bg-amber-500/10 px-4 py-3 text-sm text-amber-100"
    >
      <p>
        <strong>{stale ? "Lista prodavnica je zastarela." : "Filter prodavnice nije dostupan."}</strong>{" "}
        {message ?? "Podaci po svim objektima mogu biti prikazani, ali izbor pojedinačne prodavnice nije potvrđen."}
      </p>
      <button
        type="button"
        onClick={onRetry}
        className="rounded-lg border border-amber-400/60 px-3 py-1.5 font-semibold text-amber-100 transition hover:bg-amber-400/20"
      >
        Pokušaj ponovo
      </button>
    </div>
  );
}
