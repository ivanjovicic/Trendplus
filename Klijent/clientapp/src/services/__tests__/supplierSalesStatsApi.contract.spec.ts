import { describe, expect, it } from "vitest";
import { rest } from "../../mocks/mswCompat";
import { server } from "../../mocks/server";
import { AnalyticsResponseValidationError } from "../../validation/analyticsResponseValidation";
import { getSupplierSalesStats } from "../supplierSalesStatsApi";

describe("Supplier Sales API contract", () => {
  it("fails closed when decision-critical response sections are missing", async () => {
    server.use(
      rest.get("/api/analytics/supplier-sales-stats", (_req, res, ctx) => res(
        ctx.status(200),
        ctx.json({
          generatedAt: "2026-07-01T08:00:00Z",
          meta: { success: true },
          fromDate: null,
          toDate: null,
          dataWindowFrom: null,
          dataWindowTo: null,
          sezonaId: null,
          storeId: null,
          dataScope: "all",
          suppliers: [],
          dataQuality: {},
          sezone: [],
        }),
      )),
    );

    await expect(getSupplierSalesStats()).rejects.toBeInstanceOf(AnalyticsResponseValidationError);
  });
});
