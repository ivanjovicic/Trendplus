import { z } from "zod";

/**
 * Access "Random AutoNumber" produces negative IDs for suppliers, shoe types, stores and possibly seasons.
 * Any Int32 is a valid entity reference; "unknown" means null or a missing master row, never a sign check.
 */
export const INT32_MIN = -2147483648;
export const INT32_MAX = 2147483647;

export const entityId = z.number().finite().int().min(INT32_MIN).max(INT32_MAX);
export const nullableEntityId = entityId.nullable();

const ENTITY_ID_PARAM_PATTERN = /^-?\d+$/;

export function isEntityId(value: unknown): value is number {
  return typeof value === "number" && Number.isInteger(value) && value >= INT32_MIN && value <= INT32_MAX;
}

export function parseEntityIdParam(value: string | null | undefined): number | null {
  if (value == null) return null;
  const trimmed = value.trim();
  if (!ENTITY_ID_PARAM_PATTERN.test(trimmed)) return null;
  const parsed = Number(trimmed);
  return isEntityId(parsed) ? parsed : null;
}

export type EntityKind = "supplier" | "store" | "shoeType" | "season";

const UNKNOWN_ENTITY_LABELS: Record<EntityKind, string> = {
  supplier: "Nepoznat dobavljač",
  store: "Nepoznat objekat",
  shoeType: "Nepoznat tip obuće",
  season: "Nepoznata sezona",
};

export function formatEntityFallbackLabel(kind: EntityKind, id: number | null | undefined): string {
  const base = UNKNOWN_ENTITY_LABELS[kind];
  return isEntityId(id) ? `${base} (ID ${id})` : base;
}
