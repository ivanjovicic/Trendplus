import { useContext, useState } from "react";
import { Link } from "react-router-dom";
import { Menu, RefreshCw, Server, Settings } from "lucide-react";
import ApiPingFlag from "../../components/ApiPingFlag";
import WorkerControlFlag from "../../components/WorkerControlFlag";
import RedisToggleFlag from "../../components/RedisToggleFlag";
import { BackendStatusContext } from "../../context/BackendStatusContext";
import { getDataScope, setDataScope, type DataScope } from "../../utils/dataScope";

type HeaderStatusProps = {
  onOpenMobileNav: () => void;
};

export default function HeaderStatus({ onOpenMobileNav }: HeaderStatusProps) {
  const { online } = useContext(BackendStatusContext);
  const [refreshing, setRefreshing] = useState(false);
  const [dataScopeValue, setDataScopeValue] = useState<DataScope>(getDataScope());

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
    <header className="sticky top-0 z-30 border-b border-muted surface/95 px-4 py-3 backdrop-blur">
      <div className="flex flex-wrap items-center gap-3">
        <button
          type="button"
          onClick={onOpenMobileNav}
          className="rounded-lg border border-muted surface-elevated p-2 text-secondary lg:hidden"
          aria-label="Open navigation"
        >
          <Menu size={16} />
        </button>

        <div className="flex min-w-[180px] items-center gap-2 rounded-xl border border-muted surface-elevated px-3 py-2">
          <Server size={16} className={online ? "text-emerald-400" : "text-red-400"} />
          <span className="text-xs uppercase tracking-wide text-muted">Backend</span>
          <span className={`text-sm font-semibold ${online ? "text-emerald-300" : "text-red-300"}`}>
            {online ? "ONLINE" : "OFFLINE"}
          </span>
        </div>

        <ApiPingFlag />
        <WorkerControlFlag />
        <RedisToggleFlag />

        <div className="ml-auto flex items-center gap-2">
          <label className="text-xs text-secondary">Prikaz</label>
          <select
            value={dataScopeValue}
            onChange={(e) => onScopeChange(e.target.value as DataScope)}
            className="dark-select control-muted rounded-lg px-2.5 py-2 text-xs"
          >
            <option value="all">Sve</option>
            <option value="existing">Postojeci</option>
            <option value="imported">Importovani</option>
          </select>
          <Link
            to="/settings/themes"
            className="inline-flex items-center gap-1.5 rounded-lg border border-muted surface-elevated px-3 py-2 text-xs font-semibold text-contrast transition hover:surface-hover"
            title="Podešavanje tema"
          >
            <Settings size={14} />
            Teme
          </Link>
          <button
            type="button"
            onClick={refreshAll}
            className="inline-flex items-center gap-1.5 rounded-lg border border-muted surface-elevated px-3 py-2 text-xs font-semibold text-contrast transition hover:bg-[var(--surface-light)]"
          >
            <RefreshCw size={14} className={refreshing ? "animate-spin" : ""} />
            Osvezi
          </button>
        </div>
      </div>
    </header>
  );
}

