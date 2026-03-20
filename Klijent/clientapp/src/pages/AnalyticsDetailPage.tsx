import { BarChart3 } from "lucide-react";
import { useLocation, useParams } from "react-router-dom";
import AnalyticsDetailView from "../components/analytics/AnalyticsDetailView";
import { InventoryPageShell } from "../components/inventory/InventoryPageShell";

export default function AnalyticsDetailPage({ standalone = true }: { standalone?: boolean }) {
  const params = useParams<{ table?: string; id?: string }>();
  const location = useLocation();
  const table = params.table ?? "";
  const id = params.id ?? "";
  const content = <AnalyticsDetailView table={table} recordId={id} queryString={location.search} />;

  if (!standalone) {
    return content;
  }

  return (
    <InventoryPageShell
      icon={BarChart3}
      title="Analitika detalj"
      subtitle={`Tabela: ${table} | Zapis: ${id}`}
    >
      {content}
    </InventoryPageShell>
  );
}
