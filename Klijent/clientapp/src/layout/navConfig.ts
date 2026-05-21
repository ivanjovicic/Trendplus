import type { LucideIcon } from "lucide-react";
import {
  Activity,
  ArrowUpDown,
  BarChart3,
  Boxes,
  CalendarDays,
  CalendarRange,
  ClipboardList,
  Gauge,
  Globe2,
  LayoutGrid,
  LibraryBig,
  Link2,
  ListChecks,
  Logs,
  AlertTriangle,
  Package,
  PackagePlus,
  Palette,
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
  BookOpen,
  Zap,
} from "lucide-react";

export type NavItem = {
  to: string;
  label: string;
  icon: LucideIcon;
  title?: string;
  hidden?: boolean;
  badge?: { label: string; tone?: string; title?: string };
};

export type NavGroup = {
  id: string;
  label: string;
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
    id: "analytics",
    label: "Analitika",
    icon: BarChart3,
    items: [
      {
        to: "/analytics",
        label: "Pregled poslovanja",
        icon: BarChart3,
        title: "Glavni ulaz: KPI pregled, trendovi i prioriteti za tekući period.",
      },
      {
        to: "/analytics/products",
        label: "Odluke o proizvodima",
        icon: Sparkles,
        title: "Centralni ekran za odluke: dopuna, pojačanje, praćenje i sniženje artikala.",
      },
      {
        to: "/analytics/supplier",
        label: "Dobavljači",
        icon: TrendingUp,
        title: "Jedini glavni ulaz za odluke o učinku i saradnji sa dobavljačima.",
      },
      {
        to: "/analytics/inventory",
        label: "Zalihe i dopuna",
        icon: LibraryBig,
        title: "Bilans stanja, signalizacija rizika i predlozi dopune zaliha.",
      },
      {
        to: "/analytics/nivelacije-pre-post",
        label: "Cene i nivelacije: pre/posle",
        icon: ArrowUpDown,
        title: "Uticaj nivelacije cena kroz poređenje perioda pre i posle.",
      },
      {
        to: "/analytics/pre-nivelacija-prioriteti",
        label: "Cene i nivelacije: prioriteti",
        icon: Tags,
        title: "Prioriteti intervencija pre narednog ciklusa nivelacije.",
      },
      {
        to: "/analytics/shoe-type-sales-stats",
        label: "Segmenti prodaje: tip obuće",
        icon: ShoppingBag,
        title: "Performanse prodaje po tipu obuće i preporuke po segmentu.",
      },
      {
        to: "/analytics/daily-sales",
        label: "Segmenti prodaje: smene",
        icon: CalendarRange,
        title: "Promet po smenama i operativni ritam prodaje tokom dana.",
      },
      {
        to: "/analytics/color-sales-stats",
        label: "Segmenti prodaje: boje",
        icon: Palette,
        title: "Prodajni učinak i preporuke po kolor segmentima.",
      },
      {
        to: "/analytics/data-quality",
        label: "Kvalitet podataka",
        icon: AlertTriangle,
        title: "Detekcija i prioritetizacija problema koji utiču na pouzdanost analitike.",
      },
      {
        to: "/analytics/insight-studio",
        label: "Insight Studio",
        icon: Microscope,
        title: "Napredne analize i dublji uvidi za strateške odluke.",
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
      {
        to: "/analytics-details",
        label: "Legacy detaljni pregled",
        icon: Activity,
        title: "Legacy prikaz za power-user potrebe. Glavni analytics dashboard je Pregled poslovanja.",
      },
      { to: "/admin/configuration", label: "Konfiguracija", icon: Settings2 },
      { to: "/admin/common-products", label: "Zajednički proizvodi", icon: Link2 },
      { to: "/image-upload-test", label: "Upload slika (Test)", icon: PackagePlus },
      { to: "/admin/nivelacija-repair", label: "Nivelacija Repair", icon: Zap },
    ],
  },
];
