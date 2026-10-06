import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import AnalyticsErrorState from "../../components/analytics/AnalyticsErrorState";
import { getSupplierOptionLabels, resolveNivelacijaErrorDetails } from "../nivelacijaErrorPresentation";

describe("Nivelacija failure presentation", () => {
  it("maps the missing-contract backend failure and renders support code plus correlation ID", () => {
    const details = resolveNivelacijaErrorDetails({
      message: "Npgsql.PostgresException: relation does not exist",
      errorCode: "vendor_sales_nivelacija_contract_missing",
      correlationId: "corr-contract-554",
    });

    render(
      <MemoryRouter>
        <AnalyticsErrorState
          title="Podaci trenutno nisu dostupni"
          message={details.message}
          errorCode={details.errorCode}
          showErrorCode
          correlationId={details.correlationId}
        />
      </MemoryRouter>,
    );

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent("Pre/post analiza čeka ispravku šeme baze.");
    expect(alert).toHaveTextContent("vendor_sales_nivelacija_contract_missing");
    expect(alert).toHaveTextContent("Correlation ID: corr-contract-554");
    expect(alert).not.toHaveTextContent("Npgsql");
  });

  it("maps a missing SELECT privilege to business copy instead of the generic fallback", () => {
    const details = resolveNivelacijaErrorDetails({
      message: "Pre/post nivelacija nije dostupna: API uloga nema SELECT privilegiju nad relacijom public.vw_vendor_sales_nivelacija.",
      errorCode: "vendor_sales_nivelacija_privilege_missing",
      correlationId: "corr-privilege-1",
    });

    expect(details.message).toBe("Pre/post analiza čeka dozvolu za čitanje izveštaja u bazi. Sačuvajte kod i ID za podršku.");
    expect(details.errorCode).toBe("vendor_sales_nivelacija_privilege_missing");
    expect(details.correlationId).toBe("corr-privilege-1");
  });

  it("adds supplier IDs only when Serbian-normalized names collide and sorts by Serbian collation", () => {
    const labels = getSupplierOptionLabels([
      { id: 8, naziv: "čarda" },
      { id: 3, naziv: "Alfa" },
      { id: 12, naziv: "Čarda" },
    ]);

    expect(labels.map(({ id, displayName }) => [id, displayName])).toEqual([
      [3, "Alfa"],
      [8, "čarda (8)"],
      [12, "Čarda (12)"],
    ]);
  });
});
