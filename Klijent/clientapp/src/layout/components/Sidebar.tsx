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
    <aside className="h-full w-80 border-r border-muted surface">
      <div className="flex items-center justify-between border-b border-muted px-5 py-4">
        <div>
          <div className="text-sm font-medium text-muted">Trendplus</div>
          <h1 className="text-lg font-semibold text-contrast">Backoffice</h1>
        </div>
        <div className="flex items-center gap-1">
          <button
            type="button"
            className="hidden rounded-md border border-muted p-1 text-secondary lg:block"
            onClick={onToggleCollapse}
            aria-label="Skupi meni"
            title="Skupi meni"
          >
            <ChevronLeft size={16} />
          </button>
          <button
            type="button"
            className="rounded-md border border-muted p-1 text-secondary lg:hidden"
            onClick={onCloseMobile}
            aria-label="Close navigation"
          >
            <X size={16} />
          </button>
        </div>
      </div>

      <nav className="h-[calc(100%-73px)] overflow-y-auto px-3 py-4">
        <div className="space-y-2">
          {NAV_GROUPS.map((group) => {
            const GroupIcon = group.icon;
            const isOpen = openGroups.has(group.id);
            const activeItemTo = findBestMatchForGroup(location.pathname, group);
            return (
              <div key={group.id} className="rounded-xl border border-muted surface-elevated">
                <button
                  type="button"
                  onClick={() => toggleGroup(group.id)}
                  className="flex w-full items-center justify-between px-3 py-2.5 text-left"
                >
                  <span className="flex items-center gap-2 text-sm font-semibold text-contrast">
                    <GroupIcon size={15} className="text-[var(--info)]" />
                    <span>{group.label}</span>
                    {group.badge && (
                      <span
                        className={`ml-2 inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-semibold ${
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
                    className={`text-muted transition-transform ${isOpen ? "rotate-180" : ""}`}
                  />
                </button>

                {isOpen ? (
                  <ul className="space-y-1 px-2 pb-2">
                    {group.items.map((item) => {
                      const ItemIcon = item.icon;
                      const isTestItem = Boolean(group.badge || item.badge);
                      const badge = item.badge ?? group.badge;
                      return (
                        <li key={item.to}>
                          <NavLink to={item.to} onClick={onCloseMobile} className={
                            `group flex items-center gap-2 rounded-lg px-2.5 py-2 text-sm transition ${
                              item.to === activeItemTo
                                ? "bg-[var(--surface-light)] text-contrast ring-1 ring-[var(--info)]"
                                : "text-secondary hover:bg-[var(--surface-default)] hover:text-contrast"
                            }`
                          }>
                            <ItemIcon size={15} className="shrink-0 text-[var(--info)] group-hover:opacity-80" />
                            <span className="truncate flex items-center gap-2">
                              <span>{item.label}</span>
                              {isTestItem && badge && (
                                <span
                                  className={`inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-semibold ${
                                    badge.tone === "warning" ? "bg-warning/10 text-warning" : "bg-muted/10 text-muted"
                                  }`}
                                  title={badge.title}
                                  aria-label={badge.title}
                                >
                                  {badge.label}
                                </span>
                              )}
                            </span>
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
        <div className="hidden lg:flex lg:h-screen lg:w-12 lg:shrink-0 lg:flex-col lg:items-center lg:border-r lg:border-muted lg:pt-3 surface">
          <button
            type="button"
            className="rounded-md border border-muted p-1.5 text-secondary"
            onClick={onToggleCollapse}
            aria-label="Raspiri meni"
            title="Raspiri meni"
          >
            <ChevronRight size={16} />
          </button>
        </div>
      ) : (
        <div className="hidden lg:block">{sidebarContent}</div>
      )}
      {mobileOpen ? (
        <div className="fixed inset-0 z-50 lg:hidden">
          <div className="absolute inset-0 bg-black/60" onClick={onCloseMobile} />
          <div className="absolute left-0 top-0 h-full">{sidebarContent}</div>
        </div>
      ) : null}
    </>
  );
}

