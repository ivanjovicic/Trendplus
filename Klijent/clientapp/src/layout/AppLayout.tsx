import { useEffect, useRef, useState } from "react";
import { useLocation } from "react-router-dom";
import AutoReloadOnBackendOnline from "../components/AutoReloadOnBackendOnline";
import BackendWakeupNotice from "../components/BackendWakeupNotice";
import GlobalRequestSpinner from "../components/GlobalRequestSpinner";
import WorkerStatusAlert from "../components/WorkerStatusAlert";
import AnalyticsRefreshStatusBanner from "../components/analytics/AnalyticsRefreshStatusBanner";
import DashboardFooter from "../components/dashboard/DashboardFooter";
import { getAnalyticsRefreshStatus } from "../services/analyticsApi";
import type { AnalyticsRefreshStatus } from "../types/analytics";
import Sidebar from "./components/Sidebar";
import HeaderStatus from "./components/HeaderStatus";

const SIDEBAR_COLLAPSED_PREFERENCE_KEY = "trendplus.sidebarCollapsed";
const SMALL_LAPTOP_QUERY = "(min-width: 1024px) and (max-width: 1279px)";

function readSidebarPreference(): boolean | null {
  if (typeof window === "undefined") return null;
  try {
    const preference = window.localStorage.getItem(SIDEBAR_COLLAPSED_PREFERENCE_KEY);
    if (preference === "collapsed") return true;
    if (preference === "expanded") return false;
  } catch {
    // Storage can be unavailable in privacy-restricted browser contexts.
  }
  return null;
}

function matchesSmallLaptopQuery(): boolean {
  return typeof window !== "undefined"
    && typeof window.matchMedia === "function"
    && window.matchMedia(SMALL_LAPTOP_QUERY).matches;
}

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [sidebarPreference, setSidebarPreference] = useState<boolean | null>(readSidebarPreference);
  const [isSmallLaptop, setIsSmallLaptop] = useState(matchesSmallLaptopQuery);
  const sidebarCollapsed = sidebarPreference ?? isSmallLaptop;
  const [refreshStatus, setRefreshStatus] = useState<AnalyticsRefreshStatus | null>(null);
  const [refreshStatusLoading, setRefreshStatusLoading] = useState(false);
  const [refreshStatusError, setRefreshStatusError] = useState<string | null>(null);
  const mobileNavButtonRef = useRef<HTMLButtonElement | null>(null);
  const { pathname } = useLocation();
  const isAnalyticsRoute = pathname.startsWith("/analytics")
    || pathname.startsWith("/analytics-details")
    || pathname.startsWith("/analitika/");

  useEffect(() => {
    if (typeof window.matchMedia !== "function") return undefined;
    const media = window.matchMedia(SMALL_LAPTOP_QUERY);
    const update = () => setIsSmallLaptop(media.matches);
    update();
    media.addEventListener("change", update);
    return () => media.removeEventListener("change", update);
  }, []);

  const toggleSidebarCollapsed = () => {
    const nextPreference = !sidebarCollapsed;
    setSidebarPreference(nextPreference);
    try {
      window.localStorage.setItem(SIDEBAR_COLLAPSED_PREFERENCE_KEY, nextPreference ? "collapsed" : "expanded");
    } catch {
      // Keep the current-session choice when browser storage is unavailable.
    }
  };

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
          onToggleCollapse={toggleSidebarCollapsed}
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


          <DashboardFooter />
        </div>
      </div>
    </div>
  );
}
