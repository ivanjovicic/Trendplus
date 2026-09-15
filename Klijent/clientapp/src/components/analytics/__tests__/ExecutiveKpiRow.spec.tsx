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

  it("keeps value text, tone and accessibility state aligned for unavailable metrics", () => {
    render(
      <MemoryRouter>
        <ExecutiveKpiRow
          loading={false}
          totalRevenue={null}
          marginContributionRsd={Number.NaN}
          totalUnits={Number.POSITIVE_INFINITY}
          inventoryDangerValueRsd={Number.NEGATIVE_INFINITY}
          dataQualityTone="insufficient_data"
          dataQualityStatus="insufficient_data"
          missingSupplierCount={null}
          missingCostCount={null}
        />
      </MemoryRouter>,
    );

    for (const label of ["Prihod", "Maržni doprinos", "Prodate jedinice", "Lager u riziku"]) {
      const card = screen.getByRole("article", { name: `${label}: Nije dostupno` });
      expect(card).toHaveClass("insufficient_data");
      expect(card).toHaveAttribute("data-value-state", "unavailable");
      expect(card).not.toHaveClass("good", "warning", "neutral");
    }

    const readiness = screen.getByRole("article", { name: /Spremnost za preporuke: Nedovoljno podataka/i });
    expect(readiness).toHaveClass("insufficient_data");
    expect(readiness).toHaveAttribute("data-value-state", "unavailable");
  });

  it("keeps finite zero visible and uses an appropriate non-healthy tone for negative evidence", () => {
    render(
      <MemoryRouter>
        <ExecutiveKpiRow
          loading={false}
          totalRevenue={0}
          marginContributionRsd={-10}
          totalUnits={0}
          inventoryDangerValueRsd={0}
          dataQualityTone="good"
          dataQualityStatus="good"
          missingSupplierCount={0}
          missingCostCount={0}
        />
      </MemoryRouter>,
    );

    expect(screen.getByRole("article", { name: "Prihod: 0 RSD" })).toHaveClass("neutral");
    expect(screen.getByRole("article", { name: "Maržni doprinos: -10 RSD" })).toHaveClass("critical");
    expect(screen.getByRole("article", { name: "Prodate jedinice: 0" })).toHaveClass("neutral");
    expect(screen.getByRole("article", { name: "Lager u riziku: 0 RSD" })).toHaveClass("neutral");
  });
});
