import { useState } from "react";
import AutoReloadOnBackendOnline from "../components/AutoReloadOnBackendOnline";
import WorkerStatusAlert from "../components/WorkerStatusAlert";
import SeasonalImageCarousel from "../components/trendshoes/SeasonalImageCarousel";
import DashboardFooter from "../components/dashboard/DashboardFooter";
import Sidebar from "./components/Sidebar";
import HeaderStatus from "./components/HeaderStatus";

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false);

  return (
    <div className="min-h-screen surface text-contrast">
      <AutoReloadOnBackendOnline />
      <WorkerStatusAlert />

      <div className="flex min-h-screen">
        <Sidebar mobileOpen={mobileOpen} onCloseMobile={() => setMobileOpen(false)} />

        <div className="flex min-w-0 flex-1 flex-col">
          <HeaderStatus onOpenMobileNav={() => setMobileOpen(true)} />

          <main className="mx-auto w-full max-w-[1320px] flex-1 px-4 py-5">
            <div className="space-y-5">{children}</div>
          </main>

          <section className="w-full pb-5">
            <SeasonalImageCarousel />
          </section>

          <DashboardFooter />
        </div>
      </div>
    </div>
  );
}
