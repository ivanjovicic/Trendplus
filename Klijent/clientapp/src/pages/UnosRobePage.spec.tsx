import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import UnosRobePage from "./UnosRobePage";

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("UnosRobePage SEF XML pilot entry", () => {
  it("keeps manual receipt entry separate and clears a selected invoice when leaving the pilot", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => [{ id: 1, naziv: "Sintetički dobavljač" }],
    });
    vi.stubGlobal("fetch", fetchMock);

    render(<MemoryRouter><UnosRobePage /></MemoryRouter>);

    expect(await screen.findByText("Unesite račun ili otpremnicu")).toBeInTheDocument();
    const xmlButton = screen.getByRole("button", { name: "Uvezi iz XML-a (pilot)" });
    fireEvent.click(xmlButton);

    expect(screen.getByLabelText("SEF XML fajl")).toBeInTheDocument();
    expect(screen.queryByText("Unesite račun ili otpremnicu")).not.toBeInTheDocument();
    expect(screen.queryByText("Istorija prijema robe")).not.toBeInTheDocument();
    expect(xmlButton).toHaveAttribute("aria-pressed", "true");

    fireEvent.change(screen.getByLabelText("SEF XML fajl"), {
      target: { files: [new File(["<Invoice />"], "synthetic.xml", { type: "application/xml" })] },
    });
    expect(screen.getByText("Izabrano: synthetic.xml")).toBeInTheDocument();
    const prompt = vi.spyOn(window, "prompt").mockReturnValue(null);
    fireEvent.click(screen.getByRole("button", { name: "Pregledaj XML" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("XML nije poslat na pregled.");
    expect(prompt).toHaveBeenCalledWith("Unesite admin ključ za read-only SEF XML pregled.");

    fireEvent.click(screen.getByRole("button", { name: "Ručni unos" }));
    expect(screen.getByText("Unesite račun ili otpremnicu")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Uvezi iz XML-a (pilot)" }));

    expect(screen.queryByText("Izabrano: synthetic.xml")).not.toBeInTheDocument();
    expect(screen.getByLabelText("SEF XML fajl")).toBeInTheDocument();
    const requestPaths = fetchMock.mock.calls.map(([url]) => new URL(String(url), window.location.origin).pathname);
    expect(requestPaths).toContain("/api/dobavljaci");
    expect(fetchMock.mock.calls.every(([, init]) => !init?.method || init.method.toUpperCase() === "GET")).toBe(true);
  });
});
