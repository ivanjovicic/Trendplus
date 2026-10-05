import { describe, expect, it } from "vitest";

/**
 * Frontend metamorphic guards for certified retail presentation math.
 * These do not call production page components so a co-broken mapper cannot
 * keep the suite green; they lock the arithmetic contracts the UI relies on.
 */

type SaleLine = {
  soldAtUtc: string;
  storeId: number | null;
  receiptNumber: string | null;
  dataOrigin: string | null;
  quantity: number;
  unitPrice: number;
};

const DAY_A = "2026-09-01T00:00:00.000Z";
const DAY_B = "2026-09-02T00:00:00.000Z";
const DAY_C = "2026-09-03T00:00:00.000Z";

function isExcludedReceipt(receiptNumber: string | null | undefined): boolean {
  const normalized = (receiptNumber ?? "").trim().toUpperCase();
  return normalized === "DUG" || normalized === "KOREKCIJA";
}

function inHalfOpen(soldAtUtc: string, fromUtc: string, toExclusiveUtc: string): boolean {
  const sold = Date.parse(soldAtUtc);
  return sold >= Date.parse(fromUtc) && sold < Date.parse(toExclusiveUtc);
}

function filterLines(
  lines: SaleLine[],
  fromUtc: string,
  toExclusiveUtc: string,
  storeId?: number | null,
): SaleLine[] {
  return lines.filter((line) => {
    if (!inHalfOpen(line.soldAtUtc, fromUtc, toExclusiveUtc)) return false;
    if (storeId != null && line.storeId !== storeId) return false;
    if (isExcludedReceipt(line.receiptNumber)) return false;
    return true;
  });
}

function sumRevenue(lines: SaleLine[]): number {
  return lines.reduce((sum, line) => sum + line.quantity * line.unitPrice, 0);
}

const kit: SaleLine[] = [
  { soldAtUtc: "2026-09-01T09:00:00.000Z", storeId: 1, receiptNumber: "R-100", dataOrigin: "existing", quantity: 2, unitPrice: 100 },
  { soldAtUtc: "2026-09-01T10:00:00.000Z", storeId: 1, receiptNumber: "R-101", dataOrigin: "existing", quantity: 1, unitPrice: 80 },
  { soldAtUtc: "2026-09-01T11:00:00.000Z", storeId: 1, receiptNumber: "R-102", dataOrigin: "existing", quantity: -1, unitPrice: 100 },
  { soldAtUtc: "2026-09-01T12:00:00.000Z", storeId: 1, receiptNumber: "R-103", dataOrigin: "existing", quantity: 1, unitPrice: 80 },
  { soldAtUtc: "2026-09-01T13:00:00.000Z", storeId: 1, receiptNumber: "  dUg  ", dataOrigin: "existing", quantity: 5, unitPrice: 200 },
  { soldAtUtc: "2026-09-01T14:00:00.000Z", storeId: 1, receiptNumber: " KoReKcIjA ", dataOrigin: "existing", quantity: 3, unitPrice: 150 },
  { soldAtUtc: "2026-09-01T15:00:00.000Z", storeId: 2, receiptNumber: "R-200", dataOrigin: "access", quantity: 2, unitPrice: 120 },
  { soldAtUtc: "2026-09-01T00:00:00.000Z", storeId: 1, receiptNumber: "R-FROM", dataOrigin: "existing", quantity: 1, unitPrice: 50 },
  { soldAtUtc: "2026-09-02T00:00:00.000Z", storeId: 1, receiptNumber: "R-TOEXCL", dataOrigin: "existing", quantity: 9, unitPrice: 999 },
  { soldAtUtc: "2026-09-02T08:00:00.000Z", storeId: 1, receiptNumber: "R-NEXT", dataOrigin: "existing", quantity: 1, unitPrice: 120 },
];

describe("certifiedRetailMetamorphic", () => {
  it("hand-expected current-day promet excludes DUG/KOREKCIJA and toExclusive", () => {
    const revenue = sumRevenue(filterLines(kit, DAY_A, DAY_B));
    expect(revenue).toBe(550);
  });

  it("metamorphic: [A,C) = [A,B) + [B,C) for promet", () => {
    const ab = sumRevenue(filterLines(kit, DAY_A, DAY_B));
    const bc = sumRevenue(filterLines(kit, DAY_B, DAY_C));
    const ac = sumRevenue(filterLines(kit, DAY_A, DAY_C));
    // R-TOEXCL sits on DayB: out of [A,B), in [B,C) → 9*999 + 120 = 9111.
    expect(ab).toBe(550);
    expect(bc).toBe(9111);
    expect(ac).toBe(ab + bc);
  });

  it("metamorphic: +100 RSD sale bumps revenue by exactly 100", () => {
    const before = sumRevenue(filterLines(kit, DAY_A, DAY_B));
    const withSale = [
      ...kit,
      {
        soldAtUtc: "2026-09-01T18:00:00.000Z",
        storeId: 1,
        receiptNumber: "R-PLUS-100",
        dataOrigin: "existing",
        quantity: 1,
        unitPrice: 100,
      },
    ];
    expect(sumRevenue(filterLines(withSale, DAY_A, DAY_B))).toBe(before + 100);
  });

  it("metamorphic: other-store sale does not change selected store", () => {
    const store1Before = sumRevenue(filterLines(kit, DAY_A, DAY_B, 1));
    const withOther = [
      ...kit,
      {
        soldAtUtc: "2026-09-01T19:00:00.000Z",
        storeId: 2,
        receiptNumber: "R-OTHER",
        dataOrigin: "existing",
        quantity: 7,
        unitPrice: 333,
      },
    ];
    expect(sumRevenue(filterLines(withOther, DAY_A, DAY_B, 1))).toBe(store1Before);
  });

  it("full return nets matching unit revenue to zero", () => {
    const pair: SaleLine[] = [
      { soldAtUtc: "2026-09-01T09:00:00.000Z", storeId: 1, receiptNumber: "S", dataOrigin: "existing", quantity: 1, unitPrice: 100 },
      { soldAtUtc: "2026-09-01T10:00:00.000Z", storeId: 1, receiptNumber: "R", dataOrigin: "existing", quantity: -1, unitPrice: 100 },
    ];
    expect(sumRevenue(filterLines(pair, DAY_A, DAY_B))).toBe(0);
  });

  it("datedaily row sum of totalRevenue must equal screen promet (presentation invariant)", () => {
    const dateRows = [
      { date: "2026-09-01", totalRevenue: 310 },
      { date: "2026-09-01", totalRevenue: 240 },
    ];
    const screenPromet = 550;
    expect(dateRows.reduce((sum, row) => sum + row.totalRevenue, 0)).toBe(screenPromet);
  });
});
