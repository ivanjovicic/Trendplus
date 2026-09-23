export const UNKNOWN_COLOR_DISPLAY_NAME = "Nepoznato";

export function colorDisplayName(value: string | null | undefined): string {
  const normalized = (value ?? "").trim().normalize("NFC");
  return !normalized || normalized.toUpperCase() === UNKNOWN_COLOR_DISPLAY_NAME.toUpperCase()
    ? UNKNOWN_COLOR_DISPLAY_NAME
    : normalized;
}

export function colorIdentityKey(value: string | null | undefined): string {
  return colorDisplayName(value).toUpperCase();
}
