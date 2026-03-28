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
      <section
        className="rounded-2xl border p-4 sm:p-5"
        style={{
          background: "linear-gradient(135deg, var(--surface-elevated, #ffffff) 0%, var(--surface-elevated-light, #ffffff) 50%, var(--surface-elevated-dark, #ffffff) 100%)",
          borderColor: "var(--border-default, #d3dce9)",
          boxShadow: "0 16px 40px -30px var(--card-shadow, rgba(16,24,40,0.06))",
        }}
      >
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <span
                className="rounded-lg p-2"
                style={{
                  border: "1px solid var(--border-default, #d3dce9)",
                  background: "var(--surface-elevated, #ffffff)",
                  color: "var(--text-primary, #0f172a)",
                }}
              >
                <Icon size={16} />
              </span>
              <h1 className="text-lg font-semibold sm:text-xl" style={{ color: "var(--text-primary, #0f172a)" }}>{title}</h1>
            </div>
            {subtitle ? (
              <p className="mt-2 max-w-3xl text-sm" style={{ color: "var(--text-primary, #0f172a)" }}>{subtitle}</p>
            ) : null}
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

