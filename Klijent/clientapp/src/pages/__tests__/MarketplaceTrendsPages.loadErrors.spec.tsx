import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import type { ComponentType } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const googleApi = vi.hoisted(() => ({
  syncGoogleShopping: vi.fn(),
  getGoogleShoppingByType: vi.fn(),
  getGoogleShoppingCategories: vi.fn(),
  deleteGoogleShoppingCategory: vi.fn(),
}));
const amazonApi = vi.hoisted(() => ({
  syncAmazonShoes: vi.fn(),
  getAmazonShoesByType: vi.fn(),
  getAmazonShoeCategories: vi.fn(),
  deleteAmazonShoeCategory: vi.fn(),
}));
const ebayApi = vi.hoisted(() => ({
  syncEbayShoes: vi.fn(),
  getEbayShoesByType: vi.fn(),
  getEbayShoeCategories: vi.fn(),
  deleteEbayShoeCategory: vi.fn(),
}));

vi.mock("../../services/googleShoppingApi", () => googleApi);
vi.mock("../../services/amazonShoesApi", () => amazonApi);
vi.mock("../../services/ebayShoesApi", () => ebayApi);

import GoogleShoppingTrendsPage from "../GoogleShoppingTrendsPage";
import AmazonShoesTrendsPage from "../AmazonShoesTrendsPage";
import EbayShoesTrendsPage from "../EbayShoesTrendsPage";

type PageCase = {
  name: string;
  Page: ComponentType;
  getByType: ReturnType<typeof vi.fn>;
  getCategories: ReturnType<typeof vi.fn>;
};

const cases: PageCase[] = [
  { name: "Google Shopping", Page: GoogleShoppingTrendsPage, getByType: googleApi.getGoogleShoppingByType, getCategories: googleApi.getGoogleShoppingCategories },
  { name: "Amazon", Page: AmazonShoesTrendsPage, getByType: amazonApi.getAmazonShoesByType, getCategories: amazonApi.getAmazonShoeCategories },
  { name: "eBay", Page: EbayShoesTrendsPage, getByType: ebayApi.getEbayShoesByType, getCategories: ebayApi.getEbayShoeCategories },
];

function product(id: number, label: string) {
  return {
    id,
    productId: `p-${id}`,
    asin: `a-${id}`,
    ebayItemId: `e-${id}`,
    title: label,
    name: label,
    brand: "Brand",
    condition: null,
    price: 49.9,
    originalPrice: null,
    currency: "EUR",
    rating: 4.2,
    reviewCount: 10,
    position: id,
    imageUrl: null,
    productUrl: null,
    category: "sneakers",
    gender: null,
    domain: null,
    marketplace: null,
    trendScore: 1.5,
    lastSynced: "2026-10-14T08:00:00Z",
    createdAt: "2026-10-14T08:00:00Z",
  };
}

function page(items: ReturnType<typeof product>[], pageNumber: number, pages: number) {
  return { items, total: items.length * pages, page: pageNumber, pageSize: 20, pages };
}

describe.each(cases)("$name trends page loader errors (RQ655)", ({ Page, getByType, getCategories }) => {
  let consoleError: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    vi.resetAllMocks();
    consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
    getCategories.mockResolvedValue([]);
  });

  afterEach(() => {
    consoleError.mockRestore();
  });

  it("shows a product load failure with a retry action instead of the empty state", async () => {
    getByType.mockRejectedValueOnce(new Error("HTTP 502"));
    getByType.mockResolvedValueOnce(page([product(1, "Recovered shoe")], 1, 1));

    render(<Page />);

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Proizvodi trenutno nisu dostupni");
    expect(screen.queryByText(/Nema rezultata za/)).not.toBeInTheDocument();
    expect(consoleError).toHaveBeenCalled();

    fireEvent.click(within(alert).getByRole("button", { name: "Pokušaj ponovo" }));

    expect(await screen.findByText("Recovered shoe")).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(getByType).toHaveBeenCalledTimes(2);
  });

  it("keeps loaded products and surfaces a failed next page", async () => {
    getByType.mockResolvedValueOnce(page([product(1, "First shoe")], 1, 2));
    getByType.mockRejectedValueOnce(new Error("HTTP 504"));

    render(<Page />);

    expect(await screen.findByText("First shoe")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /Još rezultata/ }));

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Dodatni proizvodi nisu učitani");
    expect(screen.getByText("First shoe")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Još rezultata/ })).toBeEnabled();
    expect(consoleError).toHaveBeenCalled();
  });

  it("shows a category load failure with a retry action", async () => {
    getCategories.mockRejectedValueOnce(new Error("HTTP 500"));
    getCategories.mockResolvedValueOnce([]);
    getByType.mockResolvedValue(page([product(1, "Any shoe")], 1, 1));

    render(<Page />);

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Kategorije trenutno nisu dostupne");

    fireEvent.click(within(alert).getByRole("button", { name: "Pokušaj ponovo" }));

    await waitFor(() => expect(screen.queryByRole("alert")).not.toBeInTheDocument());
    expect(getCategories).toHaveBeenCalledTimes(2);
  });
});
