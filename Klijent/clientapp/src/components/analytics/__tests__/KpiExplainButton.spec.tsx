import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { MemoryRouter } from "react-router-dom";
import KpiExplainButton from "../KpiExplainButton";

describe("KpiExplainButton", () => {
  it("closes on Escape and restores focus to the trigger", async () => {
    render(
      <MemoryRouter>
        <KpiExplainButton metricKey="totalRevenue" ariaLabel="Kako je izračunat prihod" />
      </MemoryRouter>
    );

    const trigger = screen.getByRole("button", { name: "Kako je izračunat prihod" });
    trigger.focus();
    expect(trigger).toHaveFocus();
    fireEvent.click(trigger);

    expect(screen.getByRole("dialog")).toBeInTheDocument();
    fireEvent.keyDown(window, { key: "Escape" });

    await waitFor(() => {
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    });
    expect(trigger).toHaveFocus();
  });
});
