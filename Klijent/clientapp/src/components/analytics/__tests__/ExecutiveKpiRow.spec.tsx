import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import ExecutiveKpiRow from "../ExecutiveKpiRow";

describe("ExecutiveKpiRow", () => {
  it("makes recommendation readiness and decision-set scope explicit", () => {
    render(
      <MemoryRouter>
        <ExecutiveKpiRow
          loading={false}
          totalRevenue={0}
          marginContributionRsd={null}
          totalUnits={0}
          inventoryDangerValueRsd={null}
          dataQualityTone="critical"
          dataQualityStatus="critical"
          missingSupplierCount={0}
          missingCostCount={280}
          readinessLabel="Kritično — preporuke nisu bezbedne"
          dataQualityScopeLabel="Artikli u skupu odluka"
        />
      </MemoryRouter>,
    );

    expect(screen.getByText("Spremnost za preporuke")).toBeInTheDocument();
    expect(screen.getByText("Kritično — preporuke nisu bezbedne")).toBeInTheDocument();
    expect(screen.getByText(/Obuhvat: Artikli u skupu odluka/)).toBeInTheDocument();
  });
});
