import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("../../services/accessImportApi", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../services/accessImportApi")>();
  return {
    ...actual,
    getAccessImportBatches: vi.fn(),
    getAccessImportRuntimeStatus: vi.fn(),
    runAccessImport: vi.fn(),
    previewAccessImport: vi.fn(),
    cancelAccessImportBatch: vi.fn(),
  };
});

import AccessImportPage from "../AccessImportPage";
import { getAccessImportBatches, type AccessImportBatchDto } from "../../services/accessImportApi";

const runningBatch: AccessImportBatchDto = {
  id: 77, sourceSystem: "access", sourceFileName: "trend.accdb", queuedAtUtc: new Date(Date.now() - 60_000).toISOString(),
  startedAtUtc: new Date(Date.now() - 60_000).toISOString(), completedAtUtc: null, lastHeartbeatUtc: new Date().toISOString(),
  status: "running", currentStep: "import", currentTable: "Prodaja", progressPercent: 40, summaryJson: null, errorMessage: null,
  durationSeconds: null, totalImported: 0, totalUpdated: 0, totalErrors: 0, dataOrigin: "access",
};

function renderPage() {
  return render(<MemoryRouter><AccessImportPage /></MemoryRouter>);
}

describe("AccessImportPage active run detection", () => {
  beforeEach(() => {
    vi.mocked(getAccessImportBatches).mockReset();
  });

  it("attaches to an import that is already running on the server after a reload", async () => {
    vi.mocked(getAccessImportBatches).mockResolvedValue([runningBatch]);
    renderPage();

    expect(await screen.findByText("Batch #77")).toBeInTheDocument();
    expect(screen.getByText("Tabela: Prodaja")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Otkaži import" })).toBeEnabled();
    expect(vi.mocked(getAccessImportBatches).mock.calls[0][1]).toBe("mount");
  });

  it("asks for the admin key instead of silently showing an idle screen when history is protected", async () => {
    vi.mocked(getAccessImportBatches).mockRejectedValue(new Error("401 Unauthorized"));
    renderPage();

    expect(await screen.findByRole("button", { name: "Proveri status importa" })).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByText("Pregled import istorije zahteva važeći admin key.")).not.toBeInTheDocument());
  });
});
