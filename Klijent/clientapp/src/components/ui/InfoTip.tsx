import "./InfoTip.css";
import { useCallback, useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";

const SHOW_DELAY_MS = 150;
const HIDE_ANIM_MS = 180;
const HALF_TOOLTIP_W = 132; // half of max-width (264px)
const EDGE_MARGIN = 12;

type Pos = { top: number; left: number; below: boolean };

function computePos(rect: DOMRect): Pos {
  const vw = window.innerWidth;
  let left = rect.left + rect.width / 2;
  // Clamp so tooltip never bleeds off-screen
  left = Math.max(HALF_TOOLTIP_W + EDGE_MARGIN, Math.min(left, vw - HALF_TOOLTIP_W - EDGE_MARGIN));
  const below = rect.top < 120;
  const top = below ? rect.bottom + 8 : rect.top - 8;
  return { top, left, below };
}

export default function InfoTip({ text }: { text: string }) {
  const tooltipId = useId();
  const triggerRef = useRef<HTMLButtonElement>(null);
  const showTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const hideTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const [pos, setPos] = useState<Pos | null>(null);
  const [entering, setEntering] = useState(false);

  const clearTimers = useCallback(() => {
    if (showTimer.current !== null) { clearTimeout(showTimer.current); showTimer.current = null; }
    if (hideTimer.current !== null) { clearTimeout(hideTimer.current); hideTimer.current = null; }
  }, []);

  const show = useCallback(() => {
    clearTimers();
    showTimer.current = setTimeout(() => {
      if (!triggerRef.current) return;
      const p = computePos(triggerRef.current.getBoundingClientRect());
      setPos(p);
      // next frame so the element is in DOM before we trigger the transition
      requestAnimationFrame(() => requestAnimationFrame(() => setEntering(true)));
    }, SHOW_DELAY_MS);
  }, [clearTimers]);

  const hide = useCallback(() => {
    clearTimers();
    setEntering(false);
    hideTimer.current = setTimeout(() => setPos(null), HIDE_ANIM_MS);
  }, [clearTimers]);

  const toggle = useCallback(() => {
    if (entering) hide(); else show();
  }, [entering, show, hide]);

  // ESC closes
  useEffect(() => {
    if (!entering) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") { hide(); triggerRef.current?.focus(); } };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [entering, hide]);

  // Scroll / resize close
  useEffect(() => {
    if (!pos) return;
    window.addEventListener("scroll", hide, { passive: true, capture: true });
    window.addEventListener("resize", hide, { passive: true });
    return () => {
      window.removeEventListener("scroll", hide, { capture: true });
      window.removeEventListener("resize", hide);
    };
  }, [pos, hide]);

  // Cleanup on unmount
  useEffect(() => () => clearTimers(), [clearTimers]);

  return (
    <>
      <button
        ref={triggerRef}
        type="button"
        className="info-tip"
        aria-describedby={entering ? tooltipId : undefined}
        aria-label="Više informacija"
        onMouseEnter={show}
        onMouseLeave={hide}
        onFocus={show}
        onBlur={hide}
        onClick={toggle}
      >
        {/* Info circle SVG — always centered via flex parent */}
        <svg
          viewBox="0 0 16 16"
          fill="currentColor"
          aria-hidden="true"
          focusable="false"
          width="14"
          height="14"
        >
          <path d="M8 1a7 7 0 100 14A7 7 0 008 1zM7.25 4.5a.75.75 0 111.5 0 .75.75 0 01-1.5 0zM7.25 7h1.5v4.5h-1.5V7z" />
        </svg>
      </button>

      {pos &&
        createPortal(
          <span
            id={tooltipId}
            role="tooltip"
            className={[
              "info-tip-portal",
              pos.below ? "info-tip-portal--below" : "",
              entering ? "info-tip-portal--visible" : "",
            ]
              .filter(Boolean)
              .join(" ")}
            style={{ top: pos.top, left: pos.left }}
          >
            {text}
          </span>,
          document.body,
        )}
    </>
  );
}
