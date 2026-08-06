import { useEffect, useMemo, useState } from "react";
import { NavLink, useLocation } from "react-router-dom";
import { ChevronDown, ChevronLeft, ChevronRight, X } from "lucide-react";
import { NAV_GROUPS } from "../navConfig";

type SidebarProps = {
  mobileOpen: boolean;
  onCloseMobile: () => void;
  collapsed?: boolean;
  onToggleCollapse?: () => void;
};

function isRouteMatch(pathname: string, route: string): boolean {
  if (route === "/") return pathname === "/";
  return pathname === route || pathname.startsWith(`${route}/`);
}

function findCurrentGroupId(pathname: string): string {
  let selectedGroupId = "core";
  let longestMatch = 0;

  for (const group of NAV_GROUPS) {
    for (const item of group.items) {
      if (!isRouteMatch(pathname, item.to)) continue;
      if (item.to.length < longestMatch) continue;
      longestMatch = item.to.length;
      selectedGroupId = group.id;
    }
  }

  return selectedGroupId;
}

function findBestMatchForGroup(pathname: string, group: { id: string; items: { to: string }[] }) {
  let best: string | null = null;
  let bestLen = -1;
  for (const item of group.items) {
    if (!isRouteMatch(pathname, item.to)) continue;
    if (item.to.length > bestLen) {
      bestLen = item.to.length;
      best = item.to;
    }
  }
  return best;
}

