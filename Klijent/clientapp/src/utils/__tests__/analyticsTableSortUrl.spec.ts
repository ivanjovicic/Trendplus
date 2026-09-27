import { describe, expect, it } from "vitest";
import { readAnalyticsTableSort, writeAnalyticsTableSort } from "../analyticsTableSortUrl";

const FIELDS = ["name", "revenue", "score"] as const;
type Field = (typeof FIELDS)[number];
const defaultDirFor = (field: Field) => (field === "name" ? "asc" : "desc");

describe("analyticsTableSortUrl", () => {
  it("reads a valid field and direction from the URL", () => {
    const params = new URLSearchParams("sort=revenue&dir=asc");
    expect(readAnalyticsTableSort(params, FIELDS, "score", "desc")).toEqual({ field: "revenue", dir: "asc" });
  });

  it("falls back to the default field and a per-field default direction for invalid values", () => {
    const params = new URLSearchParams("sort=unknown&dir=sideways");
    expect(readAnalyticsTableSort(params, FIELDS, "name", defaultDirFor)).toEqual({ field: "name", dir: "asc" });
    expect(readAnalyticsTableSort(new URLSearchParams("sort=score"), FIELDS, "name", defaultDirFor))
      .toEqual({ field: "score", dir: "desc" });
  });

  it("always writes sort and dir when no defaults are supplied", () => {
    const next = writeAnalyticsTableSort(new URLSearchParams("topN=5"), "name", "asc");
    expect(next.toString()).toBe("topN=5&sort=name&dir=asc");
  });

  it("omits the default field and the field-specific default direction when defaults are supplied", () => {
    const defaults = { field: "score" as Field, dir: defaultDirFor };
    expect(writeAnalyticsTableSort(new URLSearchParams("sort=name&dir=asc"), "score", "desc", defaults).toString()).toBe("");
    expect(writeAnalyticsTableSort(new URLSearchParams(), "name", "asc", defaults).toString()).toBe("sort=name");
    expect(writeAnalyticsTableSort(new URLSearchParams(), "name", "desc", defaults).toString()).toBe("sort=name&dir=desc");
  });

  it("does not mutate the input search params", () => {
    const current = new URLSearchParams("sort=name");
    writeAnalyticsTableSort(current, "revenue", "asc");
    expect(current.toString()).toBe("sort=name");
  });
});
