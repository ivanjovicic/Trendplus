import type { LucideIcon } from "lucide-react";
import {
  Activity,
  AlertTriangle,
  BarChart3,
  BookOpen,
  Boxes,
  CalendarDays,
  ClipboardList,
  Gauge,
  Globe2,
  LayoutGrid,
  ListChecks,
  Logs,
  Microscope,
  Package,
  PackagePlus,
  Palette,
  Rocket,
  ScanLine,
  Settings2,
  ShoppingBag,
  ShoppingCart,
  Sparkles,
  Tags,
  TrendingUp,
  Undo2,
  Wrench,
  Zap,
} from "lucide-react";

export type NavItem = {
  to: string;
  label: string;
  icon: LucideIcon;
  badge?: { label: string; tone?: string; title?: string };
};

export type NavGroup = {
  id: string;
  label: string;
  sidebarLabel?: string;
  icon: LucideIcon;
  items: NavItem[];
  badge?: { label: string; tone?: string; title?: string };
};

export const NAV_GROUPS: NavGroup[] = [
  {
    id: "core",
    label: "Kontrolna tabla",
    icon: LayoutGrid,
    items: [
      { to: "/", label: "Početna", icon: LayoutGrid },
      {
        to: "/trend-dashboard",
        label: "Trend pregled",
        icon: Gauge,
        badge: { label: "Test", tone: "warning", title: "Funkcionalnost u test fazi" },
      },
      {
        to: "/runtime-scoring",
        label: "Runtime Scoring",
        icon: ScanLine,
        badge: { label: "Test", tone: "warning", title: "Funkcionalnost u test fazi" },
      },
      {
        to: "/open-training",
        label: "Trening modela",
        icon: Rocket,
        badge: { label: "Test", tone: "warning", title: "Funkcionalnost u test fazi" },
      },
    ],
  },
  {
    id: "unos",
    label: "Unos i prodaja",
    icon: ShoppingCart,
    items: [
      { to: "/unos", label: "Centar unosa", icon: ClipboardList },
      { to: "/transfers", label: "Prenosi", icon: Boxes },
      { to: "/prodaja", label: "Prodaja", icon: ShoppingCart },
      { to: "/unos-robe", label: "Unos robe", icon: PackagePlus },
      { to: "/povracaj", label: "Povraćaj robe", icon: Undo2 },
      { to: "/nivelacija", label: "Nivelacija cena", icon: Tags },
    ],
  },
  {
    id: "katalog",
    label: "Katalog i pregledi",
    icon: Package,
    items: [
      { to: "/artikli/lista", label: "Pregled artikala", icon: Package },
      { to: "/nivelacije", label: "Pregled nivelacija", icon: ListChecks },
      { to: "/dnevnik-promena", label: "Dnevnik promena", icon: Logs },
      { to: "/access-import", label: "Uvoz iz Accessa", icon: Boxes },
    ],
  },
  {
    id: "master",
    label: "Šifarnici",
    icon: Settings2,
    items: [
      { to: "/sezone", label: "Sezone", icon: CalendarDays },
      { to: "/tipovi-obuce", label: "Tipovi obuće", icon: ShoppingBag },
      { to: "/dobavljaci", label: "Dobavljači", icon: Wrench },
      { to: "/release-calendar", label: "Kalendar izdanja", icon: CalendarDays },
    ],
  },
  {
    id: "analytics-executive",
    label: "Analitika",
    sidebarLabel: "Executive",
    icon: Sparkles,
    badge: { label: "P0", tone: "warning", title: "Izvršne i readiness površine" },
    items: [
      { to: "/analytics", label: "Trendplus pregled", icon: BarChart3 },
      {
        to: "/analytics/pilot-readiness",
        label: "Pilot spremnost",
        icon: ListChecks,
        badge: { label: "Ready", tone: "info", title: "Signal spremnosti i svežine" },
      },
      {
        to: "/analytics/decision-board",
        label: "Izvršni board",
        icon: Sparkles,
        badge: { label: "Board", tone: "info", title: "Izvršni decision surface" },
      },
    ],
  },
  {
    id: "analytics-decisions",
    label: "Analitika",
    sidebarLabel: "Odluke",
    icon: Microscope,
    items: [
      { to: "/analytics/products", label: "Odluke o proizvodima", icon: Sparkles },
      { to: "/analytics/supplier", label: "Pregled dobavljača", icon: Microscope },
      { to: "/analytics/actions", label: "Centralne akcije", icon: ClipboardList },
      { to: "/analytics/decision-pulse", label: "Decision Pulse", icon: AlertTriangle },
      {
        to: "/analytics/supplier-decision-hub",
        label: "Odluke o dobavljačima",
        icon: Microscope,
        badge: { label: "Hub", tone: "info", title: "Konsolidovani hub odluka za dobavljače" },
      },
    ],
  },
  {
    id: "analytics-operations",
    label: "Analitika",
    sidebarLabel: "Operacije",
    icon: Boxes,
    badge: { label: "Ops", tone: "warning", title: "Operativni tokovi i zalihe" },
    items: [
      { to: "/analytics/inventory", label: "Zalihe i dopuna", icon: Boxes },
      { to: "/analytics/supplier-sales-stats", label: "Prodaja po dobavljačima", icon: TrendingUp },
      { to: "/analytics/shoe-type-sales-stats", label: "Prodaja po tipu obuće", icon: ShoppingBag },
      { to: "/analytics/daily-sales", label: "Prodaja po smeni i dobavljačima", icon: ShoppingBag },
      { to: "/analytics/nivelacije-pre-post", label: "Pre/Posle nivelacije", icon: TrendingUp },
      { to: "/analytics/color-sales-stats", label: "Prodaja po boji artikla", icon: Palette },
      {
        to: "/analytics/pre-nivelacija-prioriteti",
        label: "Prioriteti nivelacije",
        icon: Sparkles,
        badge: { label: "Task", tone: "warning", title: "Operativni prioriteti za nivelaciju" },
      },
      { to: "/analytics/dobavljaci-tipovi-obuce", label: "Dobavljači i tipovi obuće", icon: ShoppingBag },
    ],
  },
  {
    id: "analytics-data-quality",
    label: "Analitika",
    sidebarLabel: "Kvalitet podataka",
    icon: AlertTriangle,
    badge: { label: "DQ", tone: "warning", title: "Kvalitet podataka i svežina signala" },
    items: [
      { to: "/analytics/data-quality", label: "Kvalitet podataka", icon: AlertTriangle },
    ],
  },
  {
    id: "analytics-reports-legacy",
    label: "Analitika",
    sidebarLabel: "Izveštaji / Legacy",
    icon: BookOpen,
    badge: { label: "Archive", tone: "warning", title: "Izveštaji i stariji pregledi" },
    items: [
      {
        to: "/analytics/supplier/report",
        label: "Izveštaj dobavljača",
        icon: ClipboardList,
        badge: { label: "Izveštaj", tone: "info", title: "Dokumentarni izveštaj za dobavljače" },
      },
      {
        to: "/analytics/reports/pilot-intake",
        label: "Pilot intake izveštaj",
        icon: ClipboardList,
        badge: { label: "Izveštaj", tone: "info", title: "Dokument za pilot intake i refresh" },
      },
      {
        to: "/analytics/insight-studio",
        label: "Insight Studio",
        icon: Microscope,
        badge: { label: "Lab", tone: "warning", title: "Eksperimentalni istraživački pregled" },
      },
      {
        to: "/analytics-details",
        label: "Detaljne analize",
        icon: Activity,
        badge: { label: "Legacy", tone: "warning", title: "Stariji detaljni pregled" },
      },
    ],
  },
  {
    id: "scrapers",
    label: "Trendovi i scraperi",
    icon: Globe2,
    badge: { label: "Test", tone: "warning", title: "Ova sekcija je u fazi testa — mogući prekidi" },
    items: [
      { to: "/global-trends", label: "Globalni trendovi", icon: Globe2 },
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
    label: "Nadzor i admin",
    icon: BookOpen,
    items: [
      { to: "/outbox", label: "Outbox nadzor", icon: Activity },
      { to: "/outbox/messages", label: "Outbox poruke", icon: Logs },
      { to: "/performance", label: "Performanse", icon: Gauge },
      { to: "/logs", label: "Logovi", icon: Logs },
      { to: "/image-upload-test", label: "Upload slika (Test)", icon: PackagePlus },
      {
        to: "/admin/common-products",
        label: "Zajednički proizvodi",
        icon: Boxes,
        badge: { label: "Support", tone: "warning", title: "Pomoćni administrativni ekran" },
      },
      { to: "/admin/configuration", label: "Konfiguracija", icon: Settings2 },
      { to: "/admin/nivelacija-repair", label: "Nivelacija Repair", icon: Zap },
    ],
  },
];
