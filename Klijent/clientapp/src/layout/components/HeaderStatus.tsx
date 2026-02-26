import { useContext, useState } from "react";
import { Menu, RefreshCw, Server } from "lucide-react";
import ApiPingFlag from "../../components/ApiPingFlag";
import WorkerControlFlag from "../../components/WorkerControlFlag";
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
    <header className="sticky top-0 z-30 border-b border-[#2a2b32] bg-[#12141a]/95 px-4 py-3 backdrop-blur">
      <div className="flex flex-wrap items-center gap-3">
        <button
          type="button"
          onClick={onOpenMobileNav}
          className="rounded-lg border border-[#2a2b32] bg-[#1a1b1f] p-2 text-[#b9c4d8] lg:hidden"
          aria-label="Open navigation"
        >
          <Menu size={16} />
        </button>

        <div className="flex min-w-[180px] items-center gap-2 rounded-xl border border-[#2a2b32] bg-[#1a1b1f] px-3 py-2">
          <Server size={16} className={online ? "text-emerald-400" : "text-red-400"} />
          <span className="text-xs uppercase tracking-wide text-[#95a4be]">Backend</span>
          <span className={`text-sm font-semibold ${online ? "text-emerald-300" : "text-red-300"}`}>
            {online ? "ONLINE" : "OFFLINE"}
          </span>
        </div>

        <ApiPingFlag />
        <WorkerControlFlag />

        <div className="ml-auto flex items-center gap-2">
          <label className="text-xs text-[#90a0ba]">Prikaz</label>
          <select
            value={dataScopeValue}
            onChange={(e) => onScopeChange(e.target.value as DataScope)}
            className="rounded-lg border border-[#2a2b32] bg-[#1a1b1f] px-2.5 py-2 text-xs text-[#d6e0f2]"
          >
            <option value="all">Sve</option>
            <option value="existing">Postojeci</option>
            <option value="imported">Importovani</option>
          </select>
          <button
            type="button"
            onClick={refreshAll}
            className="inline-flex items-center gap-1.5 rounded-lg border border-[#345dad] bg-[#1d2a46] px-3 py-2 text-xs font-semibold text-[#d6e4ff] transition hover:bg-[#22335a]"
          >
            <RefreshCw size={14} className={refreshing ? "animate-spin" : ""} />
            Osvezi
          </button>
        </div>
      </div>
    </header>
  );
}

