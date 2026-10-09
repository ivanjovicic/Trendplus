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
    id: "analytics-overview",
    label: "Analitika",
    sidebarLabel: "Pregled",
    icon: BarChart3,
    items: [
      { to: "/analytics", label: "Pregled poslovanja", icon: BarChart3 },
    ],
  },
  {
    id: "analytics-primary",
    label: "Analitika",
    sidebarLabel: "Glavne odluke",
    icon: Sparkles,
    items: [
      { to: "/analytics/inventory", label: "Analitika zaliha", icon: Boxes },
      { to: "/analytics/supplier", label: "Prodaja po dobavljačima", icon: Microscope },
      { to: "/analytics/shoe-type-sales-stats", label: "Prodaja po tipu obuće", icon: ShoppingBag },
      { to: "/analytics/pre-nivelacija-prioriteti", label: "Prioriteti nivelacije", icon: Sparkles },
      { to: "/analytics/nivelacije-pre-post", label: "Pre/Posle nivelacije", icon: Activity },
      { to: "/analytics/actions", label: "Akcije i preporuke", icon: ClipboardList },
      { to: "/analytics/data-quality", label: "Kvalitet podataka", icon: AlertTriangle },
    ],
  },
  {
    id: "analytics-additional",
    label: "Analitika",
    sidebarLabel: "Dodatne analize",
    icon: BookOpen,
    items: [
      { to: "/analytics/pilot-readiness", label: "Pilot spremnost", icon: ListChecks },
      { to: "/analytics/decision-board", label: "Izvršni pregled odluka", icon: Sparkles },
      { to: "/analytics/products", label: "Odluke o proizvodima", icon: ShoppingCart },
      { to: "/analytics/decision-pulse", label: "Puls odluka", icon: AlertTriangle },
      { to: "/analytics/supplier?tab=scorecard", label: "Ocena dobavljača", icon: Microscope },
      { to: "/analytics/daily-sales", label: "Prodaja po smenama", icon: CalendarDays },
      {
        to: "/analytics/color-sales-stats",
        label: "Prodaja po boji artikla",
        icon: Palette,
        badge: {
          label: "Analiza",
          tone: "info",
          title: "Sekundarna analiza atributa; nije samostalna konačna preporuka",
        },
      },
      {
        to: "/analytics/supplier/report",
        label: "Izveštaj dobavljača",
        icon: Wrench,
        badge: { label: "Izveštaj", tone: "info", title: "Dokumentarni izveštaj za dobavljače" },
      },
      {
        to: "/analytics/reports/pilot-intake",
        label: "Pilot izveštaj kvaliteta podataka",
        icon: ClipboardList,
        badge: { label: "Izveštaj", tone: "info", title: "Dokument za pilot intake i refresh" },
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
      { to: "/admin/nivelacija-repair", label: "Popravka nivelacija", icon: Zap },
    ],
  },
];
