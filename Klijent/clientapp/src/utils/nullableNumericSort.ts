export type NullableNumericSortDirection = "asc" | "desc";

type NullableNumeric = number | null | undefined;

/** Compare measured numbers while keeping unavailable/non-finite values last in either direction. */
export function compareNullableNumbers(
  left: NullableNumeric,
  right: NullableNumeric,
  direction: NullableNumericSortDirection,
): number {
  const leftAvailable = typeof left === "number" && Number.isFinite(left);
  const rightAvailable = typeof right === "number" && Number.isFinite(right);

  if (!leftAvailable && !rightAvailable) return 0;
  if (!leftAvailable) return 1;
  if (!rightAvailable) return -1;

  const compare = left - right;
  return direction === "asc" ? compare : -compare;
}
