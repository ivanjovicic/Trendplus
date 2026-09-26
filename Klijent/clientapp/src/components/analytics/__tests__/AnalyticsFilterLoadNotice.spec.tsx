import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import AnalyticsFilterLoadNotice from "../AnalyticsFilterLoadNotice";

describe("AnalyticsFilterLoadNotice", () => {
  it("explains that the store filter is unavailable instead of implying an empty store list", () => {
    render(<AnalyticsFilterLoadNotice onRetry={vi.fn()} />);

    expect(screen.getByRole("alert")).toHaveTextContent("Filter prodavnice nije dostupan.");
    expect(screen.getByRole("alert")).toHaveTextContent("izbor pojedinačne prodavnice nije potvrđen");
  });

  it("exposes a retry action", () => {
    const onRetry = vi.fn();
    render(<AnalyticsFilterLoadNotice onRetry={onRetry} />);

    fireEvent.click(screen.getByRole("button", { name: "Pokušaj ponovo" }));

    expect(onRetry).toHaveBeenCalledOnce();
  });
});
