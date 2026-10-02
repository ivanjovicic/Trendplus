import { useEffect, useState, type RefObject } from "react";

type SearchPanelStyle = {
  position: "fixed";
  left: number;
  top: number;
  width: number;
  maxHeight: number;
};

/** Keep mobile autocomplete results inside the visible viewport, including when a keyboard resizes it. */
export function useViewportSearchPanel(
  anchorRef: RefObject<HTMLElement | null>,
  open: boolean,
): SearchPanelStyle | undefined {
  const [style, setStyle] = useState<SearchPanelStyle>();

  useEffect(() => {
    if (!open || typeof window.matchMedia !== "function" || !window.matchMedia("(pointer: coarse) and (hover: none)").matches) {
      setStyle(undefined);
      return;
    }

    const update = () => {
      const anchor = anchorRef.current;
      if (!anchor) return;

      const bounds = anchor.getBoundingClientRect();
      const viewport = window.visualViewport;
      const viewportTop = viewport?.offsetTop ?? 0;
      const viewportBottom = viewportTop + (viewport?.height ?? window.innerHeight);
      const gap = 8;
      const below = Math.max(0, viewportBottom - bounds.bottom - gap);
      const above = Math.max(0, bounds.top - viewportTop - gap);
      const placeAbove = below < 144 && above > below;
      const maxHeight = Math.min(320, placeAbove ? above : below);

      setStyle({
        position: "fixed",
        left: Math.max(8, bounds.left),
        top: placeAbove ? Math.max(viewportTop + 4, bounds.top - maxHeight - gap) : bounds.bottom + gap,
        width: Math.min(bounds.width, window.innerWidth - Math.max(16, bounds.left + 8)),
        maxHeight,
      });
    };

    update();
    const viewport = window.visualViewport;
    window.addEventListener("resize", update);
    window.addEventListener("scroll", update, true);
    viewport?.addEventListener("resize", update);
    viewport?.addEventListener("scroll", update);
    return () => {
      window.removeEventListener("resize", update);
      window.removeEventListener("scroll", update, true);
      viewport?.removeEventListener("resize", update);
      viewport?.removeEventListener("scroll", update);
    };
  }, [anchorRef, open]);

  return style;
}
