import { Link } from "react-router-dom";
import { BarChart3, Package, ShoppingCart, Tags, Undo2, Wrench } from "lucide-react";

const QUICK_ACTIONS = [
  { to: "/prodaja", title: "Prodaja", subtitle: "Kreiranje novih računa", icon: ShoppingCart },
  { to: "/unos-robe", title: "Unos robe", subtitle: "Prijem robe po dobavljaču", icon: Package },
  { to: "/nivelacija", title: "Nivelacija", subtitle: "Korekcija cena i marži", icon: Tags },
  { to: "/povracaj", title: "Povraćaj robe", subtitle: "Obrada reklamacija", icon: Undo2 },
  { to: "/analytics", title: "Analitika", subtitle: "KPI i trend performanse", icon: BarChart3 },
  { to: "/logs", title: "Logovi", subtitle: "Nadzor i dijagnostika", icon: Wrench },
];

export default function DashboardCards() {
  return (
    <section className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {QUICK_ACTIONS.map((item) => {
          const Icon = item.icon;
          return (
            <Link
              key={item.to}
              to={item.to}
              className="group rounded-2xl border border-muted surface-elevated p-4 shadow-lg transition hover:-translate-y-0.5 hover:border-[var(--info)] hover:bg-[var(--surface-light)]"
            >
              <div className="mb-3 inline-flex rounded-lg border border-muted bg-[var(--surface-darker)] p-2 text-[var(--info)] transition group-hover:border-secondary group-hover:opacity-80">
                <Icon size={18} />
              </div>
              <h3 className="text-base font-semibold text-contrast">{item.title}</h3>
              <p className="mt-1 text-sm text-muted">{item.subtitle}</p>
            </Link>
          );
        })}
      </div>
    </section>
  );
}

