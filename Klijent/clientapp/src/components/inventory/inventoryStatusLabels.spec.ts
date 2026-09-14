import { describe, expect, it } from "vitest";
import {
  inventoryActionStatusLabel,
  inventoryActionTypeLabel,
  inventoryPriorityLabel,
  inventoryScheduleFormatLabel,
  inventoryScheduleFrequencyLabel,
  inventoryScheduleRunStatusLabel,
} from "./inventoryUtils";

describe("Inventory operational status labels", () => {
  it("maps known workflow values without exposing backend tokens", () => {
    expect(inventoryActionTypeLabel("dopuna")).toBe("Dopuna");
    expect(inventoryActionTypeLabel("TRANSFER")).toBe("Transfer");
    expect(inventoryActionTypeLabel("markdown")).toBe("Sniženje");
    expect(inventoryActionTypeLabel("clearance")).toBe("Rasprodaja");
    expect(inventoryActionStatusLabel("pending")).toBe("Na čekanju");
    expect(inventoryActionStatusLabel("approved")).toBe("Odobreno");
    expect(inventoryActionStatusLabel("deferred")).toBe("Odloženo");
    expect(inventoryActionStatusLabel("closed")).toBe("Zatvoreno");
    expect(inventoryPriorityLabel("critical")).toBe("Kritičan prioritet");
    expect(inventoryPriorityLabel("high")).toBe("Visok prioritet");
    expect(inventoryPriorityLabel("medium")).toBe("Srednji prioritet");
    expect(inventoryPriorityLabel("low")).toBe("Nizak prioritet");
  });

  it("maps known scheduler values and keeps unknown values visibly non-successful", () => {
    expect(inventoryScheduleFrequencyLabel("daily")).toBe("Dnevno");
    expect(inventoryScheduleFrequencyLabel("weekly")).toBe("Nedeljno");
    expect(inventoryScheduleFormatLabel("pdf")).toBe("PDF");
    expect(inventoryScheduleFormatLabel("xlsx")).toBe("Excel");
    expect(inventoryScheduleFormatLabel("csv")).toBe("CSV");
    expect(inventoryScheduleRunStatusLabel("generated", "2026-08-10T10:00:00Z")).toBe("Generisan");
    expect(inventoryScheduleRunStatusLabel("emailed", "2026-08-10T10:00:00Z")).toBe("Poslat mejlom");
    expect(inventoryScheduleRunStatusLabel("failed", "2026-08-10T10:00:00Z")).toBe("Neuspešno");
    expect(inventoryScheduleRunStatusLabel("unknown_status", "2026-08-10T10:00:00Z")).toBe("Nepoznato");
  });

  it("distinguishes missing values from a schedule that has not run", () => {
    expect(inventoryActionTypeLabel(null)).toBe("Nepoznato");
    expect(inventoryActionStatusLabel("")).toBe("Nepoznato");
    expect(inventoryPriorityLabel(undefined)).toBe("Nepoznato");
    expect(inventoryScheduleFrequencyLabel("future_frequency")).toBe("Nepoznato");
    expect(inventoryScheduleFormatLabel(null)).toBe("Nepoznato");
    expect(inventoryScheduleRunStatusLabel(null, "2026-08-10T10:00:00Z")).toBe("Nepoznato");
    expect(inventoryScheduleRunStatusLabel(null, null)).toBe("Nije pokrenuto");
  });
});
