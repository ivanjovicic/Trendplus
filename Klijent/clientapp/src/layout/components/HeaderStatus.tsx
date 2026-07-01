import { useContext, useMemo, useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { Activity, ChevronRight, Database, Menu, RefreshCw, Server, Settings, Sparkles } from "lucide-react";
import ApiPingFlag from "../../components/ApiPingFlag";
import WorkerControlFlag from "../../components/WorkerControlFlag";
import RedisToggleFlag from "../../components/RedisToggleFlag";
import { BackendStatusContext } from "../../context/BackendStatusContext";
import { getDataScope, setDataScope, type DataScope } from "../../utils/dataScope";
import { NAV_GROUPS } from "../navConfig";

type HeaderStatusProps = {
  onOpenMobileNav: () => void;
};

function isRouteMatch(pathname: string, route: string): boolean {
  if (route === "/") return pathname === "/";
  return pathname === route || pathname.startsWith(`${route}/`);
}

function resolveCurrentNavigation(pathname: string) {
  let selectedGroup = NAV_GROUPS[0];
  let selectedItem = NAV_GROUPS[0]?.items[0] ?? null;
  let longestMatch = -1;

  for (const group of NAV_GROUPS) {
    for (const item of group.items) {
      if (!isRouteMatch(pathname, item.to)) continue;
      if (item.to.length < longestMatch) continue;
      longestMatch = item.to.length;
      selectedGroup = group;
      selectedItem = item;
    }
  }

  return { group: selectedGroup, item: selectedItem };
}

function dataScopeLabel(value: DataScope): string {
  if (value === "existing") return "Postojeći";
  if (value === "imported") return "Importovani";
  return "Sve";
}

export default function HeaderStatus({ onOpenMobileNav }: HeaderStatusProps) {
  const { online, checking, lastCheckedAt } = useContext(BackendStatusContext);
  const location = useLocation();
  const [refreshing, setRefreshing] = useState(false);
  const [dataScopeValue, setDataScopeValue] = useState<DataScope>(getDataScope());

  const { group, item } = useMemo(
    () => resolveCurrentNavigation(location.pathname),
    [location.pathname],
  );

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

  return (
    <header className="sticky top-0 z-30 border-b border-muted bg-[var(--surface-default)]/95 px-4 py-3 shadow-[0_18px_42px_-38px_rgba(0,0,0,0.85)] backdrop-blur-xl">
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
            <div className="flex min-w-0 items-center gap-1.5 text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">
              <span className="truncate">{group?.label ?? "Trendplus"}</span>
              <ChevronRight size={12} className="shrink-0" />
              <span className="truncate">{item?.label ?? "Pregled"}</span>
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
          <div className={`inline-flex items-center gap-2 rounded-xl border px-2.5 py-1.5 text-xs font-semibold ${backendTone}`} title={lastCheckedAt ? `Poslednja provera: ${lastCheckedAt.toLocaleTimeString("sr-RS")}` : "Backend status"}>
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
    </header>
  );
}
