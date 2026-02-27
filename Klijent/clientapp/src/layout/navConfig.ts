import type { LucideIcon } from "lucide-react";
import {
  Activity,
  BarChart3,
  Boxes,
  CalendarDays,
  Gauge,
  Globe2,
  LayoutGrid,
  ListChecks,
  Logs,
  Package,
  PackagePlus,
  Rocket,
  ScanLine,
  Settings2,
  ShoppingBag,
  ShoppingCart,
  Tags,
  TrendingUp,
  Undo2,
  Wrench,
  Microscope,
  Sparkles,
} from "lucide-react";

export type NavItem = {
  to: string;
  label: string;
  icon: LucideIcon;
};

export type NavGroup = {
  id: string;
  label: string;
  icon: LucideIcon;
  items: NavItem[];
};

export const NAV_GROUPS: NavGroup[] = [
  {
    id: "core",
    label: "Dashboard",
    icon: LayoutGrid,
    items: [
      { to: "/", label: "Pocetna", icon: LayoutGrid },
      { to: "/trend-dashboard", label: "Trend Dashboard", icon: Gauge },
      { to: "/runtime-scoring", label: "Runtime Scoring", icon: ScanLine },
      { to: "/open-training", label: "Open Training", icon: Rocket },
    ],
  },
  {
    id: "inventory",
    label: "Inventory & Sales",
    icon: Package,
    items: [
      { to: "/artikli/lista", label: "Pregled/izmene artikala", icon: Package },
      { to: "/unos-robe", label: "Unos robe", icon: PackagePlus },
      { to: "/prodaja", label: "Prodaja", icon: ShoppingCart },
      { to: "/povracaj", label: "Povracaj robe", icon: Undo2 },
      { to: "/nivelacija", label: "Nivelacija cena", icon: Tags },
      { to: "/nivelacije", label: "Pregled nivelacija", icon: ListChecks },
      { to: "/dnevnik-promena", label: "Dnevnik promena", icon: Logs },
      { to: "/access-import", label: "Access Import", icon: Boxes },
    ],
  },
  {
    id: "master",
    label: "Master Data",
    icon: Settings2,
    items: [
      { to: "/sezone", label: "Sezone", icon: CalendarDays },
      { to: "/tipovi-obuce", label: "Tipovi obuce", icon: ShoppingBag },
      { to: "/dobavljaci", label: "Dobavljaci", icon: Wrench },
      { to: "/release-calendar", label: "Release Calendar", icon: CalendarDays },
    ],
  },
  {
    id: "analytics",
    label: "Analytics",
    icon: BarChart3,
    items: [
      { to: "/analytics", label: "Analitika", icon: BarChart3 },
      { to: "/analytics/nivelacije-pre-post", label: "Pre/Posle Nivelacije", icon: TrendingUp },
      { to: "/analytics/pre-nivelacija-prioriteti", label: "Pre-Nivelacija Prioriteti", icon: Sparkles },
      { to: "/analytics/dobavljaci-tipovi-obuce", label: "Dobavljaci i tipovi obuce", icon: ShoppingBag },
      { to: "/analytics/insight-studio", label: "Insight Studio", icon: Microscope },
      { to: "/analytics-details", label: "Detaljne analize", icon: Activity },
      { to: "/admin/common-products", label: "Zajednicki proizvodi", icon: Boxes },
    ],
  },
  {
    id: "scrapers",
    label: "Scrapers & Trends",
    icon: Globe2,
    items: [
      { to: "/global-trends", label: "Global Trends", icon: Globe2 },
      { to: "/scraper-hub", label: "Scraper Hub Top 10", icon: BarChart3 },
      { to: "/deichmann", label: "Deichmann Scraper", icon: ScanLine },
      { to: "/aboutyou", label: "About You Scraper", icon: ScanLine },
      { to: "/humanic", label: "Humanic Scraper", icon: ScanLine },
      { to: "/amazon-shoes", label: "Amazon Shoes", icon: ShoppingBag },
      { to: "/ebay-shoes", label: "eBay Shoes", icon: ShoppingBag },
      { to: "/google-shopping", label: "Google Shopping", icon: ShoppingBag },
    ],
  },
  {
    id: "admin",
    label: "Monitoring & Admin",
    icon: Settings2,
    items: [
      { to: "/outbox", label: "Outbox Dashboard", icon: Activity },
      { to: "/outbox/messages", label: "Outbox Messages", icon: Logs },
      { to: "/performance", label: "Performance", icon: Gauge },
      { to: "/logs", label: "Logovi", icon: Logs },
      { to: "/image-upload-test", label: "Upload slika (Test)", icon: PackagePlus },
    ],
  },
];
