import type { LucideIcon } from "lucide-react";

type Tone = "neutral" | "positive" | "warning" | "danger";

export function InventoryPageShell({
  title,
  subtitle,
  icon: Icon,
  children,
  actions,
}: {
  title: string;
  subtitle?: string;
  icon: LucideIcon;
  children: React.ReactNode;
  actions?: React.ReactNode;
}) {
  return (
    <div className="space-y-4">
      <section className="rounded-2xl border border-[var(--border-default)] bg-gradient-to-br from-[var(--surface-elevated)] via-[var(--surface-elevated-light)] to-[var(--surface-elevated-dark)] p-4 shadow-[0_16px_40px_-30px_var(--card-shadow)] sm:p-5">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <span className="rounded-lg border border-[var(--border-default)] bg-[var(--surface-elevated)] p-2 text-[var(--text-primary)]">
                <Icon size={16} />
              </span>
              <h1 className="text-lg font-semibold text-[var(--text-primary)] sm:text-xl">{title}</h1>
            </div>
            {subtitle ? <p className="mt-2 max-w-3xl text-sm text-[var(--text-primary)]">{subtitle}</p> : null}
          </div>
          {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
        </div>
      </section>
      {children}
    </div>
  );
}

export function InventoryKpiRow({
  items,
}: {
  items: Array<{ label: string; value: string; tone?: Tone }>;
}) {
  return (
    <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
      {items.map((item) => {
        const toneClass =
          item.tone === "positive"
            ? "text-emerald-300"
            : item.tone === "warning"
            ? "text-amber-300"
            : item.tone === "danger"
            ? "text-rose-300"
            : "text-[var(--text-primary)]";

        return (
          <article key={item.label} className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4">
            <p className="text-xs uppercase tracking-wide text-[var(--text-primary)]">{item.label}</p>
            <p className={`mt-2 text-xl font-semibold ${toneClass}`}>{item.value}</p>
          </article>
        );
      })}
    </section>
  );
}

export function InventoryPanel({ children, className = "" }: { children: React.ReactNode; className?: string }) {
  return <section className={`rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4 sm:p-5 ${className}`.trim()}>{children}</section>;
}

export function InventoryState({
  message,
  tone = "neutral",
}: {
  message: string;
  tone?: Tone;
}) {
  const toneClass =
    tone === "positive"
      ? "text-emerald-300"
      : tone === "warning"
      ? "text-amber-300"
      : tone === "danger"
      ? "text-rose-300"
      : "text-[var(--text-primary)]";

  return (
    <div className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-8 text-center">
      <p className={`text-sm ${toneClass}`}>{message}</p>
    </div>
  );
}

