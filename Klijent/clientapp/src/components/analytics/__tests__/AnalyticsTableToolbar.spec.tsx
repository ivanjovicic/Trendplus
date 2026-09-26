import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import AnalyticsTableToolbar from "../AnalyticsTableToolbar";
import { getPrintPayload } from "../../../services/analyticsTableState";
import {
  downloadExport,
  generateExport,
  requestPrintPreview,
  waitForExport,
} from "../../../services/exportApi";
import type {
  DocumentOperationResponse,
  DocumentStatusResponse,
} from "../../../services/exportApi";
import type { AnalyticsTableColumn } from "../../../types/analyticsTable";

vi.mock("../../../services/exportApi", async () => {
  const actual = await vi.importActual<
    typeof import("../../../services/exportApi")
  >("../../../services/exportApi");

  return {
    ...actual,
    generateExport: vi.fn(),
    requestPrintPreview: vi.fn(),
    waitForExport: vi.fn(),
    downloadExport: vi.fn(),
    resolveApiUrl: vi.fn((path: string) => `https://api.local${path}`),
  };
});

type TestRow = {
  supplier: string;
  revenue: number;
  hiddenInternal?: string;
};

const rows: TestRow[] = [
  {
    supplier: "Dobavlja\u010D A",
    revenue: 120000,
    hiddenInternal: "ignore-a",
  },
  {
    supplier: "Dobavlja\u010D B",
    revenue: 80000,
    hiddenInternal: "ignore-b",
  },
];

const columns: AnalyticsTableColumn<TestRow>[] = [
  { key: "supplier", header: "Dobavlja\u010D", dataType: "text" },
  {
    key: "revenue",
    header: "Prihod",
    dataType: "currency",
    getValue: (row) => row.revenue,
  },
];

function operation(
  overrides: Partial<DocumentOperationResponse>,
): DocumentOperationResponse {
  return {
    documentId: "doc-sync",
    status: "completed",
    isAsync: false,
    createdAtUtc: "2026-07-01T10:00:00Z",
    ...overrides,
  };
}

function status(
  overrides: Partial<DocumentStatusResponse>,
): DocumentStatusResponse {
  return {
    documentId: "doc-status",
    status: "completed",
    isAsync: false,
    createdAtUtc: "2026-07-01T10:00:00Z",
    ...overrides,
  };
}

function renderToolbar() {
  return render(
    <AnalyticsTableToolbar
      tableKey="supplier-test"
      tableTitle="Supplier test"
      columns={columns}
      rows={rows}
      filters={[{ key: "period", label: "Period", value: "30d" }]}
      metadata={[
        { key: "generatedAt", label: "Generisano", value: "2026-07-01" },
      ]}
      defaultOrientation="landscape"
    />,
  );
}

