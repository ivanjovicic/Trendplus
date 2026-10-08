import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ActionWorkflowPanel } from "./ActionWorkflowPanel";

describe("ActionWorkflowPanel cost trust", () => {
  it("shows the source-anchored signal window and its basis", () => {
    render(
      <ActionWorkflowPanel
        actionWorkflow={{
          generatedAtUtc: "2026-10-08T10:00:00Z",
          asOfUtc: "2026-08-08T23:59:59.9999999Z",
          signalWindowFromUtc: "2026-07-10T00:00:00Z",
          signalWindowToExclusiveUtc: "2026-08-09T00:00:00Z",
          horizonBasis: "source_horizon",
          pendingCount: 0,
          approvedCount: 0,
          deferredCount: 0,
          closedCount: 0,
          items: [],
        }}
        operationsLoading={false}
        workflowBusyKey={null}
        onUpdateWorkflowStatus={vi.fn()}
      />,
    );

    const horizon = screen.getByTestId("inventory-action-horizon");
    // Inclusive business dates in UTC: the exclusive window end (9. 8.) and the local timezone must not shift the last day.
    expect(horizon).toHaveTextContent("10. 7. 2026. – 8. 8. 2026.");
    expect(horizon).toHaveTextContent("poslednjim danom prodaje u izvoru, ne sa današnjim datumom");
    expect(horizon).not.toHaveTextContent("9. 8. 2026.");
  });

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
              label: "Predložena dopuna",
              reason: "Forecast signal",
              status: "pending",
              artikalId: 501,
              naziv: "Artikal A",
              fromStoreName: null,
              toStoreName: "Prodavnica 1",
              suggestedQty: 2,
              estimatedValue: null,
              estimatedValueBasis: "suggested_action_cost",
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

    expect(screen.getByText("Procena troška predloga: Nije dostupno (nedostaje nabavna cena)")).toBeInTheDocument();
    expect(screen.getByText("Količina: 2")).toBeInTheDocument();
    expect(screen.getByText("Predložena dopuna")).toBeInTheDocument();
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
              label: "Predložena dopuna",
              reason: "Forecast signal",
              status: "pending",
              artikalId: 501,
              naziv: "Artikal A",
              fromStoreName: null,
              toStoreName: "Prodavnica 1",
              suggestedQty: 2,
              forecastDemandQty: 2,
              estimatedValue: 1000,
              estimatedValueBasis: "suggested_action_cost",
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

    expect(screen.getByText("Prognozirana tražnja (kol.): 2")).toBeInTheDocument();
    expect(screen.getByText(/Procena troška predloga:.*1\.000.*RSD/)).toBeInTheDocument();
    expect(screen.queryByText("Količina: 2")).not.toBeInTheDocument();
  });

  it("uses safe Serbian labels for known and unknown workflow tokens", () => {
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
              suggestionKey: "unknown-1",
              actionType: "future_action",
              priority: "future_priority",
              label: "Nepoznat predlog",
              reason: "Potrebna provera",
              status: "future_status",
              artikalId: 501,
              naziv: "Artikal A",
              fromStoreName: null,
              toStoreName: null,
              suggestedQty: 1,
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

    expect(screen.getAllByText("Nepoznato")).toHaveLength(3);
    expect(screen.queryByText("future_action")).not.toBeInTheDocument();
    expect(screen.queryByText("future_status")).not.toBeInTheDocument();
    expect(screen.queryByText("future_priority")).not.toBeInTheDocument();
  });
});
