export const TONE = {
  severity: {
    critical: "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
    warning: "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
    info: "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
  },
  urgency: {
    urgent: "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
    recommended: "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
    optional: "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
  },
  aging: {
    "0-30": "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
    "31-60": "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
    "61-90": "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
    "90+": "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
  },
  abc: {
    A: "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
    B: "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
    C: "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
  },
  stock: {
    critical: "bg-[var(--surface-elevated)] text-[var(--text-primary)] border-[var(--border-default)]",
    warning: "bg-[var(--surface-elevated)] text-[var(--text-primary)] border-[var(--border-default)]",
    healthy: "bg-[var(--surface-elevated)] text-[var(--text-primary)] border-[var(--border-default)]",
  },
  stockPanel: {
    critical: "from-[var(--surface-elevated)] to-[var(--surface-elevated-dark)]",
    warning: "from-[var(--surface-elevated)] to-[var(--surface-elevated-dark)]",
    healthy: "from-[var(--surface-elevated)] to-[var(--surface-elevated-dark)]",
  },
  actionType: {
    dopuna: "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
    transfer: "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
    markdown: "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
    clearance: "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
  },
  actionStatus: {
    pending: "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
    approved: "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
    deferred: "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
    closed: "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]",
  },
  priority: {
    critical: "text-[var(--text-primary)]",
    high: "text-[var(--text-primary)]",
    medium: "text-[var(--text-primary)]",
    low: "text-[var(--text-primary)]",
  },
} as const;

export function resolveTone(map: Record<string, string>, key: string | null | undefined, fallback: string) {
  return (key && map[key]) || fallback;
}

