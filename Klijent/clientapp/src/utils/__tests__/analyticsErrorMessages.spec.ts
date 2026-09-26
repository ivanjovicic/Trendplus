import { describe, expect, it } from "vitest";
import {
  ANALYTICS_ERROR_FALLBACK_MESSAGE,
  getSafeAnalyticsErrorMessage,
} from "../analyticsErrorMessages";

describe("getSafeAnalyticsErrorMessage", () => {
  it("maps raw .NET/provider text to the fallback", () => {
    expect(getSafeAnalyticsErrorMessage("System.NullReferenceException: Object reference not set to an instance of an object."))
      .toBe(ANALYTICS_ERROR_FALLBACK_MESSAGE);
  });

  it("returns only explicitly allowlisted screen-safe messages", () => {
    const safeMessage = "Statistika prodaje po tipu obuće trenutno nije dostupna. Referentni ID: trace-123.";

    expect(getSafeAnalyticsErrorMessage(
      safeMessage,
      null,
      "Greška pri učitavanju podataka po tipu obuće.",
      ["Statistika prodaje po tipu obuće trenutno nije dostupna."],
    )).toBe(safeMessage);

    expect(getSafeAnalyticsErrorMessage(
      "provider internals leaked",
      null,
      "Greška pri učitavanju podataka po tipu obuće.",
      ["Statistika prodaje po tipu obuće trenutno nije dostupna."],
    )).toBe("Greška pri učitavanju podataka po tipu obuće.");
  });
});
