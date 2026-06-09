from __future__ import annotations

import argparse
import csv
import json
from datetime import datetime, timedelta, timezone
from pathlib import Path


ANCHOR = datetime(2026, 6, 3, 2, 0, 0, tzinfo=timezone.utc)
DEFAULT_ROOT = Path(__file__).resolve().parents[2] / "seed" / "demo-data"


def iso(dt: datetime) -> str:
    return dt.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def dstr(dt: datetime) -> str:
    return dt.strftime("%Y-%m-%d")


def f2(value: float | int) -> str:
    return f"{float(value):.2f}"


def maybe(value) -> str:
    if value is None:
        return ""
    return str(value)


def ensure_dirs(root: Path) -> None:
    for path in (root / "operational", root / "analytics", root / "support"):
        path.mkdir(parents=True, exist_ok=True)


def write_csv(path: Path, rows: list[dict], fieldnames: list[str]) -> None:
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for row in rows:
            writer.writerow({key: row.get(key, "") for key in fieldnames})


def segment(product_id: int) -> str:
    if product_id <= 15:
        return "BOOST"
    if product_id <= 35:
        return "REPLENISH"
    if product_id <= 53:
        return "MARKDOWN"
    if product_id <= 63:
        return "DO_NOT_ORDER"
    if product_id <= 100:
        return "WATCH"
    if product_id <= 110:
        return "INSUFFICIENT_DATA"
    return "FIX_DATA"


def category(product_id: int) -> dict[str, str]:
    categories = {
        1: {"name": "Patike", "pol": "Unisex", "materijal": "Tekstil", "brand": "DemoRun", "type": 1},
        2: {"name": "Sandale", "pol": "Zensko", "materijal": "Koza", "brand": "DemoWalk", "type": 2},
        3: {"name": "Cizme", "pol": "Musko", "materijal": "Nabuk", "brand": "DemoWarm", "type": 3},
        4: {"name": "Odeca", "pol": "Unisex", "materijal": "Pamuk", "brand": "DemoStyle", "type": 4},
    }
    return categories[((product_id - 1) % 4) + 1]


def supplier_id(product_id: int, seg: str) -> int | None:
    if seg == "BOOST":
        return 1 if product_id % 2 == 0 else 2
    if seg == "REPLENISH":
        return 1 if product_id % 3 == 0 else 3
    if seg == "MARKDOWN":
        return 4 if product_id % 2 == 0 else 5
    if seg == "DO_NOT_ORDER":
        return 5
    if seg == "WATCH":
        return 6 if product_id % 4 == 0 else ((product_id - 1) % 5) + 1
    if seg == "INSUFFICIENT_DATA":
        return 6
    return None if product_id in {111, 112, 113, 114} else 7


def cost_for(product_id: int, seg: str) -> float:
    base = {
        "BOOST": 4200 + ((product_id % 5) * 150),
        "REPLENISH": 3900 + ((product_id % 7) * 130),
        "MARKDOWN": 5100 + ((product_id % 6) * 180),
        "DO_NOT_ORDER": 3600 + ((product_id % 4) * 120),
        "WATCH": 3300 + ((product_id % 8) * 110),
        "INSUFFICIENT_DATA": 2800 + ((product_id % 5) * 100),
        "FIX_DATA": 0,
    }[seg]
    if product_id in {115, 116, 117, 120}:
        return 0
    return float(base)


def sale_price(cost: float) -> float:
    return round(cost * 1.8, 2) if cost > 0 else 0.0


def first_sale_price(cost: float) -> float:
    return round(cost * 1.55, 2) if cost > 0 else 0.0


def total_stock(product_id: int, seg: str) -> int:
    return {
        "BOOST": 4 + (product_id % 3),
        "REPLENISH": product_id % 2,
        "MARKDOWN": 18 + (product_id % 12),
        "DO_NOT_ORDER": 24 + (product_id % 16),
        "WATCH": 8 + (product_id % 8),
        "INSUFFICIENT_DATA": 2 + (product_id % 5),
        "FIX_DATA": product_id % 3,
    }[seg]


def min_stock(seg: str) -> int:
    return {
        "BOOST": 8,
        "REPLENISH": 6,
        "MARKDOWN": 5,
        "DO_NOT_ORDER": 10,
        "WATCH": 4,
        "INSUFFICIENT_DATA": 3,
        "FIX_DATA": 2,
    }[seg]


def velocity_for(product_id: int, seg: str) -> float:
    return {
        "BOOST": 1.45 + ((product_id % 4) * 0.12),
        "REPLENISH": 0.95 + ((product_id % 5) * 0.08),
        "MARKDOWN": 0.10 + ((product_id % 3) * 0.03),
        "DO_NOT_ORDER": 0.06 + ((product_id % 4) * 0.02),
        "WATCH": 0.28 + ((product_id % 6) * 0.05),
        "INSUFFICIENT_DATA": 0.05 + ((product_id % 4) * 0.01),
        "FIX_DATA": 0.02 + ((product_id % 2) * 0.01),
    }[seg]


def trend_for(product_id: int, seg: str) -> float:
    return {
        "BOOST": 0.82 + ((product_id % 4) * 0.03),
        "REPLENISH": 0.70 + ((product_id % 4) * 0.03),
        "MARKDOWN": 0.22 + ((product_id % 4) * 0.02),
        "DO_NOT_ORDER": 0.14 + ((product_id % 3) * 0.02),
        "WATCH": 0.48 + ((product_id % 5) * 0.02),
        "INSUFFICIENT_DATA": 0.30 + ((product_id % 3) * 0.02),
        "FIX_DATA": 0.18 + ((product_id % 2) * 0.01),
    }[seg]