export default function Sidebar({ mobileOpen, onCloseMobile, collapsed, onToggleCollapse }: SidebarProps) {
  const location = useLocation();
  const defaultOpenGroups = useMemo(() => {
    return new Set<string>([findCurrentGroupId(location.pathname)]);
  }, [location.pathname]);

  const [openGroups, setOpenGroups] = useState<Set<string>>(defaultOpenGroups);

  useEffect(() => {
    const currentGroupId = findCurrentGroupId(location.pathname);

    if (!currentGroupId) return;

    setOpenGroups((prev) => {
      if (prev.has(currentGroupId)) return prev;
      const next = new Set(prev);
      next.add(currentGroupId);
      return next;
    });
  }, [location.pathname]);

  const toggleGroup = (groupId: string) => {
    setOpenGroups((prev) => {
      const next = new Set(prev);
      if (next.has(groupId)) next.delete(groupId);
      else next.add(groupId);
      return next;
    });
  };

  const sidebarContent = (
    <aside className="h-full w-80 border-r border-muted bg-[linear-gradient(180deg,var(--surface-darker)_0%,var(--surface-default)_42%,var(--surface-elevated)_100%)] text-contrast shadow-[18px_0_60px_-42px_rgba(0,0,0,0.75)]">
      <div className="border-b border-muted px-5 py-4">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="inline-flex items-center gap-2 rounded-full border border-[var(--border-default)] bg-[var(--surface-light)] px-2.5 py-1 text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">
              Trendplus
            </div>
            <h1 className="mt-3 text-xl font-semibold leading-tight text-contrast">Backoffice</h1>
            <p className="mt-1 text-xs leading-relaxed text-secondary">Prodaja, lager, odluke i kontrola podataka.</p>
          </div>
          <div className="flex items-center gap-1">
            <button
              type="button"
              className="hidden rounded-xl border border-muted bg-[var(--surface-light)] p-1.5 text-secondary transition hover:border-[var(--info)] hover:text-contrast lg:block"
              onClick={onToggleCollapse}
              aria-label="Skupi meni"
              title="Skupi meni"
            >
              <ChevronLeft size={16} />
            </button>
            <button
              type="button"
              className="rounded-xl border border-muted bg-[var(--surface-light)] p-1.5 text-secondary transition hover:border-[var(--info)] hover:text-contrast lg:hidden"
              onClick={onCloseMobile}
              aria-label="Zatvori navigaciju"
            >
              <X size={16} />
            </button>
          </div>
        </div>
      </div>

      <nav className="h-[calc(100%-96px)] overflow-y-auto px-3 py-4">
        <div className="space-y-2.5">
          {NAV_GROUPS.map((group) => {
            const GroupIcon = group.icon;
            const groupLabel = group.sidebarLabel ?? group.label;
            const isOpen = openGroups.has(group.id);
            const activeItemTo = findBestMatchForGroup(location.pathname, group);
            const isGroupActive = activeItemTo != null;
            return (
              <div
                key={group.id}
                className={`rounded-2xl border bg-[var(--surface-elevated)]/90 transition ${
                  isGroupActive
                    ? "border-[var(--info)] shadow-[0_18px_42px_-32px_var(--info)]"
                    : "border-muted hover:border-[var(--border-hover)]"
                }`}
              >
                <button
                  type="button"
                  onClick={() => toggleGroup(group.id)}
                  className="flex w-full items-center justify-between gap-3 px-3 py-3 text-left"
                  aria-expanded={isOpen}
                >
                  <span className="flex min-w-0 items-center gap-2.5 text-sm font-semibold text-contrast">
                    <span className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-xl border ${isGroupActive ? "border-[var(--info)] bg-[var(--info)]/10" : "border-muted bg-[var(--surface-light)]"}`}>
                      <GroupIcon size={16} className={isGroupActive ? "text-[var(--info)]" : "text-secondary"} />
                    </span>
                    <span className="min-w-0">
                      <span className="block truncate">{groupLabel}</span>
                      <span className="block text-[11px] font-medium text-muted">{group.items.length} ekrana</span>
                    </span>
                    {group.badge && (
                      <span
                        className={`ml-1 inline-flex shrink-0 items-center rounded-full px-2 py-0.5 text-[11px] font-semibold ${
                          group.badge.tone === "warning" ? "bg-warning/10 text-warning" : "bg-muted/10 text-muted"
                        }`}
                        title={group.badge.title}
                        aria-label={group.badge.title}
                      >
                        {group.badge.label}
                      </span>
                    )}
                  </span>
                  <ChevronDown
                    size={15}
                    className={`shrink-0 text-muted transition-transform ${isOpen ? "rotate-180" : ""}`}
                  />
                </button>

                {isOpen ? (
                  <ul className="space-y-1.5 px-2 pb-2.5">
                    {group.items.map((item) => {
                      const ItemIcon = item.icon;
                      const isTestItem = Boolean(group.badge || item.badge);
                      const badge = item.badge ?? group.badge;
                      const isActive = item.to === activeItemTo;
                      return (
                        <li key={item.to}>
                          <NavLink to={item.to} onClick={onCloseMobile} className={
                            `group relative flex items-center gap-2 rounded-xl px-2.5 py-2.5 text-sm transition ${
                              isActive
                                ? "bg-[var(--surface-light)] text-contrast ring-1 ring-[var(--info)]/80"
                                : "text-secondary hover:bg-[var(--surface-default)] hover:text-contrast"
                            }`
                          }>
                            {isActive ? <span className="absolute left-0 top-2 bottom-2 w-1 rounded-r-full bg-[var(--info)]" /> : null}
                            <ItemIcon size={15} className={`ml-1 shrink-0 ${isActive ? "text-[var(--info)]" : "text-muted group-hover:text-[var(--info)]"}`} />
                            <span className="min-w-0 flex-1 truncate">{item.label}</span>
                            {isTestItem && badge && (
                              <span
                                className={`inline-flex shrink-0 items-center rounded-full px-2 py-0.5 text-[11px] font-semibold ${
                                  badge.tone === "warning" ? "bg-warning/10 text-warning" : "bg-muted/10 text-muted"
                                }`}
                                title={badge.title}
                                aria-label={badge.title}
                              >
                                {badge.label}
                              </span>
                            )}
                          </NavLink>
                        </li>
                      );
                    })}
                  </ul>
                ) : null}
              </div>
            );
          })}
        </div>
      </nav>
    </aside>
  );

  return (
    <>
      {collapsed ? (
        <div className="hidden lg:flex lg:h-screen lg:w-14 lg:shrink-0 lg:flex-col lg:items-center lg:border-r lg:border-muted lg:bg-[var(--surface-darker)] lg:pt-3">
          <button
            type="button"
            className="rounded-xl border border-muted bg-[var(--surface-light)] p-2 text-secondary transition hover:border-[var(--info)] hover:text-contrast"
            onClick={onToggleCollapse}
            aria-label="Raširi meni"
            title="Raširi meni"
          >
            <ChevronRight size={16} />
          </button>
        </div>
      ) : (
        <div className="hidden lg:block lg:sticky lg:top-0 lg:h-screen lg:shrink-0">{sidebarContent}</div>
      )}
      {mobileOpen ? (
        <div className="fixed inset-0 z-50 lg:hidden">
          <div className="absolute inset-0 bg-black/70 backdrop-blur-sm" onClick={onCloseMobile} />
          <div className="absolute left-0 top-0 h-full">{sidebarContent}</div>
        </div>
      ) : null}
    </>
  );
}
