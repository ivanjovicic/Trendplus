import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import TrendModelList from "./TrendModelList";

describe("TrendModelList", () => {
  it("does not present hardcoded model values as accuracy", () => {
    render(<TrendModelList />);

    expect(screen.getByRole("heading", { name: "Trend modeli" })).toBeInTheDocument();
    expect(screen.getAllByText("Tačnost: nije dostupna")).toHaveLength(5);
    expect(screen.queryByText("84")).not.toBeInTheDocument();
    expect(screen.queryByText("+4.2%")).not.toBeInTheDocument();
  });

  it("explains the missing evaluation evidence through an accessible tooltip", async () => {
    render(<TrendModelList />);

    const infoButtons = screen.getAllByRole("button", { name: "Više informacija" });
    expect(infoButtons.length).toBe(6);

    fireEvent.click(infoButtons[0]);

    expect(await screen.findByRole("tooltip")).toHaveTextContent(/ne prikazuje izmišljenu tačnost/i);
  });
});
