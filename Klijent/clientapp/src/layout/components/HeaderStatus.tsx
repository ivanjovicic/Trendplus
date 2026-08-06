import { useContext, useEffect, useMemo, useRef, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import {
  Activity,
  Bell,
  ChevronRight,
  Command,
  Database,
  LayoutGrid,
  Menu,
  RefreshCw,
  Search,
  Server,
  Settings,
  Sparkles,
  Store,
  UserRound,
  X,
} from "lucide-react";
import ApiPingFlag from "../../components/ApiPingFlag";
import WorkerControlFlag from "../../components/WorkerControlFlag";
import RedisToggleFlag from "../../components/RedisToggleFlag";
import { BackendStatusContext } from "../../context/BackendStatusContext";
import { getDataScope, setDataScope, type DataScope } from "../../utils/dataScope";
import { getHeaderRouteCommands, resolveHeaderNavigation, type HeaderTrailEntry } from "./headerNavigation";
import type { LucideIcon } from "lucide-react";

type HeaderStatusProps = {
  onOpenMobileNav: () => void;
};

type HeaderPanelMode = "commands" | "inbox" | "context";

type HeaderLauncherEntry =
  | {
      key: string;
      kind: "route";
      groupLabel: string;
      label: string;
      description: string;
      to: string;
      icon: LucideIcon;
    }
  | {
      key: string;
      kind: "action";
      groupLabel: string;
      label: string;
      description: string;
      onSelect: () => void;
      icon: LucideIcon;
    };

type HeaderInboxEntry = {
  key: string;
  tone: "info" | "warning" | "critical";
  title: string;
  detail: string;
  actionLabel: string;
  to?: string;
  onSelect?: () => void;
};

function dataScopeLabel(value: DataScope): string {
  if (value === "existing") return "Postojeći";
  if (value === "imported") return "Importovani";
  return "Sve";
}

function scopeTone(value: DataScope): string {
  if (value === "all") return "border-[var(--info)]/40 bg-[var(--info)]/10 text-[var(--info)]";
  if (value === "existing") return "border-[var(--success)]/40 bg-success-soft text-[var(--success)]";
  return "border-[var(--warning)]/40 bg-warning-soft text-[var(--warning)]";
}

function trailToNodes(trail: HeaderTrailEntry[]) {
  return trail.map((entry, index) => {
    const isLast = index === trail.length - 1;
    const isClickable = index === 1 && !!entry.to;

    const content = isClickable ? (
      <Link key={`${entry.label}-${index}`} to={entry.to ?? "/"} className="truncate transition hover:text-contrast">
        {entry.label}
      </Link>
    ) : (
      <span key={`${entry.label}-${index}`} className={`truncate ${isLast ? "text-contrast" : "text-muted"}`}>
        {entry.label}
      </span>
    );

    return (
      <span key={`${entry.label}-${index}`} className="inline-flex min-w-0 items-center gap-1.5">
        {index > 0 ? <ChevronRight size={12} className="shrink-0 text-muted" /> : null}
        {content}
      </span>
    );
  });
}

export default function HeaderStatus({ onOpenMobileNav }: HeaderStatusProps) {
  const { online, checking, lastCheckedAt } = useContext(BackendStatusContext);
  const location = useLocation();
  const [refreshing, setRefreshing] = useState(false);
  const [dataScopeValue, setDataScopeValue] = useState<DataScope>(getDataScope());
  const [panelMode, setPanelMode] = useState<HeaderPanelMode | null>(null);
  const [commandQuery, setCommandQuery] = useState("");
  const commandInputRef = useRef<HTMLInputElement | null>(null);

  const { group, item, trail } = useMemo(
    () => resolveHeaderNavigation(location.pathname),
    [location.pathname],
  );

  useEffect(() => {
    if (panelMode !== "commands") {
      return;
    }

    const frame = window.requestAnimationFrame(() => {
      commandInputRef.current?.focus();
    });

    return () => window.cancelAnimationFrame(frame);
  }, [panelMode]);

  const isInitialProbe = checking && lastCheckedAt === null;
  const isRecovering = checking && !online;
  const backendTone = isInitialProbe || isRecovering
    ? "border-[var(--warning)]/50 bg-warning-soft text-[var(--warning)]"
    : online
    ? "border-[var(--success)]/50 bg-success-soft text-[var(--success)]"
    : "border-[var(--error)]/50 bg-error-soft text-[var(--error)]";
  const backendDot = isInitialProbe || isRecovering
    ? "bg-[var(--warning)]"
    : online
    ? "bg-[var(--success)]"
    : "bg-[var(--error)]";
  const backendLabel = isInitialProbe ? "Provera" : isRecovering ? "Budi se" : online ? "Online" : "Offline";

  const refreshAll = () => {
    setRefreshing(true);
    window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    setTimeout(() => {
      setRefreshing(false);
      window.location.reload();
    }, 450);
  };

  const onScopeChange = (next: DataScope) => {
    setDataScopeValue(next);
    setDataScope(next);
    window.dispatchEvent(new Event("trendplus:data-scope-changed"));
  };

  const routeCommands = useMemo(() => getHeaderRouteCommands(), []);

  const launcherEntries = useMemo<HeaderLauncherEntry[]>(() => {
    const utilityEntries: HeaderLauncherEntry[] = [
      {
        key: "utility:refresh",
        kind: "action",
        groupLabel: "Aplikacija",
        label: "Osveži aplikaciju",
        description: "Ponovo učitaj trenutni prikaz i osveži signal podataka.",
        onSelect: refreshAll,
        icon: RefreshCw,
      },
      {
        key: "utility:scope-all",
        kind: "action",
        groupLabel: "Prikaz",
        label: "Prikaz: Sve",
        description: "Prebaci na sve podatke.",
        onSelect: () => onScopeChange("all"),
        icon: LayoutGrid,
      },
      {
        key: "utility:scope-existing",
        kind: "action",
        groupLabel: "Prikaz",
        label: "Prikaz: Postojeći",
        description: "Prebaci na postojeće podatke.",
        onSelect: () => onScopeChange("existing"),
        icon: LayoutGrid,
      },
      {
        key: "utility:scope-imported",
        kind: "action",
        groupLabel: "Prikaz",
        label: "Prikaz: Importovani",
        description: "Prebaci na uvozne podatke.",
        onSelect: () => onScopeChange("imported"),
        icon: LayoutGrid,
      },
      {
        key: "utility:themes",
        kind: "route",
        groupLabel: "Podešavanja",
        label: "Teme",
        description: "Otvori podešavanje tema i vizuelnog režima.",
        to: "/settings/themes",
        icon: Settings,
      },
      {
        key: "utility:workers",
        kind: "route",
        groupLabel: "Nadzor",
        label: "Worker panel",
        description: "Otvori worker kontrole i stanje procesa.",
        to: "/admin/configuration?panel=workers",
        icon: Command,
      },
      {
        key: "utility:quality",
        kind: "route",
        groupLabel: "Analitika",
        label: "Kvalitet podataka",
        description: "Otvori data quality pregled i upozorenja.",
        to: "/analytics/data-quality",
        icon: Bell,
      },
      {
        key: "utility:readiness",
        kind: "route",
        groupLabel: "Analitika",
        label: "Pilot spremnost",
        description: "Otvori readiness signal i status osvežavanja.",
        to: "/analytics/pilot-readiness",
        icon: Sparkles,
      },
    ];

    return [
      ...utilityEntries,
      ...routeCommands.map((entry): HeaderLauncherEntry => ({
        key: entry.key,
        kind: "route",
        groupLabel: entry.groupLabel,
        label: entry.label,
        description: entry.description,
        to: entry.to,
        icon: entry.icon,
      })),
    ];
  }, [onScopeChange, refreshAll, routeCommands]);

  const inboxEntries = useMemo<HeaderInboxEntry[]>(() => {
    const entries: HeaderInboxEntry[] = [];

    if (isInitialProbe) {
      entries.push({
        key: "backend:initial-probe",
        tone: "warning",
        title: "Backend se proverava",
        detail: "Još nema potvrđenog signala o dostupnosti, pa readiness ostaje oprezan.",
        actionLabel: "Ponovo proveri",
        onSelect: refreshAll,
      });
    } else if (isRecovering) {
      entries.push({
        key: "backend:recovering",
        tone: "warning",
        title: "Backend se budi",
        detail: "Trenutni odgovor je u prelaznom stanju i ne treba ga čitati kao stabilno zeleno.",
        actionLabel: "Osveži status",
        onSelect: refreshAll,
      });
    } else if (!online) {
      entries.push({
        key: "backend:offline",
        tone: "critical",
        title: "Backend nije dostupan",
        detail: "Kontrole i analytics signali zavise od ponovnog uspostavljanja veze.",
        actionLabel: "Osveži",
        onSelect: refreshAll,
      });
    }

    if (lastCheckedAt !== null && online) {
      entries.push({
        key: "backend:healthy",
        tone: "info",
        title: "Backend je potvrđen",
        detail: `Poslednja provera je zabeležena u ${new Date(lastCheckedAt).toLocaleTimeString("sr-RS")}.`,
        actionLabel: "Otvori readiness",
        to: "/analytics/pilot-readiness",
      });
    }

    if (dataScopeValue !== "all") {
      entries.push({
        key: "scope:limited",
        tone: "info",
        title: "Prikaz je sužen",
        detail: `Trenutni prikaz je ${dataScopeLabel(dataScopeValue)}. To je namerno, ali nije isto što i puni skup podataka.`,
        actionLabel: "Vrati na sve",
        onSelect: () => onScopeChange("all"),
      });
    }

    if (group.id.startsWith("analytics-")) {
      entries.push({
        key: "analytics:quality",
        tone: "info",
        title: "Analitički signalni centar",
        detail: "Data quality i readiness ostaju najbrži put do provere da li je trenutni pilot bezbedan za odluku.",
        actionLabel: "Otvori data quality",
        to: "/analytics/data-quality",
      });
    }

    entries.push({
      key: "system:controls",
      tone: "info",
      title: "Sistemske kontrole su dostupne",
      detail: "API ping, worker i Redis kontrole ostaju u traci iznad, bez promena ponašanja.",
      actionLabel: "Otvori komande",
      onSelect: () => setPanelMode("commands"),
    });

    return entries;
  }, [dataScopeValue, group.id, isInitialProbe, isRecovering, lastCheckedAt, online, refreshAll, onScopeChange]);

  const filteredLauncherEntries = useMemo(() => {
    const query = commandQuery.trim().toLowerCase();
    if (!query) {
      return launcherEntries;
    }

    return launcherEntries.filter((entry) => {
      const haystack = `${entry.label} ${entry.description} ${entry.groupLabel}`.toLowerCase();
      return haystack.includes(query);
    });
  }, [commandQuery, launcherEntries]);

  const panelTitle = panelMode === "inbox"
    ? "Obaveštenja"
    : panelMode === "context"
    ? "Kontekst"
    : "Komande";

  const panelSubtitle = panelMode === "inbox"
    ? "Aktivni signali i akcije koje treba imati na oku."
    : panelMode === "context"
    ? "Povezani prikaz, nalog i prodavnica, bez izmišljanja podataka."
    : "Brze veze, akcije i prečice za trenutni deo aplikacije.";

  const openPanel = (mode: HeaderPanelMode) => {
    setPanelMode((current) => (current === mode ? null : mode));
    if (mode === "commands") {
      setCommandQuery("");
    }
  };

  const closePanel = () => {
    setPanelMode(null);
  };

  const alertBadgeTone = inboxEntries.some((entry) => entry.tone === "critical")
    ? "border-[var(--error)]/50 bg-error-soft text-[var(--error)]"
    : inboxEntries.some((entry) => entry.tone === "warning")
    ? "border-[var(--warning)]/50 bg-warning-soft text-[var(--warning)]"
    : "border-[var(--info)]/40 bg-[var(--info)]/10 text-[var(--info)]";

  return (
    <header className="sticky top-0 relative z-30 border-b border-muted bg-[var(--surface-default)]/95 px-4 py-3 shadow-[0_18px_42px_-38px_rgba(0,0,0,0.85)] backdrop-blur-xl">
      <div className="flex flex-wrap items-center gap-3">
        <button
          type="button"
          onClick={onOpenMobileNav}
          className="rounded-xl border border-muted bg-[var(--surface-elevated)] p-2 text-secondary transition hover:border-[var(--info)] hover:text-contrast lg:hidden"
          aria-label="Otvori navigaciju"
        >
          <Menu size={17} />
        </button>

        <div className="flex min-w-0 flex-1 items-center gap-3">
          <div className="hidden h-10 w-10 shrink-0 items-center justify-center rounded-2xl border border-[var(--info)]/40 bg-[var(--info)]/10 text-[var(--info)] sm:flex">
            <Sparkles size={18} />
          </div>
          <div className="min-w-0">
            <div className="flex min-w-0 flex-wrap items-center gap-x-1.5 gap-y-1 text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">
              {trailToNodes(trail)}
            </div>
            <div className="mt-0.5 flex min-w-0 items-center gap-2">
              <h1 className="truncate text-sm font-semibold text-contrast sm:text-base">
                {item?.label ?? "Trendplus Backoffice"}
              </h1>
              <span className="hidden rounded-full border border-muted bg-[var(--surface-elevated)] px-2 py-0.5 text-[11px] font-semibold text-muted md:inline-flex">
                Premium workspace
              </span>
            </div>
          </div>
        </div>

        <div className="flex max-w-full flex-wrap items-center gap-2 rounded-2xl border border-muted bg-[var(--surface-elevated)]/80 px-2 py-1.5 shadow-[0_14px_32px_-28px_rgba(0,0,0,0.9)]">
          <div
            className={`inline-flex items-center gap-2 rounded-xl border px-2.5 py-1.5 text-xs font-semibold ${backendTone}`}
            title={lastCheckedAt ? `Poslednja provera: ${new Date(lastCheckedAt).toLocaleTimeString("sr-RS")}` : "Backend status"}
          >
            <span className={`h-2 w-2 rounded-full ${backendDot}`} />
            <Server size={14} />
            <span className="hidden uppercase tracking-wide text-muted sm:inline">Backend</span>
            <span>{backendLabel}</span>
          </div>

          <div className="hidden items-center gap-2 xl:flex">
            <ApiPingFlag />
            <WorkerControlFlag />
            <RedisToggleFlag />
          </div>
        </div>

        <div className="ml-auto flex flex-wrap items-center justify-end gap-2">
          <button
            type="button"
            onClick={() => openPanel("commands")}
            className={`inline-flex items-center gap-1.5 rounded-2xl border px-3 py-2 text-xs font-semibold transition ${
              panelMode === "commands"
                ? "border-[var(--info)]/50 bg-[var(--info)]/10 text-contrast"
                : "border-muted bg-[var(--surface-elevated)] text-secondary hover:border-[var(--info)] hover:text-contrast"
            }`}
          >
            <Command size={14} />
            Komande
          </button>
          <button
            type="button"
            onClick={() => openPanel("inbox")}
            className={`inline-flex items-center gap-1.5 rounded-2xl border px-3 py-2 text-xs font-semibold transition ${
              panelMode === "inbox"
                ? "border-[var(--info)]/50 bg-[var(--info)]/10 text-contrast"
                : "border-muted bg-[var(--surface-elevated)] text-secondary hover:border-[var(--info)] hover:text-contrast"
            }`}
          >
            <Bell size={14} />
            Obaveštenja
            <span className={`rounded-full border px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-[0.14em] ${alertBadgeTone}`}>
              {inboxEntries.length}
            </span>
          </button>
          <button
            type="button"
            onClick={() => openPanel("context")}
            className={`inline-flex items-center gap-1.5 rounded-2xl border px-3 py-2 text-xs font-semibold transition ${
              panelMode === "context"
                ? "border-[var(--info)]/50 bg-[var(--info)]/10 text-contrast"
                : "border-muted bg-[var(--surface-elevated)] text-secondary hover:border-[var(--info)] hover:text-contrast"
            }`}
          >
            <Sparkles size={14} />
            Kontekst
          </button>

          <div
            className={`inline-flex items-center gap-1.5 rounded-2xl border px-3 py-2 text-xs font-semibold ${scopeTone(dataScopeValue)}`}
            title={`Trenutni prikaz: ${dataScopeLabel(dataScopeValue)}`}
          >
            <Database size={14} />
            <span className="hidden sm:inline">Prikaz</span>
            <span>{dataScopeLabel(dataScopeValue)}</span>
          </div>

          <label className="inline-flex items-center gap-2 rounded-2xl border border-muted bg-[var(--surface-elevated)] px-2.5 py-1.5 text-xs font-semibold text-secondary">
            <Database size={14} className="text-[var(--info)]" />
            <span className="hidden sm:inline">Prikaz</span>
            <select
              value={dataScopeValue}
              onChange={(e) => onScopeChange(e.target.value as DataScope)}
              className="min-w-[118px] rounded-xl border border-muted bg-[var(--surface-light)] px-2.5 py-1.5 text-xs font-semibold text-contrast"
              title={`Trenutni prikaz: ${dataScopeLabel(dataScopeValue)}`}
            >
              <option value="all">Sve</option>
              <option value="existing">Postojeći</option>
              <option value="imported">Importovani</option>
            </select>
          </label>

          <Link
            to="/settings/themes"
            className="inline-flex items-center gap-1.5 rounded-xl border border-muted bg-[var(--surface-elevated)] px-3 py-2 text-xs font-semibold text-contrast transition hover:border-[var(--info)] hover:bg-[var(--surface-light)]"
            title="Podešavanje tema"
          >
            <Settings size={14} />
            <span className="hidden sm:inline">Teme</span>
          </Link>
          <button
            type="button"
            onClick={refreshAll}
            className="inline-flex items-center gap-1.5 rounded-xl border border-[var(--info)]/50 bg-[var(--info)]/10 px-3 py-2 text-xs font-semibold text-contrast transition hover:translate-y-[-1px] hover:bg-[var(--info)]/15"
            title="Osveži aplikaciju i primeni trenutni prikaz podataka"
          >
            <RefreshCw size={14} className={refreshing ? "animate-spin" : ""} />
            <span>Osveži</span>
          </button>
        </div>
      </div>

      <div className="mt-2 flex items-center gap-2 overflow-x-auto pb-0.5 xl:hidden">
        <span className="inline-flex items-center gap-1 rounded-full border border-muted bg-[var(--surface-elevated)] px-2.5 py-1 text-[11px] font-semibold text-muted">
          <Activity size={12} />
          Sistemske kontrole
        </span>
        <ApiPingFlag />
        <WorkerControlFlag />
        <RedisToggleFlag />
      </div>

      {panelMode !== null ? (
        <div className="absolute inset-x-4 top-full z-40 mt-3">
          <div className="mx-auto w-full max-w-[1320px] overflow-hidden rounded-[28px] border border-muted bg-[var(--surface-default)]/98 shadow-[0_24px_68px_-34px_rgba(0,0,0,0.9)] backdrop-blur-xl">
            <div className="flex flex-wrap items-start justify-between gap-3 border-b border-muted px-4 py-4">
              <div className="min-w-0">
                <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">Command center</div>
                <h2 className="mt-1 text-base font-semibold text-contrast">{panelTitle}</h2>
                <p className="mt-1 max-w-2xl text-sm text-secondary">{panelSubtitle}</p>
              </div>
              <button
                type="button"
                onClick={closePanel}
                className="inline-flex items-center gap-1.5 rounded-2xl border border-muted bg-[var(--surface-elevated)] px-3 py-2 text-xs font-semibold text-secondary transition hover:border-[var(--info)] hover:text-contrast"
              >
                <X size={14} />
                Zatvori
              </button>
            </div>

            <div className="grid gap-4 p-4 xl:grid-cols-[1.35fr_0.95fr_0.8fr]">
              <section className={`rounded-[24px] border p-4 ${panelMode === "commands" ? "border-[var(--info)]/50 bg-[var(--info)]/8" : "border-muted bg-[var(--surface-elevated)]/80"}`}>
                <div className="flex items-center gap-2">
                  <Command size={16} className="text-[var(--info)]" />
                  <div>
                    <div className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">Komande</div>
                    <h3 className="text-sm font-semibold text-contrast">Brze veze i akcije</h3>
                  </div>
                </div>

                <label className="mt-4 flex items-center gap-2 rounded-2xl border border-muted bg-[var(--surface-default)] px-3 py-2 text-sm text-secondary">
                  <Search size={14} className="text-muted" />
                  <input
                    ref={commandInputRef}
                    value={commandQuery}
                    onChange={(event) => setCommandQuery(event.target.value)}
                    type="search"
                    placeholder="Pretraži stranice i akcije..."
                    className="min-w-0 flex-1 bg-transparent text-sm text-contrast outline-none placeholder:text-muted"
                  />
                </label>

                <div className="mt-4 max-h-[360px] space-y-2 overflow-y-auto pr-1">
                  {filteredLauncherEntries.length > 0 ? (
                    filteredLauncherEntries.map((entry) =>
                      entry.kind === "route" ? (
                        <Link
                          key={entry.key}
                          to={entry.to}
                          onClick={closePanel}
                          className="group flex items-start gap-3 rounded-2xl border border-muted bg-[var(--surface-default)] px-3 py-3 transition hover:border-[var(--info)] hover:bg-[var(--surface-light)]"
                        >
                          <div className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-xl border border-[var(--info)]/20 bg-[var(--info)]/10 text-[var(--info)]">
                            <entry.icon size={15} />
                          </div>
                          <div className="min-w-0 flex-1">
                            <div className="flex flex-wrap items-center gap-2">
                              <span className="truncate text-sm font-semibold text-contrast">{entry.label}</span>
                              <span className="rounded-full border border-muted bg-[var(--surface-elevated)] px-2 py-0.5 text-[10px] font-bold uppercase tracking-[0.16em] text-muted">
                                {entry.groupLabel}
                              </span>
                            </div>
                            <p className="mt-1 text-xs text-secondary">{entry.description}</p>
                          </div>
                          <ChevronRight size={14} className="mt-1 shrink-0 text-muted transition group-hover:text-contrast" />
                        </Link>
                      ) : (
                        <button
                          key={entry.key}
                          type="button"
                          onClick={() => {
                            entry.onSelect();
                            closePanel();
                          }}
                          className="group flex w-full items-start gap-3 rounded-2xl border border-muted bg-[var(--surface-default)] px-3 py-3 text-left transition hover:border-[var(--info)] hover:bg-[var(--surface-light)]"
                        >
                          <div className="mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-xl border border-[var(--info)]/20 bg-[var(--info)]/10 text-[var(--info)]">
                            <entry.icon size={15} />
                          </div>
                          <div className="min-w-0 flex-1">
                            <div className="flex flex-wrap items-center gap-2">
                              <span className="truncate text-sm font-semibold text-contrast">{entry.label}</span>
                              <span className="rounded-full border border-muted bg-[var(--surface-elevated)] px-2 py-0.5 text-[10px] font-bold uppercase tracking-[0.16em] text-muted">
                                {entry.groupLabel}
                              </span>
                            </div>
                            <p className="mt-1 text-xs text-secondary">{entry.description}</p>
                          </div>
                          <ChevronRight size={14} className="mt-1 shrink-0 text-muted transition group-hover:text-contrast" />
                        </button>
                      )
                    )
                  ) : (
                    <div className="rounded-2xl border border-dashed border-muted bg-[var(--surface-default)] px-4 py-8 text-sm text-secondary">
                      Nema podudaranja za trenutni unos.
                    </div>
                  )}
                </div>
              </section>

              <section className={`rounded-[24px] border p-4 ${panelMode === "inbox" ? "border-[var(--warning)]/50 bg-warning-soft/20" : "border-muted bg-[var(--surface-elevated)]/80"}`}>
                <div className="flex items-center gap-2">
                  <Bell size={16} className="text-[var(--warning)]" />
                  <div>
                    <div className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">Obaveštenja</div>
                    <h3 className="text-sm font-semibold text-contrast">Signali i akcije</h3>
                  </div>
                </div>

                <div className="mt-4 space-y-3">
                  {inboxEntries.map((entry) => (
                    <article key={entry.key} className="rounded-2xl border border-muted bg-[var(--surface-default)] p-3">
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <div className="flex flex-wrap items-center gap-2">
                            <span className="text-sm font-semibold text-contrast">{entry.title}</span>
                            <span
                              className={`rounded-full border px-2 py-0.5 text-[10px] font-bold uppercase tracking-[0.16em] ${
                                entry.tone === "critical"
                                  ? "border-[var(--error)]/40 bg-error-soft text-[var(--error)]"
                                  : entry.tone === "warning"
                                  ? "border-[var(--warning)]/40 bg-warning-soft text-[var(--warning)]"
                                  : "border-[var(--info)]/40 bg-[var(--info)]/10 text-[var(--info)]"
                              }`}
                            >
                              {entry.tone === "critical" ? "Kritično" : entry.tone === "warning" ? "Upozorenje" : "Info"}
                            </span>
                          </div>
                          <p className="mt-1 text-xs leading-5 text-secondary">{entry.detail}</p>
                        </div>
                      </div>
                      <div className="mt-3">
                        {entry.to ? (
                          <Link
                            to={entry.to}
                            onClick={closePanel}
                            className="inline-flex items-center gap-1.5 rounded-xl border border-muted bg-[var(--surface-elevated)] px-3 py-2 text-xs font-semibold text-contrast transition hover:border-[var(--info)] hover:bg-[var(--surface-light)]"
                          >
                            {entry.actionLabel}
                          </Link>
                        ) : (
                          <button
                            type="button"
                            onClick={() => {
                              entry.onSelect?.();
                              closePanel();
                            }}
                            className="inline-flex items-center gap-1.5 rounded-xl border border-muted bg-[var(--surface-elevated)] px-3 py-2 text-xs font-semibold text-contrast transition hover:border-[var(--info)] hover:bg-[var(--surface-light)]"
                          >
                            {entry.actionLabel}
                          </button>
                        )}
                      </div>
                    </article>
                  ))}
                </div>
              </section>

              <section className={`rounded-[24px] border p-4 ${panelMode === "context" ? "border-[var(--success)]/40 bg-success-soft/15" : "border-muted bg-[var(--surface-elevated)]/80"}`}>
                <div className="flex items-center gap-2">
                  <Sparkles size={16} className="text-[var(--success)]" />
                  <div>
                    <div className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">Kontekst</div>
                    <h3 className="text-sm font-semibold text-contrast">Naloga, prodavnica i prikaz</h3>
                  </div>
                </div>

                <div className="mt-4 space-y-3">
                  <div className="rounded-2xl border border-muted bg-[var(--surface-default)] p-3">
                    <div className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">Prikaz</div>
                    <div className={`mt-2 inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-semibold ${scopeTone(dataScopeValue)}`}>
                      <Database size={13} />
                      {dataScopeLabel(dataScopeValue)}
                    </div>
                  </div>

                  <div className="rounded-2xl border border-dashed border-muted bg-[var(--surface-default)] p-3">
                    <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.16em] text-muted">
                      <UserRound size={13} />
                      Nalog
                    </div>
                    <p className="mt-2 text-sm font-semibold text-contrast">Slot spreman</p>
                    <p className="mt-1 text-xs leading-5 text-secondary">
                      Autentikacioni izvor nije povezan sa ovim headerom, pa ovde ostaje pripremljeno mesto umesto lažnog korisnika.
                    </p>
                  </div>

                  <div className="rounded-2xl border border-dashed border-muted bg-[var(--surface-default)] p-3">
                    <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.16em] text-muted">
                      <Store size={13} />
                      Prodavnica
                    </div>
                    <p className="mt-2 text-sm font-semibold text-contrast">Slot spreman</p>
                    <p className="mt-1 text-xs leading-5 text-secondary">
                      Prodavnica će se prikazati tek kada postoji pouzdan izvor podataka.
                    </p>
                  </div>

                  <div className="rounded-2xl border border-muted bg-[var(--surface-default)] p-3">
                    <div className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">Ruta</div>
                    <div className="mt-2 flex flex-wrap items-center gap-1 text-xs font-semibold text-contrast">
                      {trail.map((entry, index) => (
                        <span key={`${entry.label}-${index}`} className="inline-flex items-center gap-1">
                          {index > 0 ? <ChevronRight size={11} className="text-muted" /> : null}
                          <span className={index === trail.length - 1 ? "text-contrast" : "text-secondary"}>{entry.label}</span>
                        </span>
                      ))}
                    </div>
                  </div>
                </div>
              </section>
            </div>
          </div>
        </div>
      ) : null}
    </header>
  );
}
