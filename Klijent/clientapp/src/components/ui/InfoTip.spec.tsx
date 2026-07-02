import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import InfoTip from "./InfoTip";

describe("InfoTip accessibility", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => {
      callback(0);
      return 1;
    });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it("shows on keyboard focus, links trigger to tooltip and closes with Escape", () => {
    render(<InfoTip text="Objašnjenje analytics metrike." />);

    const trigger = screen.getByRole("button", { name: "Više informacija" });
    trigger.focus();
    fireEvent.focus(trigger);

    act(() => {
      vi.advanceTimersByTime(150);
    });

    const tooltip = screen.getByRole("tooltip");
    expect(tooltip).toHaveTextContent("Objašnjenje analytics metrike.");
    expect(trigger).toHaveAttribute("aria-describedby", tooltip.id);

    fireEvent.keyDown(document, { key: "Escape" });
    expect(trigger).toHaveFocus();
    expect(trigger).not.toHaveAttribute("aria-describedby");

    act(() => {
      vi.advanceTimersByTime(180);
    });

    expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();
  });

  it("opens with Enter and toggles closed from the keyboard", () => {
    render(<InfoTip text="Detalji preporuke." />);

    const trigger = screen.getByRole("button", { name: "Više informacija" });
    fireEvent.keyDown(trigger, { key: "Enter" });

    act(() => {
      vi.advanceTimersByTime(150);
    });

    expect(screen.getByRole("tooltip")).toHaveTextContent("Detalji preporuke.");

    fireEvent.keyDown(trigger, { key: "Enter" });
    act(() => {
      vi.advanceTimersByTime(180);
    });

    expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();
  });

  it("opens with Space and hides when focus leaves the trigger", () => {
    render(<InfoTip text="Kvalitet podataka." />);

    const trigger = screen.getByRole("button", { name: "Više informacija" });
    fireEvent.keyDown(trigger, { key: " " });

    act(() => {
      vi.advanceTimersByTime(150);
    });

    expect(screen.getByRole("tooltip")).toHaveTextContent("Kvalitet podataka.");

    fireEvent.blur(trigger);
    act(() => {
      vi.advanceTimersByTime(180);
    });

    expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();
  });
});