describe("AnalyticsTableToolbar", () => {
  beforeEach(() => {
    vi.mocked(generateExport).mockReset();
    vi.mocked(requestPrintPreview).mockReset();
    vi.mocked(waitForExport).mockReset();
    vi.mocked(downloadExport).mockReset();
    vi.spyOn(window, "open").mockImplementation(() => ({ close: vi.fn() }) as unknown as Window);
    vi.mocked(window.open).mockClear();
  });

  it("prints the exact resolved table payload through local print state", () => {
    renderToolbar();

    fireEvent.click(screen.getByRole("button", { name: /\u0160tampaj/i }));

    expect(window.open).toHaveBeenCalledTimes(1);
    const [url] = vi.mocked(window.open).mock.calls[0];
    expect(String(url)).toContain("/print/analytics/supplier-test?stateKey=");

    const stateKey = new URL(String(url), "http://localhost").searchParams.get(
      "stateKey",
    );
    const payload = getPrintPayload(stateKey);

    expect(payload).not.toBeNull();
    expect(payload?.tableKey).toBe("supplier-test");
    expect(payload?.rows).toEqual([
      { supplier: "Dobavlja\u010D A", revenue: 120000 },
      { supplier: "Dobavlja\u010D B", revenue: 80000 },
    ]);
    expect(payload?.filters).toEqual([
      { key: "period", label: "Period", value: "30d" },
    ]);
    expect(payload?.metadata).toEqual([
      { key: "generatedAt", label: "Generisano", value: "2026-07-01" },
    ]);
  });

  it("opens the export menu and sends sync Excel export with table rows, filters and metadata", async () => {
    vi.mocked(generateExport).mockResolvedValue(
      operation({
        downloadUrl: "/exports/supplier-test.xlsx",
        fileName: "supplier-test.xlsx",
      }),
    );

    renderToolbar();

    expect(screen.getByText("Redova: 2")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /Izvoz/i }));
    expect(
      screen.getByRole("menu", { name: "Formati izvoza" }),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Izve\u0161taj za menad\u017Ement i \u0161tampu"),
    ).toBeInTheDocument();
    expect(screen.getByText("Tabela za dalju analizu")).toBeInTheDocument();
    expect(screen.getByText("Brz flat-file izvoz")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("menuitem", { name: "Izvezi kao Excel" }));
    expect(
      screen.getByRole("dialog", { name: /Export Supplier test/i }),
    ).toBeInTheDocument();
    expect(screen.getByText("Premium analytics export")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /Pokreni export/i }));

    await waitFor(() => expect(generateExport).toHaveBeenCalledTimes(1));
    expect(generateExport).toHaveBeenCalledWith(
      expect.objectContaining({
        tableKey: "supplier-test",
        rows: [
          { supplier: "Dobavlja\u010D A", revenue: 120000 },
          { supplier: "Dobavlja\u010D B", revenue: 80000 },
        ],
        filters: [{ key: "period", label: "Period", value: "30d" }],
        metadata: [
          { key: "generatedAt", label: "Generisano", value: "2026-07-01" },
        ],
      }),
      expect.objectContaining({
        format: "xlsx",
        orientation: "landscape",
        includeFiltersAndMetadata: true,
      }),
    );
    expect(downloadExport).toHaveBeenCalledWith(
      "/exports/supplier-test.xlsx",
      "supplier-test.xlsx",
    );
    expect(screen.getByRole("status")).toHaveTextContent(
      "Eksport je preuzet.",
    );
  });

  it("routes PDF preview through print preview instead of direct export", async () => {
    vi.mocked(requestPrintPreview).mockResolvedValue(
      operation({ printUrl: "/print-preview/123" }),
    );

    renderToolbar();

    fireEvent.click(screen.getByRole("button", { name: /Izvoz/i }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Izvezi kao PDF" }));
    fireEvent.click(screen.getByRole("button", { name: /Otvori preview/i }));

    await waitFor(() => expect(requestPrintPreview).toHaveBeenCalledTimes(1));
    expect(generateExport).not.toHaveBeenCalled();
    expect(window.open).toHaveBeenCalledWith(
      "https://api.local/print-preview/123",
      "_blank",
      "noopener",
    );
  });

  it("waits for async exports before downloading the completed document", async () => {
    vi.mocked(generateExport).mockResolvedValue(
      operation({ isAsync: true, documentId: "doc-1", status: "queued" }),
    );
    vi.mocked(waitForExport).mockResolvedValue(
      status({ downloadUrl: "/exports/ready.csv", fileName: "ready.csv" }),
    );

    renderToolbar();

    fireEvent.click(screen.getByRole("button", { name: /Izvoz/i }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Izvezi kao CSV" }));
    fireEvent.click(screen.getByRole("button", { name: /Pokreni export/i }));

    await waitFor(() => expect(waitForExport).toHaveBeenCalledWith("doc-1"));
    expect(downloadExport).toHaveBeenCalledWith(
      "/exports/ready.csv",
      "ready.csv",
    );
    expect(screen.getByRole("status")).toHaveTextContent(
      "Eksport je zavr\u0161en i preuzet.",
    );
  });

  it("keeps failed export visible as an error and leaves the dialog available for retry", async () => {
    vi.mocked(generateExport).mockRejectedValue(
      new Error("NpgsqlException: password=secret; SQL timeout"),
    );

    renderToolbar();
    fireEvent.click(screen.getByRole("button", { name: /Izvoz/i }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Izvezi kao Excel" }));
    fireEvent.click(screen.getByRole("button", { name: /Pokreni export/i }));

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Eksport nije uspeo. Pokušajte ponovo.");
    expect(alert).not.toHaveTextContent(/NpgsqlException|password=secret|SQL timeout/i);
    expect(alert).not.toHaveClass("text-[var(--success)]");
    expect(screen.getByRole("dialog", { name: /Export Supplier test/i })).toBeInTheDocument();
  });

  it("does not claim preview opened when the response has no print URL", async () => {
    vi.mocked(requestPrintPreview).mockResolvedValue(operation({ printUrl: "   " }));

    renderToolbar();
    fireEvent.click(screen.getByRole("button", { name: /Izvoz/i }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Izvezi kao PDF" }));
    fireEvent.click(screen.getByRole("button", { name: /Otvori preview/i }));

    const warning = await screen.findByRole("status");
    expect(warning).toHaveTextContent(/nije otvoren.*validan link/i);
    expect(warning).not.toHaveTextContent(/otvoren u novom tabu/i);
    expect(window.open).not.toHaveBeenCalled();
    expect(screen.getByRole("dialog", { name: /Export Supplier test/i })).toBeInTheDocument();
  });

  it("does not claim preview opened when the browser blocks the new window", async () => {
    vi.mocked(window.open).mockReturnValueOnce(null);
    vi.mocked(requestPrintPreview).mockResolvedValue(operation({ printUrl: "/print-preview/blocked" }));

    renderToolbar();
    fireEvent.click(screen.getByRole("button", { name: /Izvoz/i }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Izvezi kao PDF" }));
    fireEvent.click(screen.getByRole("button", { name: /Otvori preview/i }));

    const warning = await screen.findByRole("status");
    expect(warning).toHaveTextContent(/novi prozor blokiran/i);
    expect(warning).not.toHaveTextContent(/otvoren u novom tabu/i);
    expect(screen.getByRole("dialog", { name: /Export Supplier test/i })).toBeInTheDocument();
  });

  it("does not claim async export was downloaded without a completed artifact", async () => {
    vi.mocked(generateExport).mockResolvedValue(
      operation({ isAsync: true, documentId: "doc-incomplete", status: "queued" }),
    );
    vi.mocked(waitForExport).mockResolvedValue(
      status({ status: "completed", downloadUrl: "   " }),
    );

    renderToolbar();
    fireEvent.click(screen.getByRole("button", { name: /Izvoz/i }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Izvezi kao CSV" }));
    fireEvent.click(screen.getByRole("button", { name: /Pokreni export/i }));

    const warning = await screen.findByRole("status");
    expect(warning).toHaveTextContent(/nije dostupan za preuzimanje/i);
    expect(warning).not.toHaveTextContent(/završen i preuzet/i);
    expect(downloadExport).not.toHaveBeenCalled();
    expect(screen.getByRole("dialog", { name: /Export Supplier test/i })).toBeInTheDocument();
  });

  it("keeps malformed async completion status incomplete", async () => {
    vi.mocked(generateExport).mockResolvedValue(
      operation({ isAsync: true, documentId: "doc-malformed", status: "queued" }),
    );
    vi.mocked(waitForExport).mockResolvedValue(
      status({ status: { unexpected: "object" } as never, downloadUrl: "/exports/unsafe.csv" }),
    );

    renderToolbar();
    fireEvent.click(screen.getByRole("button", { name: /Izvoz/i }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Izvezi kao CSV" }));
    fireEvent.click(screen.getByRole("button", { name: /Pokreni export/i }));

    const warning = await screen.findByRole("status");
    expect(warning).toHaveTextContent(/nije dostupan za preuzimanje/i);
    expect(downloadExport).not.toHaveBeenCalled();
  });

  it("keeps a timed out async export in an error state", async () => {
    vi.mocked(generateExport).mockResolvedValue(
      operation({ isAsync: true, documentId: "doc-timeout", status: "queued" }),
    );
    vi.mocked(waitForExport).mockRejectedValue(
      new Error("Export jos nije spreman nakon dozvoljenog vremena"),
    );

    renderToolbar();
    fireEvent.click(screen.getByRole("button", { name: /Izvoz/i }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Izvezi kao CSV" }));
    fireEvent.click(screen.getByRole("button", { name: /Pokreni export/i }));

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent(/Export jos nije spreman/i);
    expect(alert).not.toHaveTextContent(/završen|preuzet/i);
    expect(downloadExport).not.toHaveBeenCalled();
    expect(screen.getByRole("dialog", { name: /Export Supplier test/i })).toBeInTheDocument();
  });

  it("keeps a terminal sync response without an artifact visibly incomplete", async () => {
    vi.mocked(generateExport).mockResolvedValue(
      operation({ status: "completed", downloadUrl: null }),
    );

    renderToolbar();
    fireEvent.click(screen.getByRole("button", { name: /Izvoz/i }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Izvezi kao CSV" }));
    fireEvent.click(screen.getByRole("button", { name: /Pokreni export/i }));

    const warning = await screen.findByRole("status");
    expect(warning).toHaveTextContent(/nije vratio validan dokument/i);
    expect(warning).not.toHaveTextContent(/pokrenut|preuzet/i);
    expect(screen.getByRole("dialog", { name: /Export Supplier test/i })).toBeInTheDocument();
  });
});
