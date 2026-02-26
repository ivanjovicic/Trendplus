import { Link } from "react-router-dom";
import { BarChart3, Package, ShoppingCart, Tags, Undo2, Wrench } from "lucide-react";

const QUICK_ACTIONS = [
  { to: "/artikli/lista", title: "Pregled artikala", subtitle: "Brzo azuriranje kataloga", icon: Package },
  { to: "/prodaja", title: "Prodaja", subtitle: "Kreiranje novih racuna", icon: ShoppingCart },
  { to: "/nivelacija", title: "Nivelacija", subtitle: "Korekcija cena i marzi", icon: Tags },
  { to: "/povracaj", title: "Povracaj robe", subtitle: "Obrada reklamacija", icon: Undo2 },
  { to: "/analytics", title: "Analitika", subtitle: "KPI i trend performanse", icon: BarChart3 },
  { to: "/logs", title: "Logovi", subtitle: "Monitoring i dijagnostika", icon: Wrench },
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
              className="group rounded-2xl border border-[#2b2d34] bg-[#1a1b1f] p-4 shadow-[0_12px_30px_-20px_rgba(0,0,0,0.85)] transition hover:-translate-y-0.5 hover:border-[#3e64b8] hover:bg-[#1f2330]"
            >
              <div className="mb-3 inline-flex rounded-lg border border-[#3b3d47] bg-[#15161b] p-2 text-[#79a1ff] transition group-hover:border-[#5576be] group-hover:text-[#9fbfff]">
                <Icon size={18} />
              </div>
              <h3 className="text-base font-semibold text-white">{item.title}</h3>
              <p className="mt-1 text-sm text-[#8f9bb2]">{item.subtitle}</p>
            </Link>
          );
        })}
      </div>
    </section>
  );
}

