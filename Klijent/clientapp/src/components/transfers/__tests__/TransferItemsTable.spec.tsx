import { render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import TransferItemsTable from "../TransferItemsTable";

describe("TransferItemsTable responsive region", () => {
  it("keeps the item code sticky inside a keyboard-focusable contained scroller", () => {
    render(<TransferItemsTable items={[{ skuId: 7, code: "SKU-007", name: "Sintetička patika", quantity: 3 }]} onChange={vi.fn()} />);

    const region = screen.getByRole("region", { name: "Stavke za prenos" });
    expect(region).toHaveAttribute("tabindex", "0");
    expect(within(region).getByText("SKU-007")).toBeInTheDocument();
    expect(within(region).getByText("Sintetička patika")).toBeInTheDocument();
  });
});
