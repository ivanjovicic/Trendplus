import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import ExperimentalInsightStudioRoute from "./ExperimentalInsightStudioRoute";

function renderRoute() {
  return render(
    <MemoryRouter initialEntries={["/analytics/insight-studio"]}>
      <Routes>
        <Route path="/analytics/insight-studio" element={<ExperimentalInsightStudioRoute><div>Insight Studio content</div></ExperimentalInsightStudioRoute>} />
        <Route path="/analytics" element={<div>Analytics home</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("ExperimentalInsightStudioRoute", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllEnvs();
  });

  it("keeps the direct route closed unless the owner deployment flag is enabled", () => {
    vi.stubEnv("VITE_ENABLE_EXPERIMENTAL_INSIGHT_STUDIO", "false");

    renderRoute();

    expect(screen.getByText("Analytics home")).toBeInTheDocument();
    expect(screen.queryByText("Insight Studio content")).not.toBeInTheDocument();
  });

  it("renders the route with an explicit uncertified and stale-data notice when enabled", () => {
    vi.stubEnv("VITE_ENABLE_EXPERIMENTAL_INSIGHT_STUDIO", "true");

    renderRoute();

    expect(screen.getByRole("status")).toHaveTextContent("nije sertifikovan");
    expect(screen.getByRole("status")).toHaveTextContent("zastareli, nepotpuni");
    expect(screen.getByText("Insight Studio content")).toBeInTheDocument();
  });
});
