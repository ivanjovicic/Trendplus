import { Component, type ReactNode } from "react";
import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ChunkLoadError } from "../../utils/chunkLoadRecovery";
import { ErrorBoundary } from "../ErrorBoundary";

class BrokenRoute extends Component {
  public render(): ReactNode {
    throw new ChunkLoadError("route chunk unavailable");
  }
}

describe("ErrorBoundary chunk recovery", () => {
  it.each([
    ["typed chunk error", BrokenRoute],
    ["legacy undefined default signature", class extends Component {
      public render(): ReactNode { throw new TypeError("Cannot read properties of undefined (reading 'default')"); }
    }],
  ])("shows the application refresh prompt for a %s", (_label, BrokenComponent) => {
    const errorSpy = vi.spyOn(console, "error").mockImplementation(() => undefined);
    render(<ErrorBoundary><BrokenComponent /></ErrorBoundary>);

    expect(screen.getByText("Nova verzija aplikacije je dostupna")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Osveži aplikaciju" })).toBeInTheDocument();
    expect(screen.queryByText("Nešto je pošlo naopako")).not.toBeInTheDocument();
    errorSpy.mockRestore();
  });
});
