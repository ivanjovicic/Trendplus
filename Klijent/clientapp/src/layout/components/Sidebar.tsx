import { useEffect, useMemo, useState } from "react";
import { NavLink, useLocation } from "react-router-dom";
import { ChevronDown, X } from "lucide-react";
import { NAV_GROUPS } from "../navConfig";

type SidebarProps = {
  mobileOpen: boolean;
  onCloseMobile: () => void;
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

export default function Sidebar({ mobileOpen, onCloseMobile }: SidebarProps) {
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
    <aside className="h-full w-80 border-r border-[#2a2b32] bg-[#111217]">
      <div className="flex items-center justify-between border-b border-[#2a2b32] px-5 py-4">
        <div>
          <div className="text-sm font-medium text-[#91a2c0]">Trendplus</div>
          <h1 className="text-lg font-semibold text-white">Backoffice</h1>
        </div>
        <button
          type="button"
          className="rounded-md border border-[#2a2b32] p-1 text-[#9fa9ba] lg:hidden"
          onClick={onCloseMobile}
          aria-label="Close navigation"
        >
          <X size={16} />
        </button>
      </div>

      <nav className="h-[calc(100%-73px)] overflow-y-auto px-3 py-4">
        <div className="space-y-2">
          {NAV_GROUPS.map((group) => {
            const GroupIcon = group.icon;
            const isOpen = openGroups.has(group.id);
            return (
              <div key={group.id} className="rounded-xl border border-[#23242b] bg-[#181920]">
                <button
                  type="button"
                  onClick={() => toggleGroup(group.id)}
                  className="flex w-full items-center justify-between px-3 py-2.5 text-left"
                >
                  <span className="flex items-center gap-2 text-sm font-semibold text-[#c9d3e4]">
                    <GroupIcon size={15} className="text-[#7ea5ff]" />
                    {group.label}
                  </span>
                  <ChevronDown
                    size={15}
                    className={`text-[#7f8aa0] transition-transform ${isOpen ? "rotate-180" : ""}`}
                  />
                </button>

                {isOpen ? (
                  <ul className="space-y-1 px-2 pb-2">
                    {group.items.map((item) => {
                      const ItemIcon = item.icon;
                      return (
                        <li key={item.to}>
                          <NavLink
                            to={item.to}
                            onClick={onCloseMobile}
                            className={({ isActive }) =>
                              `group flex items-center gap-2 rounded-lg px-2.5 py-2 text-sm transition ${
                                isActive
                                  ? "bg-[#1f2940] text-[#d8e5ff] ring-1 ring-[#32579e]"
                                  : "text-[#9eabc4] hover:bg-[#20222a] hover:text-white"
                              }`
                            }
                          >
                            <ItemIcon size={15} className="shrink-0 text-[#86a7ff] group-hover:text-[#9dc0ff]" />
                            <span className="truncate">{item.label}</span>
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
      <div className="hidden lg:block">{sidebarContent}</div>
      {mobileOpen ? (
        <div className="fixed inset-0 z-50 lg:hidden">
          <div className="absolute inset-0 bg-black/60" onClick={onCloseMobile} />
          <div className="absolute left-0 top-0 h-full">{sidebarContent}</div>
        </div>
      ) : null}
    </>
  );
}

