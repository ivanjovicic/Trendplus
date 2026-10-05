import { useEffect, useRef, useState } from "react";
import { useLocation } from "react-router-dom";
import AutoReloadOnBackendOnline from "../components/AutoReloadOnBackendOnline";
import BackendWakeupNotice from "../components/BackendWakeupNotice";
import GlobalRequestSpinner from "../components/GlobalRequestSpinner";
import WorkerStatusAlert from "../components/WorkerStatusAlert";
import AnalyticsRefreshStatusBanner from "../components/analytics/AnalyticsRefreshStatusBanner";
import SeasonalImageCarousel from "../components/trendshoes/SeasonalImageCarousel";
import DashboardFooter from "../components/dashboard/DashboardFooter";
import { getAnalyticsRefreshStatus } from "../services/analyticsApi";
import type { AnalyticsRefreshStatus } from "../types/analytics";
import Sidebar from "./components/Sidebar";
import HeaderStatus from "./components/HeaderStatus";

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [refreshStatus, setRefreshStatus] = useState<AnalyticsRefreshStatus | null>(null);
  const [refreshStatusLoading, setRefreshStatusLoading] = useState(false);
  const [refreshStatusError, setRefreshStatusError] = useState<string | null>(null);
  const mobileNavButtonRef = useRef<HTMLButtonElement | null>(null);
  const { pathname } = useLocation();
  const isAnalyticsRoute = pathname.startsWith("/analytics")
    || pathname.startsWith("/analytics-details")
    || pathname.startsWith("/analitika/");

  useEffect(() => {
    if (!isAnalyticsRoute) {
      setRefreshStatus(null);
      setRefreshStatusError(null);
      return;
    }

    let isActive = true;
    const loadRefreshStatus = async () => {
      if (isActive) setRefreshStatusLoading(true);
      try {
        const nextStatus = await getAnalyticsRefreshStatus();
        if (!isActive) return;
        setRefreshStatus(nextStatus);
        setRefreshStatusError(null);
      } catch {
        if (!isActive) return;
        setRefreshStatusError("Status osvežavanja nije moguće potvrditi.");
      } finally {
        if (isActive) setRefreshStatusLoading(false);
      }
    };

    void loadRefreshStatus();
    const intervalId = window.setInterval(() => {
      void loadRefreshStatus();
    }, 120_000);

    return () => {
      isActive = false;
      window.clearInterval(intervalId);
    };
  }, [isAnalyticsRoute]);

  return (
    <div className="min-h-screen surface text-contrast">
      <AutoReloadOnBackendOnline />
      <BackendWakeupNotice />
      <GlobalRequestSpinner />
      <WorkerStatusAlert />
      {isAnalyticsRoute ? (
        <AnalyticsRefreshStatusBanner
          status={refreshStatus}
          loading={refreshStatusLoading}
          error={refreshStatusError}
        />
      ) : null}

      <div className="flex min-h-screen">
        <Sidebar
          mobileOpen={mobileOpen}
          onCloseMobile={() => setMobileOpen(false)}
          collapsed={sidebarCollapsed}
          onToggleCollapse={() => setSidebarCollapsed((c) => !c)}
          returnFocusRef={mobileNavButtonRef}
        />

        <div className="flex min-w-0 flex-1 flex-col">
          <HeaderStatus
            onOpenMobileNav={() => setMobileOpen(true)}
            mobileNavButtonRef={mobileNavButtonRef}
          />

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
