import { render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import SeasonalImageCarousel from "./SeasonalImageCarousel";

const images = [
  {
    id: 1,
    imageUrl: "https://example.com/a.jpg",
    source: "unsplash",
    photographerName: "Ana",
    photographerUrl: "https://example.com/ana",
    sourceUrl: "https://unsplash.com",
  },
  {
    id: 2,
    imageUrl: "https://example.com/b.jpg",
    source: "pexels",
    photographerName: "Marko",
    photographerUrl: "https://example.com/marko",
    sourceUrl: "https://pexels.com",
  },
];

describe("SeasonalImageCarousel", () => {
  beforeEach(() => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        json: async () => images,
      }),
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("renders strip and labeled nav buttons", async () => {
    vi.spyOn(window, "matchMedia").mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }) as MediaQueryList);

    render(<SeasonalImageCarousel />);
    await waitFor(() => expect(screen.getByTestId("carousel-strip")).toBeInTheDocument());
    expect(screen.getByRole("button", { name: "Prethodna slika" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Sledeća slika" })).toBeInTheDocument();
  });

  it("does not start auto-scroll when prefers-reduced-motion is set", async () => {
    const setIntervalSpy = vi.spyOn(window, "setInterval");
    vi.spyOn(window, "matchMedia").mockImplementation((query: string) => ({
      matches: query.includes("prefers-reduced-motion"),
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }) as MediaQueryList);

    render(<SeasonalImageCarousel />);
    await waitFor(() => expect(screen.getByTestId("carousel-strip")).toBeInTheDocument());
    const carouselIntervals = setIntervalSpy.mock.calls.filter((call) => call[1] === 4000);
    expect(carouselIntervals).toHaveLength(0);
    expect(window.getComputedStyle(screen.getByTestId("carousel-strip")).scrollBehavior).toBe("auto");
  });
});
