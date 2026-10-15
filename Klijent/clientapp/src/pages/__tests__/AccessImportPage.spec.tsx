import { fireEvent, render, screen, waitFor } from "@testing-library/react";
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

import AccessImportPage, { ACCESS_IMPORT_ADMIN_KEY_STORAGE } from "../AccessImportPage";
import { getAccessImportBatches, getAccessImportRuntimeStatus, type AccessImportBatchDto } from "../../services/accessImportApi";

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
    window.sessionStorage.clear();
  });

  it("attaches to an import that is already running on the server after a reload", async () => {
    vi.mocked(getAccessImportBatches).mockResolvedValue([runningBatch]);
    renderPage();

    expect(await screen.findByText("Batch #77")).toBeInTheDocument();
    expect(screen.getByText("Tabela: Prodaja")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Otkaži import" })).toBeEnabled();
    expect(vi.mocked(getAccessImportBatches).mock.calls[0][1]).toBe("mount");
  });

  it("keeps showing the last known history and a retry hint when the history is temporarily unavailable", async () => {
    vi.mocked(getAccessImportBatches).mockRejectedValue(new Error("Istorija importa trenutno nije dostupna jer baza sporo odgovara. Pokušaj ponovo za nekoliko sekundi."));
    renderPage();

    expect(await screen.findByText(/Istorija importa trenutno nije dostupna/)).toBeInTheDocument();
    expect(screen.queryByText("Pregled import istorije zahteva važeći admin key.")).not.toBeInTheDocument();
  });

  it("asks for the admin key instead of silently showing an idle screen when history is protected", async () => {
    vi.mocked(getAccessImportBatches).mockRejectedValue(new Error("401 Unauthorized"));
    renderPage();

    expect(await screen.findByRole("button", { name: "Proveri status importa" })).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByText("Pregled import istorije zahteva važeći admin key.")).not.toBeInTheDocument());
  });
});

describe("AccessImportPage admin key session (RQ675)", () => {
  beforeEach(() => {
    vi.mocked(getAccessImportRuntimeStatus).mockResolvedValue({ available: true, platform: "linux", missingDependencies: [], detail: null } as never);
    vi.mocked(getAccessImportBatches).mockReset();
    window.sessionStorage.clear();
  });

  it("uses the key stored for this tab session after a reload and attaches to the running import", async () => {
    window.sessionStorage.setItem(ACCESS_IMPORT_ADMIN_KEY_STORAGE, "secret-key");
    vi.mocked(getAccessImportBatches).mockResolvedValue([runningBatch]);
    renderPage();

    expect(await screen.findByText("Batch #77")).toBeInTheDocument();
    expect(vi.mocked(getAccessImportBatches).mock.calls[0][2]).toBe("secret-key");
    expect(screen.getByRole("button", { name: "Obriši ključ" })).toBeInTheDocument();
  });

  it("takes the key from a masked field, keeps it only in sessionStorage and can clear it", async () => {
    vi.mocked(getAccessImportBatches).mockRejectedValueOnce(new Error("401 Unauthorized")).mockResolvedValue([]);
    renderPage();

    const input = await screen.findByLabelText("Admin key", { selector: "input" });
    expect(input).toHaveAttribute("type", "password");
    fireEvent.change(input, { target: { value: "  typed-key  " } });
    fireEvent.click(screen.getByRole("button", { name: "Sačuvaj za ovu sesiju" }));

    await waitFor(() => expect(window.sessionStorage.getItem(ACCESS_IMPORT_ADMIN_KEY_STORAGE)).toBe("typed-key"));
    expect(window.localStorage.getItem(ACCESS_IMPORT_ADMIN_KEY_STORAGE)).toBeNull();
    await waitFor(() => expect(vi.mocked(getAccessImportBatches).mock.calls.some((call) => call[2] === "typed-key")).toBe(true));
    expect(input).toHaveValue("");

    fireEvent.click(screen.getByRole("button", { name: "Obriši ključ" }));
    expect(window.sessionStorage.getItem(ACCESS_IMPORT_ADMIN_KEY_STORAGE)).toBeNull();
  });

  it("clears a stored key that the server rejects", async () => {
    window.sessionStorage.setItem(ACCESS_IMPORT_ADMIN_KEY_STORAGE, "stale-key");
    vi.mocked(getAccessImportBatches).mockRejectedValue(new Error("401 Unauthorized"));
    renderPage();

    await waitFor(() => expect(window.sessionStorage.getItem(ACCESS_IMPORT_ADMIN_KEY_STORAGE)).toBeNull());
    expect(await screen.findByRole("button", { name: "Proveri status importa" })).toBeInTheDocument();
  });
});
