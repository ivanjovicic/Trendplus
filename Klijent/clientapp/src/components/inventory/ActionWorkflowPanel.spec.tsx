import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ActionWorkflowPanel } from "./ActionWorkflowPanel";

describe("ActionWorkflowPanel cost trust", () => {
  it("labels missing forecast cost as unavailable instead of zero", () => {
    render(
      <ActionWorkflowPanel
        actionWorkflow={{
          generatedAtUtc: "2026-08-10T10:00:00Z",
          pendingCount: 1,
          approvedCount: 0,
          deferredCount: 0,
          closedCount: 0,
          items: [
            {
              suggestionKey: "forecast-1",
              actionType: "dopuna",
              priority: "high",
              label: "Predlozena dopuna",
              reason: "Forecast signal",
              status: "pending",
              artikalId: 501,
              naziv: "Artikal A",
              fromStoreName: null,
              toStoreName: "Prodavnica 1",
              suggestedQty: 2,
              estimatedValue: null,
              costMissing: true,
              daysSinceMovement: 0,
              note: null,
              updatedAtUtc: "2026-08-10T10:00:00Z",
            },
          ],
        }}
        operationsLoading={false}
        workflowBusyKey={null}
        onUpdateWorkflowStatus={vi.fn()}
      />,
    );

    expect(screen.getByText("Vrednost: Nije dostupno (nedostaje nabavna cena)")).toBeInTheDocument();
    expect(screen.getByText("Qty: 2")).toBeInTheDocument();
    expect(screen.getByText("Predlozena dopuna")).toBeInTheDocument();
  });

  it("labels forecast quantity as a demand signal instead of a final reorder qty", () => {
    render(
      <ActionWorkflowPanel
        actionWorkflow={{
          generatedAtUtc: "2026-08-10T10:00:00Z",
          pendingCount: 1,
          approvedCount: 0,
          deferredCount: 0,
          closedCount: 0,
          items: [
            {
              suggestionKey: "forecast-1",
              actionType: "dopuna",
              priority: "high",
              label: "Predlozena dopuna",
              reason: "Forecast signal",
              status: "pending",
              artikalId: 501,
              naziv: "Artikal A",
              fromStoreName: null,
              toStoreName: "Prodavnica 1",
              suggestedQty: 2,
              forecastDemandQty: 2,
              estimatedValue: 1000,
              costMissing: false,
              daysSinceMovement: 0,
              note: null,
              updatedAtUtc: "2026-08-10T10:00:00Z",
            },
          ],
        }}
        operationsLoading={false}
        workflowBusyKey={null}
        onUpdateWorkflowStatus={vi.fn()}
      />,
    );

    expect(screen.getByText("Forecast demand qty: 2")).toBeInTheDocument();
    expect(screen.queryByText("Qty: 2")).not.toBeInTheDocument();
  });
});
