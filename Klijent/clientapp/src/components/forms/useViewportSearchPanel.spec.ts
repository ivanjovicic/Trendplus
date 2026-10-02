import { act, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { useViewportSearchPanel } from "./useViewportSearchPanel";

describe("useViewportSearchPanel", () => {
  afterEach(() => vi.restoreAllMocks());

  it("anchors coarse-pointer results to the input and caps them to visible space", () => {
    vi.spyOn(window, "matchMedia").mockReturnValue({ matches: true } as MediaQueryList);
    Object.defineProperty(window, "innerHeight", { configurable: true, value: 500 });
    let top = 420;
    const anchor = document.createElement("input");
    anchor.getBoundingClientRect = () => ({
      left: 16, right: 304, top, bottom: top + 44, width: 288, height: 44,
      x: 16, y: top, toJSON: () => ({}),
    }) as DOMRect;
    const anchorRef = { current: anchor };

    const { result } = renderHook(() => useViewportSearchPanel(anchorRef, true));
    expect(result.current).toMatchObject({ position: "fixed", left: 16, width: 288, maxHeight: 320 });
    expect(result.current?.top).toBeLessThan(420);

    top = 100;
    act(() => window.dispatchEvent(new Event("resize")));
    expect(result.current?.top).toBe(152);
    expect(result.current?.maxHeight).toBe(320);
  });
});
