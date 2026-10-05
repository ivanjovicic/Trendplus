import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import WorkerControlFlag from "../WorkerControlFlag";
import RedisToggleFlag from "../RedisToggleFlag";
import ApiPingFlag from "../ApiPingFlag";
import { PingControlProvider } from "../../context/PingControlContext";
import { disableWorkers, getWorkersHealth } from "../../services/workersApi";
import { fetchWithTimeout } from "../../utils/fetchWithTimeout";

vi.mock("../../services/workersApi", () => ({
  disableWorkers: vi.fn(),
  enableWorkers: vi.fn(),
  getWorkersHealth: vi.fn(),
}));

vi.mock("../../utils/fetchWithTimeout", () => ({ fetchWithTimeout: vi.fn() }));
vi.mock("../../utils/apiUrl", () => ({ apiUrl: (path: string) => path }));

const workersHealth = {
  totalWorkers: 2,
  healthyWorkers: 2,
  runningWorkers: 2,
  errorWorkers: 0,
  stoppedWorkers: 0,
  staleWorkers: 0,
  hasCriticalIssues: false,
  workers: [],
  workersEnabled: true,
  runtimeToggleAllowed: true,
};

function renderWithPing(children: React.ReactNode) {
  return render(<PingControlProvider>{children}</PingControlProvider>);
}

describe("operational header flags", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.localStorage.removeItem("trendplus:api-ping-enabled");
    vi.mocked(getWorkersHealth).mockResolvedValue(workersHealth);
    vi.mocked(fetchWithTimeout).mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ enabled: false, available: false }),
    } as Response);
  });

  it("keeps API polling preference browser-local and labels it clearly", () => {
    renderWithPing(<ApiPingFlag />);

    expect(screen.getByText(/Provera API-ja u ovom pregledaču: aktivna/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Pauziraj proveru API-ja u ovom pregledaču" }));

    expect(screen.getByText(/Provera API-ja u ovom pregledaču: pauzirana/i)).toBeInTheDocument();
    expect(window.localStorage.getItem("trendplus:api-ping-enabled")).toBe("0");
    expect(fetchWithTimeout).not.toHaveBeenCalled();
  });

  it("shows worker status without backend write buttons in status-only mode", async () => {
    renderWithPing(<WorkerControlFlag />);

    await screen.findByText("Workeri: 2/2");
    expect(screen.queryByRole("button", { name: /uključi|isključi radnike/i })).not.toBeInTheDocument();
  });

  it("requires confirmation and an admin key before changing worker runtime state", async () => {
    renderWithPing(<WorkerControlFlag showControls />);

    await screen.findByText("Workeri: 2/2");
    fireEvent.click(screen.getByRole("button", { name: "Isključi radnike" }));
    const dialog = screen.getByRole("dialog", { name: "Isključi radnike" });
    const confirm = within(dialog).getByRole("button", { name: "Isključi radnike" });
    expect(confirm).toBeDisabled();
    fireEvent.click(within(dialog).getByRole("button", { name: "Otkaži" }));
    expect(disableWorkers).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "Isključi radnike" }));
    const reopenedDialog = screen.getByRole("dialog", { name: "Isključi radnike" });
    fireEvent.change(within(reopenedDialog).getByLabelText("Admin ključ"), { target: { value: "approved-key" } });
    fireEvent.click(within(reopenedDialog).getByRole("button", { name: "Isključi radnike" }));

    await waitFor(() => expect(disableWorkers).toHaveBeenCalledWith("approved-key"));
    expect(disableWorkers).toHaveBeenCalledTimes(1);
  });

  it("requires confirmation and an admin key before changing Redis state", async () => {
    renderWithPing(<RedisToggleFlag showControls />);

    await screen.findByText("Redis: isključen");
    fireEvent.click(screen.getByRole("button", { name: "Uključi Redis" }));
    const dialog = screen.getByRole("dialog", { name: "Uključi Redis" });
    const confirm = within(dialog).getByRole("button", { name: "Uključi Redis" });
    expect(confirm).toBeDisabled();
    fireEvent.click(within(dialog).getByRole("button", { name: "Otkaži" }));
    expect(fetchWithTimeout).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole("button", { name: "Uključi Redis" }));
    const reopenedDialog = screen.getByRole("dialog", { name: "Uključi Redis" });
    fireEvent.change(within(reopenedDialog).getByLabelText("Admin ključ"), { target: { value: "approved-key" } });
    fireEvent.click(within(reopenedDialog).getByRole("button", { name: "Uključi Redis" }));

    await waitFor(() => expect(fetchWithTimeout).toHaveBeenCalledWith(
      "/api/redis/toggle",
      expect.objectContaining({ method: "POST", headers: { "X-Admin-Key": "approved-key" } }),
      60_000,
    ));
    expect(fetchWithTimeout).toHaveBeenCalledTimes(2);
  });
});
