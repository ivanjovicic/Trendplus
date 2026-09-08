import { describe, expect, it } from "vitest";
import {
  ANALYTICS_VELOCITY_HELP,
  ANALYTICS_VELOCITY_LABEL,
  ANALYTICS_VELOCITY_SHORT_LABEL,
} from "./analyticsVelocitySemantics";

describe("analytics velocity semantics", () => {
  it("labels the units-per-day field as a calendar-day rate", () => {
    expect(ANALYTICS_VELOCITY_LABEL).toContain("kalendarskom danu");
    expect(ANALYTICS_VELOCITY_SHORT_LABEL).toContain("kalendarski dan");
    expect(ANALYTICS_VELOCITY_HELP).toContain("dani bez prodaje");
  });
});
