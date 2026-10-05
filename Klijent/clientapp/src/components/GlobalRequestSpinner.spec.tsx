import { render, screen, act, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import GlobalRequestSpinner, { formatActiveRequestsLabel } from "./GlobalRequestSpinner";

const useRequestActivity = vi.fn();

vi.mock("../context/RequestActivityContext", () => ({
  useRequestActivity: (...args: unknown[]) => useRequestActivity(...args),
}));

describe("formatActiveRequestsLabel", () => {
  it("uses Serbian singular and plural forms", () => {
    expect(formatActiveRequestsLabel(1)).toBe("1 zahtev u toku");
    expect(formatActiveRequestsLabel(2)).toBe("2 zahteva u toku");
    expect(formatActiveRequestsLabel(4)).toBe("4 zahteva u toku");
    expect(formatActiveRequestsLabel(5)).toBe("5 zahteva u toku");
    expect(formatActiveRequestsLabel(0)).toBe("0 zahteva u toku");
  });
});

describe("GlobalRequestSpinner", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    useRequestActivity.mockReturnValue({ activeRequests: 1, hasActiveRequests: true });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it("shows Serbian loading copy after delay", async () => {
    render(<GlobalRequestSpinner />);
    expect(screen.queryByTestId("global-request-spinner")).not.toBeInTheDocument();

    await act(async () => {
      vi.advanceTimersByTime(180);
    });

    const rootEl = screen.getByTestId("global-request-spinner");
    expect(within(rootEl).getByText("Učitavanje podataka", { selector: "strong" })).toBeInTheDocument();
    expect(within(rootEl).getByText("1 zahtev u toku")).toBeInTheDocument();
  });

  it("pluralizes multiple requests", async () => {
    useRequestActivity.mockReturnValue({ activeRequests: 3, hasActiveRequests: true });
    render(<GlobalRequestSpinner />);
    await act(async () => {
      vi.advanceTimersByTime(180);
    });
    expect(screen.getByText("3 zahteva u toku")).toBeInTheDocument();
  });
});
