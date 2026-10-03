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
