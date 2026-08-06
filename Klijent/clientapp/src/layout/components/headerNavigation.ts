import type { LucideIcon } from "lucide-react";
import { NAV_GROUPS, type NavGroup, type NavItem } from "../navConfig";

export type HeaderTrailEntry = {
  label: string;
  to?: string;
};

export type HeaderRouteCommand = {
  key: string;
  groupLabel: string;
  label: string;
  description: string;
  to: string;
  icon: LucideIcon;
};

export type ResolvedHeaderNavigation = {
  group: NavGroup;
  item: NavItem | null;
  trail: HeaderTrailEntry[];
  matchedRoute: string;
  extraSegments: string[];
};

function stripQueryAndHash(pathname: string): string {
  return pathname.split(/[?#]/)[0] || "/";
}

function isRouteMatch(pathname: string, route: string): boolean {
  if (route === "/") return pathname === "/";
  return pathname === route || pathname.startsWith(`${route}/`);
}

export function humanizeHeaderSegment(segment: string): string {
  const decoded = decodeURIComponent(segment).replace(/[-_]+/g, " ").trim();
  if (!decoded) return segment;
  return decoded.charAt(0).toUpperCase() + decoded.slice(1);
}

export function resolveHeaderNavigation(pathname: string): ResolvedHeaderNavigation {
  const cleanPathname = stripQueryAndHash(pathname);
  let selectedGroup = NAV_GROUPS[0];
  let selectedItem = NAV_GROUPS[0]?.items[0] ?? null;
  let longestMatch = -1;
  let matchedRoute = selectedItem?.to ?? "/";

  for (const group of NAV_GROUPS) {
    for (const item of group.items) {
      if (!isRouteMatch(cleanPathname, item.to)) continue;
      if (item.to.length < longestMatch) continue;
      longestMatch = item.to.length;
      selectedGroup = group;
      selectedItem = item;
      matchedRoute = item.to;
    }
  }

  const baseSegments = matchedRoute === "/" ? [] : matchedRoute.split("/").filter(Boolean);
  const routeSegments = cleanPathname.split("/").filter(Boolean);
  const extraSegments = routeSegments.slice(baseSegments.length);

  const trail: HeaderTrailEntry[] = [{ label: selectedGroup?.label ?? "Trendplus" }];

  if (selectedItem) {
    trail.push({ label: selectedItem.label, to: selectedItem.to });
  }

  for (const segment of extraSegments) {
    trail.push({ label: humanizeHeaderSegment(segment) });
  }

  return {
    group: selectedGroup,
    item: selectedItem,
    trail,
    matchedRoute,
    extraSegments,
  };
}

export function getHeaderRouteCommands(groups: NavGroup[] = NAV_GROUPS): HeaderRouteCommand[] {
  return groups.flatMap((group) =>
    group.items.map((item) => ({
      key: `${group.id}:${item.to}`,
      groupLabel: group.sidebarLabel ?? group.label,
      label: item.label,
      description: item.badge?.title ?? `${group.sidebarLabel ?? group.label} · ${item.label}`,
      to: item.to,
      icon: item.icon,
    }))
  );
}
