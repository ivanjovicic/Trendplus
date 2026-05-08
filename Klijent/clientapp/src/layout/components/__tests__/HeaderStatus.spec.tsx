import React from "react";
import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import HeaderStatus from "../HeaderStatus";
import { BackendStatusContext } from "../../../context/BackendStatusContext";

describe("HeaderStatus", () => {
  it("does not render duplicate configuration controls in header", () => {
    render(
      <BackendStatusContext.Provider
        value={{
          status: "up",
          online: true,
          checking: false,
          lastCheckedAt: Date.now(),
          lastReachableAt: Date.now(),
          lastUnavailableAt: null,
          lastError: null,
          hadConfirmedOutage: false,
          recoveryNoticeVisible: false,
          recoveryNoticeAt: null,
          providerState: {
            phase: "primary_ready",
            activeHost: "primary",
            reason: null,
            updatedAt: Date.now(),
            retryAfterMs: null,
          },
        }}
      >
        <HeaderStatus onOpenMobileNav={() => {}} />
      </BackendStatusContext.Provider>
    );

    expect(screen.queryByText(/API ping:/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Workeri:/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/^Teme$/i)).not.toBeInTheDocument();
  });
});