def momentum_for(product_id: int, seg: str) -> float:
    return {
        "BOOST": 0.65 + ((product_id % 4) * 0.04),
        "REPLENISH": 0.40 + ((product_id % 4) * 0.04),
        "MARKDOWN": -0.10 + ((product_id % 4) * 0.02),
        "DO_NOT_ORDER": -0.18 + ((product_id % 3) * 0.02),
        "WATCH": 0.12 + ((product_id % 5) * 0.02),
        "INSUFFICIENT_DATA": 0.02 + ((product_id % 3) * 0.01),
        "FIX_DATA": -0.02 + ((product_id % 2) * 0.01),
    }[seg]


def recommended_qty(seg: str, product_id: int) -> int:
    return {
        "BOOST": 8 + (product_id % 12),
        "REPLENISH": 4 + (product_id % 8),
        "MARKDOWN": 0,
        "DO_NOT_ORDER": 0,
        "WATCH": 1 + (product_id % 4),
        "INSUFFICIENT_DATA": 0,
        "FIX_DATA": 0,
    }[seg]


def build(root: Path | None = None) -> None:
    root = root or DEFAULT_ROOT
    op = root / "operational"
    an = root / "analytics"
    sup = root / "support"
    ensure_dirs(root)

    stores = [
        {"StoreId": 1, "StoreName": "Demo City Center", "City": "Beograd", "Region": "Central", "Telefon": "+3811110001", "Menedzer": "Jelena Vukovic"},
        {"StoreId": 2, "StoreName": "Demo Shopping Park", "City": "Novi Sad", "Region": "North", "Telefon": "+3811110002", "Menedzer": "Marko Petrovic"},
        {"StoreId": 3, "StoreName": "Demo Outlet", "City": "Nis", "Region": "South", "Telefon": "+3811110003", "Menedzer": "Ivana Jovanovic"},
    ]
    footwear_types = [
        {"Id": 1, "Naziv": "Patike"},
        {"Id": 2, "Naziv": "Sandale"},
        {"Id": 3, "Naziv": "Cizme"},
        {"Id": 4, "Naziv": "Odeca"},
    ]
    seasons = [
        {"Id": 1, "Naziv": "Zima 2025/26", "DatumOd": "2025-11-01", "DatumDo": "2026-02-28"},
        {"Id": 2, "Naziv": "Prolece 2026", "DatumOd": "2026-03-01", "DatumDo": "2026-05-31"},
        {"Id": 3, "Naziv": "Leto 2026", "DatumOd": "2026-06-01", "DatumDo": "2026-08-31"},
        {"Id": 4, "Naziv": "Jesen 2026", "DatumOd": "2026-09-01", "DatumDo": "2026-11-30"},
    ]
    suppliers = [
        {"SupplierId": 1, "Naziv": "Demo Sport", "Adresa": "Beograd, Knez Mihailova 1", "Telefon": "+3811101001", "Napomena": "Strong supplier"},
        {"SupplierId": 2, "Naziv": "Balkan Comfort", "Adresa": "Novi Sad, Dunavska 5", "Telefon": "+3811101002", "Napomena": "Strong supplier"},
        {"SupplierId": 3, "Naziv": "Adriatic Steps", "Adresa": "Subotica, Korzo 3", "Telefon": "+3811101003", "Napomena": "Weak supplier"},
        {"SupplierId": 4, "Naziv": "Urban Motion", "Adresa": "Nis, Obrenoviceva 11", "Telefon": "+3811101004", "Napomena": "Weak supplier"},
        {"SupplierId": 5, "Naziv": "Atlas Trade", "Adresa": "Kragujevac, Laze Kostic 2", "Telefon": "+3811101005", "Napomena": "Fallback-only supplier"},
        {"SupplierId": 6, "Naziv": "Nova Obuca", "Adresa": "Cacak, Trg 7", "Telefon": "+3811101006", "Napomena": "New supplier with few sales"},
        {"SupplierId": 7, "Naziv": "", "Adresa": "Pancevo, Industrijska 9", "Telefon": "+3811101007", "Napomena": "Intentional missing supplier name"},
    ]

    products: list[dict] = []
    analytics_products: list[dict] = []
    stock_rows: list[dict] = []
    sales_headers: list[dict] = []
    sales_lines: list[dict] = []
    operational_moves: list[dict] = []
    analytics_moves: list[dict] = []
    inv_recs: list[dict] = []

    for product_id in range(1, 121):
        seg = segment(product_id)
        cat = category(product_id)
        supplier = supplier_id(product_id, seg)
        cost = cost_for(product_id, seg)
        sale = sale_price(cost)
        first_sale = first_sale_price(cost)
        stock_total = total_stock(product_id, seg)
        minimum = min_stock(seg)
        plu = f"DEMO-{product_id:04d}"
        if product_id in {119, 120}:
            plu = "DEMO-0118"

        missing_category = product_id in {115, 116, 117, 118}
        missing_supplier = product_id in {111, 112, 113, 114}
        if missing_supplier:
            supplier = None
        if missing_category:
            cat = {"name": "", "pol": "", "materijal": "", "brand": "DemoStyle", "type": None}

        updated_at = ANCHOR - timedelta(minutes=product_id)
        source_row_id = 100000 + product_id

        products.append(
            {
                "Id": product_id,
                "PLU": plu,
                "Naziv": f"Demo {cat['name']} {product_id:03d}",
                "IDTipObuce": maybe(cat["type"]),
                "IDDobavljac": maybe(supplier),
                "NabavnaCena": f2(0 if product_id in {115, 116, 117, 120} else cost),
                "NabavnaCenaDin": f2(0 if product_id in {115, 116, 117, 120} else round(cost * 117.0, 2)),
                "PrvaProdajnaCena": f2(0 if product_id in {115, 116, 117, 120} else first_sale),
                "ProdajnaCena": f2(0 if product_id in {115, 116, 117, 120} else sale),
                "Velicina": ["38", "39", "40", "41", "42", "43", "44"][product_id % 7],
                "Boja": ["Crna", "Bela", "Braon", "Plava", "Siva", "Crvena", "Zelena"][product_id % 7],
                "Kolicina": stock_total,
                "MinimalnaKolicina": minimum,
                "Komentar": f"Demo item {seg}",
                "IDObjekat": ((product_id - 1) % 3) + 1,
                "IDSezona": ((product_id - 1) % 4) + 1,
                "UpdatedAt": iso(updated_at),
                "Kategorija": maybe(cat["name"]),
                "Pol": maybe(cat["pol"]),
                "Materijal": maybe(cat["materijal"]),
                "DataOrigin": "access",
                "SourceTableKey": "demo_artikli",
                "SourceRowId": source_row_id,
                "SourceUpdatedAtUtc": iso(updated_at),
                "SourceHash": f"demo-art-{product_id:04d}",
                "SourceBatchId": 1001,
                "ImagePath": f"/demo/images/{plu}.jpg",
            }
        )

        analytics_products.append(
            {
                "ProductKey": product_id,
                "ProductId": product_id,
                "PLU": plu,
                "ProductName": f"Demo {cat['name']} {product_id:03d}",
                "Category": maybe(cat["name"]),
                "SubCategory": f"{cat['name']} premium" if cat["name"] else "",
                "Brand": cat["brand"],
                "Velicina": ["38", "39", "40", "41", "42", "43", "44"][product_id % 7],
                "Boja": ["Crna", "Bela", "Braon", "Plava", "Siva", "Crvena", "Zelena"][product_id % 7],
                "Materijal": cat["materijal"],
                "FootwearTypeId": maybe(cat["type"]),
                "SupplierId": maybe(supplier),
                "SeasonId": ((product_id - 1) % 4) + 1,
                "PurchasePrice": f2(0 if product_id in {115, 116, 117, 120} else cost),
                "PurchasePriceRsd": f2(0 if product_id in {115, 116, 117, 120} else round(cost * 117.0, 2)),
                "FirstSalePrice": f2(0 if product_id in {115, 116, 117, 120} else first_sale),
                "SalePrice": f2(0 if product_id in {115, 116, 117, 120} else sale),
                "IsActive": "True",
                "Timestamp": iso(ANCHOR),
                "Kolicina": stock_total,
                "MinimalnaKolicina": minimum,
                "DataOrigin": "access",
            }
        )

        for store_id, store in enumerate(stores, start=1):
            store_stock = {
                "BOOST": 2 + ((product_id + store_id) % 3),
                "REPLENISH": (product_id + store_id) % 2,
                "MARKDOWN": 12 + ((product_id + store_id) % 9),
                "DO_NOT_ORDER": 18 + ((product_id + store_id) % 14),
                "WATCH": 5 + ((product_id + store_id) % 7),
                "INSUFFICIENT_DATA": 1 + ((product_id + store_id) % 4),
                "FIX_DATA": (product_id + store_id) % 2,
            }[seg]
            risk = "OOS risk" if store_stock <= 1 else ("Dead stock" if store_stock >= 18 else "Healthy")
            stock_rows.append(
                {
                    "ProductId": product_id,
                    "StoreId": store_id,
                    "StoreName": store["StoreName"],
                    "StockOnHand": store_stock,
                    "MinimumStock": minimum,
                    "RiskLabel": risk,
                }
            )

        v = velocity_for(product_id, seg)
        trend = trend_for(product_id, seg)
        momentum = momentum_for(product_id, seg)
        inv_recs.append(
            {
                "Id": product_id,
                "SnapshotDate": dstr(ANCHOR),
                "ProductId": str(product_id),
                "Brand": cat["brand"],
                "Category": maybe(cat["name"]),
                "SalesVelocity": f2(v),
                "StockOnHand": f2(float(stock_total)),
                "TrendScore": f2(trend),
                "MomentumScore": f2(momentum),
                "RecommendedQty": recommended_qty(seg, product_id),
                "CreatedAt": iso(ANCHOR),
            }
        )

    sale_line_id = 1
    movement_id = 1
    start_date = ANCHOR.date() - timedelta(days=179)
    for day in range(180):
        sale_date = start_date + timedelta(days=day)
        sale_dt_base = datetime.combine(sale_date, datetime.min.time(), tzinfo=timezone.utc)
        for store_id in range(1, 4):
            sale_id = day * 3 + store_id
            header_ts = sale_dt_base + timedelta(hours=8 + store_id, minutes=(day + store_id) % 50)
            payment_type = "card" if (sale_id + store_id) % 2 == 0 else "cash"
            line_products = [
                ((day * 5) + (store_id * 7)) % 120 + 1,
                ((day * 5) + (store_id * 7) + 17) % 120 + 1,
                ((day * 5) + (store_id * 7) + 31) % 120 + 1,
            ]
            line_total = 0.0
            total_units = 0
            for idx, product_id in enumerate(line_products):
                product = products[product_id - 1]
                qty = 2 if product_id % 5 == 0 else 1
                unit_price = float(product["ProdajnaCena"]) if product["ProdajnaCena"] else 0.0
                if unit_price == 0:
                    unit_price = round(float(product["NabavnaCena"]) * 1.5, 2)
                if idx == 2 and sale_id % 60 == 0:
                    unit_price = 0.0
                    qty = 1
                amount = round(unit_price * qty, 2)
                line_total += amount
                total_units += qty
                sales_lines.append(
                    {
                        "Id": sale_line_id,
                        "SaleId": sale_id,
                        "ProductId": product_id,
                        "Qty": qty,
                        "UnitPrice": f2(unit_price),
                        "LineTotal": f2(amount),
                        "NabavnaCena": product["NabavnaCena"],
                        "DataOrigin": "access",
                    }
                )
                sale_line_id += 1

            sales_headers.append(
                {
                    "Id": sale_id,
                    "SaleId": sale_id,
                    "BrojRacuna": f"R-{sale_date:%Y%m%d}-{store_id}-{sale_id:05d}",
                    "SaleTimestampUtc": iso(header_ts),
                    "StoreId": store_id,
                    "PaymentType": payment_type,
                    "TotalAmount": f2(line_total),
                    "TotalUnits": total_units,
                    "TotalLines": 3,
                    "DataOrigin": "access",
                }
            )

    markdown_specs = [
        {"ProductId": 12, "TipPromene": "Nivelacija cena", "Old": 6890, "New": 5990, "Qty": 6, "StoreId": 1, "Doc": "MD-001", "Note": "Demo markdown on slowing sneaker", "Supplier": 4},
        {"ProductId": 27, "TipPromene": "Nivelacija", "Old": 5490, "New": 4990, "Qty": 4, "StoreId": 2, "Doc": "NV-002", "Note": "Seasonal price correction", "Supplier": 4},
        {"ProductId": 44, "TipPromene": "Nivelacija cena", "Old": 7390, "New": 6490, "Qty": 8, "StoreId": 3, "Doc": "MD-003", "Note": "Outlet markdown", "Supplier": 5},
        {"ProductId": 68, "TipPromene": "UlazRobe", "Old": 0, "New": 0, "Qty": 24, "StoreId": 1, "Doc": "UL-004", "Note": "Replenishment inbound", "Supplier": 1},
        {"ProductId": 81, "TipPromene": "UlazRobe", "Old": 0, "New": 0, "Qty": 18, "StoreId": 2, "Doc": "UL-005", "Note": "Replenishment inbound", "Supplier": 1},
        {"ProductId": 92, "TipPromene": "PovratKupca", "Old": 0, "New": 0, "Qty": 2, "StoreId": 3, "Doc": "PV-006", "Note": "Customer return", "Supplier": 4},
    ]

    for spec in markdown_specs:
        movement_ts = ANCHOR - timedelta(days=movement_id)
        amount = spec["Qty"] * 250
        operational_moves.append(
            {
                "Id": movement_id,
                "TipPromene": spec["TipPromene"],
                "Datum": iso(movement_ts),
                "Iznos": f2(amount),
                "BrojRacuna": spec["Doc"],
                "DobavljacId": spec["Supplier"],
                "ArtikalId": spec["ProductId"],
                "StaraProdajnaCena": f2(spec["Old"]) if spec["Old"] else "",
                "NovaProdajnaCena": f2(spec["New"]) if spec["New"] else "",
                "Kolicina": spec["Qty"],
                "IDObjekat": spec["StoreId"],
                "RedniBroj": movement_id,
                "Komentar": spec["Note"],
                "KorisnikIme": "demo-operator",
                "DataOrigin": "access",
                "SourceTableKey": "demo_movements",
                "SourceRowId": 200000 + movement_id,
                "SourceUpdatedAtUtc": iso(movement_ts),
                "SourceHash": f"demo-mov-{movement_id:04d}",
                "SourceBatchId": 1001,
            }
        )
        analytics_moves.append(
            {
                "Id": movement_id,
                "SourceId": movement_id,
                "TipPromene": spec["TipPromene"],
                "Datum": iso(movement_ts),
                "ArtikalId": spec["ProductId"],
                "Kolicina": spec["Qty"],
                "StaraProdajnaCena": f2(spec["Old"]) if spec["Old"] else "",
                "NovaProdajnaCena": f2(spec["New"]) if spec["New"] else "",
                "Iznos": f2(amount),
                "StoreId": spec["StoreId"],
                "DobavljacId": spec["Supplier"],
                "BrojDokumenta": spec["Doc"],
                "KorisnikIme": "demo-operator",
                "DataOrigin": "access",
            }
        )
        movement_id += 1

    action_items = [
        {
            "Id": 1,
            "SourceType": "inventory",
            "SourceKey": "dopuna:12:1",
            "SourceId": 12,
            "Title": "Dopuni SKU 12",
            "Description": "SKU 12 je u OOS riziku i treba dopunu.",
            "RecommendationStatus": "Preporuceno",
            "Priority": "P1",
            "ImpactEstimateRsd": "18500",
            "DueAtUtc": iso(ANCHOR + timedelta(days=3)),
            "ExpectedImpactRsd": "18500",
            "MeasuredImpactRsd": "",
            "OutcomeStatus": "pending",
            "OutcomeMeasuredAtUtc": "",
            "OutcomeNotes": "",
            "ConfidencePct": 92,
            "ReliabilityPct": 88,
            "DataQualityStatus": "good",
            "Status": "new",
            "ActionUrl": "/analytics/inventory?productId=12",
            "MetadataJson": json.dumps({"source": "inventory", "reason": "OOS risk"}, ensure_ascii=False),
            "CreatedAtUtc": iso(ANCHOR - timedelta(hours=23)),
            "UpdatedAtUtc": iso(ANCHOR - timedelta(hours=17)),
            "ResolvedAtUtc": "",
            "CreatedByUserId": "demo-user",
            "UpdatedByUserId": "demo-user",
            "UpdatedByUserName": "Demo Operator",
        },
        {
            "Id": 2,
            "SourceType": "supplier",
            "SourceKey": "supplier:1:reorder",
            "SourceId": 1,
            "Title": "Pregled dobavljaca Demo Sport",
            "Description": "High confidence supplier with strong margin needs a review.",
            "RecommendationStatus": "Preporuceno",
            "Priority": "P2",
            "ImpactEstimateRsd": "12400",
            "DueAtUtc": iso(ANCHOR + timedelta(days=4)),
            "ExpectedImpactRsd": "12400",
            "MeasuredImpactRsd": "6200",
            "OutcomeStatus": "accepted",
            "OutcomeMeasuredAtUtc": "",
            "OutcomeNotes": "",
            "ConfidencePct": 84,
            "ReliabilityPct": 81,
            "DataQualityStatus": "good",
            "Status": "accepted",
            "ActionUrl": "/analytics/supplier?supplierId=1",
            "MetadataJson": json.dumps({"source": "supplier", "reason": "margin review"}, ensure_ascii=False),
            "CreatedAtUtc": iso(ANCHOR - timedelta(hours=22)),
            "UpdatedAtUtc": iso(ANCHOR - timedelta(hours=16)),
            "ResolvedAtUtc": "",
            "CreatedByUserId": "demo-user",
            "UpdatedByUserId": "demo-user",
            "UpdatedByUserName": "Demo Operator",
        },
        {
            "Id": 3,
            "SourceType": "data_quality",
            "SourceKey": "dq:missing-cost:115",
            "SourceId": 115,
            "Title": "Ispravi nulti trosak za SKU 115",
            "Description": "Missing cost price blocks margin calculations.",
            "RecommendationStatus": "Blokada",
            "Priority": "P1",
            "ImpactEstimateRsd": "0",
            "DueAtUtc": iso(ANCHOR + timedelta(days=5)),
            "ExpectedImpactRsd": "0",
            "MeasuredImpactRsd": "",
            "OutcomeStatus": "",
            "OutcomeMeasuredAtUtc": "",
            "OutcomeNotes": "Missing cost requires correction before action planning.",
            "ConfidencePct": 100,
            "ReliabilityPct": 100,
            "DataQualityStatus": "critical",
            "Status": "deferred",
            "ActionUrl": "/analytics/data-quality",
            "MetadataJson": json.dumps({"source": "data_quality", "reason": "missing cost"}, ensure_ascii=False),
            "CreatedAtUtc": iso(ANCHOR - timedelta(hours=21)),
            "UpdatedAtUtc": iso(ANCHOR - timedelta(hours=15)),
            "ResolvedAtUtc": "",
            "CreatedByUserId": "demo-user",
            "UpdatedByUserId": "demo-user",
            "UpdatedByUserName": "Demo Operator",
        },
        {
            "Id": 4,
            "SourceType": "dashboard",
            "SourceKey": "dashboard:pilot-summary",
            "SourceId": "",
            "Title": "Pripremi pilot dashboard",
            "Description": "Executive dashboard summary for demo mode.",
            "RecommendationStatus": "Informativno",
            "Priority": "P3",
            "ImpactEstimateRsd": "0",
            "DueAtUtc": iso(ANCHOR + timedelta(days=6)),
            "ExpectedImpactRsd": "0",
            "MeasuredImpactRsd": "",
            "OutcomeStatus": "done",
            "OutcomeMeasuredAtUtc": iso(ANCHOR - timedelta(hours=10)),
            "OutcomeNotes": "Demo action completed",
            "ConfidencePct": 75,
            "ReliabilityPct": 73,
            "DataQualityStatus": "warning",
            "Status": "done",
            "ActionUrl": "/analytics",
            "MetadataJson": json.dumps({"source": "dashboard", "reason": "demo"}, ensure_ascii=False),
            "CreatedAtUtc": iso(ANCHOR - timedelta(hours=20)),
            "UpdatedAtUtc": iso(ANCHOR - timedelta(hours=14)),
            "ResolvedAtUtc": iso(ANCHOR - timedelta(hours=8)),
            "CreatedByUserId": "demo-user",
            "UpdatedByUserId": "demo-user",
            "UpdatedByUserName": "Demo Operator",
        },
    ]

    refresh_runs = [
        {
            "Id": 1,
            "JobKey": "nightly_refresh",
            "JobName": "Nightly analytics refresh",
            "Status": "succeeded",
            "StartedAtUtc": iso(ANCHOR - timedelta(hours=22)),
            "FinishedAtUtc": iso(ANCHOR - timedelta(hours=21, minutes=45)),
            "DurationSeconds": "900",
            "RefreshedObjectsJson": json.dumps(["dashboard", "supplier-decision", "inventory", "data-quality", "reports", "actions"], ensure_ascii=False),
            "FailedObjectsJson": "",
            "ErrorCode": "",
            "ErrorMessage": "",
            "CorrelationId": "demo-refresh-001",
            "TriggeredBy": "nightly",
            "ProcessMode": "worker",
            "WorkerName": "AnalyticsRefreshWorker",
            "CreatedAtUtc": iso(ANCHOR - timedelta(hours=22)),
        },
        {
            "Id": 2,
            "JobKey": "manual_refresh",
            "JobName": "Manual pilot refresh",
            "Status": "failed",
            "StartedAtUtc": iso(ANCHOR - timedelta(days=2)),
            "FinishedAtUtc": iso(ANCHOR - timedelta(days=2, minutes=-8)),
            "DurationSeconds": "480",
            "RefreshedObjectsJson": json.dumps(["data-quality"], ensure_ascii=False),
            "FailedObjectsJson": json.dumps(["supplier-decision"], ensure_ascii=False),
            "ErrorCode": "supplier_decision_timeout",
            "ErrorMessage": "Supplier decision cache warm-up timed out in demo state.",
            "CorrelationId": "demo-refresh-002",
            "TriggeredBy": "manual",
            "ProcessMode": "worker",
            "WorkerName": "AnalyticsRefreshWorker",
            "CreatedAtUtc": iso(ANCHOR - timedelta(days=2)),
        },
    ]

    import_batches = [
        {
            "Id": 1001,
            "SourceSystem": "access",
            "SourceFileName": "demo_mode_access_import.mdb",
            "SourceFilePath": "seed/demo-data/operational/demo_mode_access_import.mdb",
            "SourceStorageKey": "demo/demo_mode_access_import.mdb",
            "SourceStorageProvider": "local",
            "QueuedAtUtc": iso(ANCHOR - timedelta(hours=26)),
            "StartedAtUtc": iso(ANCHOR - timedelta(hours=25, minutes=30)),
            "CompletedAtUtc": iso(ANCHOR - timedelta(hours=24)),
            "LastHeartbeatUtc": iso(ANCHOR - timedelta(hours=24, minutes=10)),
            "Status": "completed",
            "CurrentStep": "complete",
            "CurrentTable": "prodaja_stavke",
            "SummaryJson": json.dumps({"rowsRead": 2303, "rowsWritten": 2303, "issues": 5}, ensure_ascii=False),
            "ErrorMessage": "",
            "ErrorDetailsJson": "",
            "RequestedBy": "demo-operator",
            "ImportMode": "manual",
            "ImportStrategy": "full",
            "IncludeAnalytics": True,
            "OverwriteExisting": True,
            "IncludeTemporaryTables": False,
            "SkipInvalidForeignKeys": True,
            "CancellationRequested": False,
            "CancellationRequestedAtUtc": "",
            "RetryCount": 0,
            "ProgressPercent": 100,
            "RowsRead": 2303,
            "RowsAccepted": 2303,
            "RowsWritten": 2303,
            "IsIncremental": False,
            "CursorSnapshot": "",
            "CursorBeforeJson": "",
            "CursorAfterJson": "",
            "ProcessedRowCount": 2303,
            "SkippedRowCount": 0,
            "RowsInserted": 2303,
            "RowsUpdated": 0,
            "RowsUnchanged": 0,
            "RowsStaged": 0,
            "RowsSkippedStale": 0,
            "RowsRejected": 0,
            "ShadowMismatchCount": 0,
            "SourceFileHash": "demo-import-hash-001",
            "DurationSeconds": 1800,
            "TotalImported": 2303,
            "TotalUpdated": 0,
            "TotalErrors": 0,
            "DataOrigin": "access",
        },
        {
            "Id": 1002,
            "SourceSystem": "access",
            "SourceFileName": "demo_mode_access_import_failed.mdb",
            "SourceFilePath": "seed/demo-data/operational/demo_mode_access_import_failed.mdb",
            "SourceStorageKey": "demo/demo_mode_access_import_failed.mdb",
            "SourceStorageProvider": "local",
            "QueuedAtUtc": iso(ANCHOR - timedelta(days=3)),
            "StartedAtUtc": iso(ANCHOR - timedelta(days=3, hours=-1)),
            "CompletedAtUtc": "",
            "LastHeartbeatUtc": iso(ANCHOR - timedelta(days=3, hours=-2)),
            "Status": "failed",
            "CurrentStep": "import_sales",
            "CurrentTable": "prodaja_stavke",
            "SummaryJson": json.dumps({"rowsRead": 62, "rowsWritten": 0, "issues": 1}, ensure_ascii=False),
            "ErrorMessage": "Intentional demo import failure.",
            "ErrorDetailsJson": json.dumps({"code": "demo_failure"}, ensure_ascii=False),
            "RequestedBy": "demo-operator",
            "ImportMode": "manual",
            "ImportStrategy": "full",
            "IncludeAnalytics": True,
            "OverwriteExisting": True,
            "IncludeTemporaryTables": False,
            "SkipInvalidForeignKeys": True,
            "CancellationRequested": False,
            "CancellationRequestedAtUtc": "",
            "RetryCount": 0,
            "ProgressPercent": 18,
            "RowsRead": 62,
            "RowsAccepted": 0,
            "RowsWritten": 0,
            "IsIncremental": False,
            "CursorSnapshot": "",
            "CursorBeforeJson": "",
            "CursorAfterJson": "",
            "ProcessedRowCount": 62,
            "SkippedRowCount": 62,
            "RowsInserted": 0,
            "RowsUpdated": 0,
            "RowsUnchanged": 0,
            "RowsStaged": 0,
            "RowsSkippedStale": 0,
            "RowsRejected": 62,
            "ShadowMismatchCount": 0,
            "SourceFileHash": "demo-import-hash-002",
            "DurationSeconds": 300,
            "TotalImported": 0,
            "TotalUpdated": 0,
            "TotalErrors": 1,
            "DataOrigin": "access",
        },
    ]

    dq_issues = [
        {"IssueType": "missing_supplier", "ProductId": 111, "Severity": "critical", "Description": "Product 111 has no supplier assigned."},
        {"IssueType": "missing_cost", "ProductId": 115, "Severity": "critical", "Description": "Product 115 has a zero cost price."},
        {"IssueType": "missing_category", "ProductId": 116, "Severity": "warning", "Description": "Product 116 has no category."},
        {"IssueType": "duplicate_plu", "ProductId": 119, "Severity": "warning", "Description": "Product 119 reuses PLU DEMO-0118."},
        {"IssueType": "zero_revenue_line", "ProductId": 12, "Severity": "warning", "Description": "A few sales lines have zero revenue to simulate returns."},
    ]

    write_csv(
        op / "Dobavljaci.csv",
        [
            {"Id": s["SupplierId"], "Naziv": s["Naziv"], "Adresa": s["Adresa"], "Telefon": s["Telefon"], "Napomena": s["Napomena"], "DataOrigin": "access"}
            for s in suppliers
        ],
        ["Id", "Naziv", "Adresa", "Telefon", "Napomena", "DataOrigin"],
    )
    write_csv(
        op / "TipoviObuce.csv",
        [{"Id": t["Id"], "Naziv": t["Naziv"], "DataOrigin": "access"} for t in footwear_types],
        ["Id", "Naziv", "DataOrigin"],
    )
    write_csv(
        op / "Sezone.csv",
        [{"Id": s["Id"], "Naziv": s["Naziv"], "DatumOd": s["DatumOd"], "DatumDo": s["DatumDo"], "DataOrigin": "access"} for s in seasons],
        ["Id", "Naziv", "DatumOd", "DatumDo", "DataOrigin"],
    )
    write_csv(
        op / "Artikli.csv",
        products,
        ["Id", "PLU", "Naziv", "IDTipObuce", "IDDobavljac", "NabavnaCena", "NabavnaCenaDin", "PrvaProdajnaCena", "ProdajnaCena", "Velicina", "Boja", "Kolicina", "MinimalnaKolicina", "Komentar", "IDObjekat", "IDSezona", "UpdatedAt", "Kategorija", "Pol", "Materijal", "DataOrigin", "SourceTableKey", "SourceRowId", "SourceUpdatedAtUtc", "SourceHash", "SourceBatchId", "ImagePath"],
    )
    write_csv(
        op / "prodaja_zaglavlje.csv",
        sales_headers,
        ["Id", "SaleId", "BrojRacuna", "SaleTimestampUtc", "StoreId", "PaymentType", "TotalAmount", "TotalUnits", "TotalLines", "DataOrigin"],
    )
    write_csv(
        op / "prodaja_stavke.csv",
        sales_lines,
        ["Id", "SaleId", "ProductId", "Qty", "UnitPrice", "LineTotal", "NabavnaCena", "DataOrigin"],
    )
    write_csv(
        op / "DnevnikPromena.csv",
        operational_moves,
        ["Id", "TipPromene", "Datum", "Iznos", "BrojRacuna", "DobavljacId", "ArtikalId", "StaraProdajnaCena", "NovaProdajnaCena", "Kolicina", "IDObjekat", "RedniBroj", "Komentar", "KorisnikIme", "DataOrigin", "SourceTableKey", "SourceRowId", "SourceUpdatedAtUtc", "SourceHash", "SourceBatchId"],
    )
    write_csv(
        op / "DataImportBatches.csv",
        import_batches,
        ["Id", "SourceSystem", "SourceFileName", "SourceFilePath", "SourceStorageKey", "SourceStorageProvider", "QueuedAtUtc", "StartedAtUtc", "CompletedAtUtc", "LastHeartbeatUtc", "Status", "CurrentStep", "CurrentTable", "SummaryJson", "ErrorMessage", "ErrorDetailsJson", "RequestedBy", "ImportMode", "ImportStrategy", "IncludeAnalytics", "OverwriteExisting", "IncludeTemporaryTables", "SkipInvalidForeignKeys", "CancellationRequested", "CancellationRequestedAtUtc", "RetryCount", "ProgressPercent", "RowsRead", "RowsAccepted", "RowsWritten", "IsIncremental", "CursorSnapshot", "CursorBeforeJson", "CursorAfterJson", "ProcessedRowCount", "SkippedRowCount", "RowsInserted", "RowsUpdated", "RowsUnchanged", "RowsStaged", "RowsSkippedStale", "RowsRejected", "ShadowMismatchCount", "SourceFileHash", "DurationSeconds", "TotalImported", "TotalUpdated", "TotalErrors", "DataOrigin"],
    )

    write_csv(
        an / "StoresDim.csv",
        [{"StoreKey": s["StoreId"], "StoreId": s["StoreId"], "StoreName": s["StoreName"], "City": s["City"], "Region": s["Region"], "Telefon": s["Telefon"], "Menedzer": s["Menedzer"], "DataOrigin": "access"} for s in stores],
        ["StoreKey", "StoreId", "StoreName", "City", "Region", "Telefon", "Menedzer", "DataOrigin"],
    )
    write_csv(
        an / "SuppliersDim.csv",
        [{"SupplierKey": s["SupplierId"], "SupplierId": s["SupplierId"], "Naziv": s["Naziv"], "Adresa": s["Adresa"], "Telefon": s["Telefon"], "Napomena": s["Napomena"], "DataOrigin": "access", "UpdatedAt": iso(ANCHOR)} for s in suppliers],
        ["SupplierKey", "SupplierId", "Naziv", "Adresa", "Telefon", "Napomena", "DataOrigin", "UpdatedAt"],
    )
    write_csv(
        an / "FootwearTypesDim.csv",
        [{"TypeKey": t["Id"], "TypeId": t["Id"], "Naziv": t["Naziv"], "DataOrigin": "access", "UpdatedAt": iso(ANCHOR)} for t in footwear_types],
        ["TypeKey", "TypeId", "Naziv", "DataOrigin", "UpdatedAt"],
    )
    write_csv(
        an / "SeasonsDim.csv",
        [{"SeasonKey": s["Id"], "SeasonId": s["Id"], "Naziv": s["Naziv"], "DatumOd": s["DatumOd"], "DatumDo": s["DatumDo"], "DataOrigin": "access", "UpdatedAt": iso(ANCHOR)} for s in seasons],
        ["SeasonKey", "SeasonId", "Naziv", "DatumOd", "DatumDo", "DataOrigin", "UpdatedAt"],
    )
    write_csv(
        an / "ProductsDim.csv",
        analytics_products,
        ["ProductKey", "ProductId", "PLU", "ProductName", "Category", "SubCategory", "Brand", "Velicina", "Boja", "Materijal", "FootwearTypeId", "SupplierId", "SeasonId", "PurchasePrice", "PurchasePriceRsd", "FirstSalePrice", "SalePrice", "IsActive", "Timestamp", "Kolicina", "MinimalnaKolicina", "DataOrigin"],
    )
    write_csv(
        an / "SalesFacts.csv",
        sales_headers,
        ["Id", "SaleId", "BrojRacuna", "SaleTimestampUtc", "StoreId", "PaymentType", "TotalAmount", "TotalUnits", "TotalLines", "DataOrigin"],
    )
    write_csv(
        an / "SalesLineFacts.csv",
        sales_lines,
        ["Id", "SaleId", "ProductId", "Qty", "UnitPrice", "LineTotal", "NabavnaCena", "DataOrigin"],
    )
    write_csv(
        an / "InventoryMovementFacts.csv",
        analytics_moves,
        ["Id", "SourceId", "TipPromene", "Datum", "ArtikalId", "Kolicina", "StaraProdajnaCena", "NovaProdajnaCena", "Iznos", "StoreId", "DobavljacId", "BrojDokumenta", "KorisnikIme", "DataOrigin"],
    )
    write_csv(
        an / "InventoryRecommendations.csv",
        inv_recs,
        ["Id", "SnapshotDate", "ProductId", "Brand", "Category", "SalesVelocity", "StockOnHand", "TrendScore", "MomentumScore", "RecommendedQty", "CreatedAt"],
    )
    write_csv(
        an / "AnalyticsRefreshRuns.csv",
        refresh_runs,
        ["Id", "JobKey", "JobName", "Status", "StartedAtUtc", "FinishedAtUtc", "DurationSeconds", "RefreshedObjectsJson", "FailedObjectsJson", "ErrorCode", "ErrorMessage", "CorrelationId", "TriggeredBy", "ProcessMode", "WorkerName", "CreatedAtUtc"],
    )
    write_csv(
        an / "AnalyticsActionItems.csv",
        action_items,
        ["Id", "SourceType", "SourceKey", "SourceId", "Title", "Description", "RecommendationStatus", "Priority", "ImpactEstimateRsd", "DueAtUtc", "ExpectedImpactRsd", "MeasuredImpactRsd", "OutcomeStatus", "OutcomeMeasuredAtUtc", "OutcomeNotes", "ConfidencePct", "ReliabilityPct", "DataQualityStatus", "Status", "ActionUrl", "MetadataJson", "CreatedAtUtc", "UpdatedAtUtc", "ResolvedAtUtc", "CreatedByUserId", "UpdatedByUserId", "UpdatedByUserName"],
    )

    write_csv(
        sup / "data_quality_issues.csv",
        dq_issues,
        ["IssueType", "ProductId", "Severity", "Description"],
    )
    write_csv(
        sup / "stock.csv",
        stock_rows,
        ["ProductId", "StoreId", "StoreName", "StockOnHand", "MinimumStock", "RiskLabel"],
    )

    manifest = {
        "name": "Trendplus Demo Dataset",
        "demoOnly": True,
        "warning": "Demo podaci - ne koristiti za poslovne odluke.",
        "anchorUtc": iso(ANCHOR),
        "counts": {
            "suppliers": len(suppliers),
            "products": len(products),
            "stores": len(stores),
            "salesDays": 180,
            "salesHeaders": len(sales_headers),
            "salesLines": len(sales_lines),
            "stockRows": len(stock_rows),
            "markdownEvents": len(operational_moves),
            "dataQualityIssues": len(dq_issues),
            "refreshRuns": len(refresh_runs),
            "importBatches": len(import_batches),
            "actionItems": len(action_items),
        },
    }
    (sup / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-root", dest="output_root", default=None)
    args = parser.parse_args()
    output_root = Path(args.output_root).resolve() if args.output_root else DEFAULT_ROOT
    output_root.mkdir(parents=True, exist_ok=True)
    build(output_root)
    print(f"Generated demo dataset under {output_root}")
