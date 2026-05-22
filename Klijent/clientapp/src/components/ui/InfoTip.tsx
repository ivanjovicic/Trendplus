import "./InfoTip.css";
import { type KeyboardEvent, useCallback, useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";

const SHOW_DELAY_MS = 150;
const HIDE_ANIM_MS = 180;
const MAX_TOOLTIP_W = 264;
const EDGE_MARGIN = 12;

type Pos = { top: number; left: number; below: boolean };

function computePos(rect: DOMRect): Pos {
  const vw = window.innerWidth;
  const tooltipWidth = Math.min(MAX_TOOLTIP_W, Math.max(180, vw - EDGE_MARGIN * 2));
  const halfTooltipWidth = tooltipWidth / 2;
  let left = rect.left + rect.width / 2;
  left = Math.max(halfTooltipWidth + EDGE_MARGIN, Math.min(left, vw - halfTooltipWidth - EDGE_MARGIN));
  const below = rect.top < 120;
  const top = below ? rect.bottom + 8 : rect.top - 8;
  return { top, left, below };
}

export default function InfoTip({ text }: { text: string }) {
  const tooltipId = useId();
  const triggerRef = useRef<HTMLSpanElement>(null);
  const showTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const hideTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const [pos, setPos] = useState<Pos | null>(null);
  const [entering, setEntering] = useState(false);

  const clearTimers = useCallback(() => {
    if (showTimer.current !== null) {
      clearTimeout(showTimer.current);
      showTimer.current = null;
    }

    if (hideTimer.current !== null) {
      clearTimeout(hideTimer.current);
      hideTimer.current = null;
    }
  }, []);

  const show = useCallback(() => {
    clearTimers();
    showTimer.current = setTimeout(() => {
      if (!triggerRef.current) {
        return;
      }

      const nextPos = computePos(triggerRef.current.getBoundingClientRect());
      setPos(nextPos);
      requestAnimationFrame(() => requestAnimationFrame(() => setEntering(true)));
    }, SHOW_DELAY_MS);
  }, [clearTimers]);

  const hide = useCallback(() => {
    clearTimers();
    setEntering(false);
    hideTimer.current = setTimeout(() => setPos(null), HIDE_ANIM_MS);
  }, [clearTimers]);

  const toggle = useCallback(() => {
    if (entering) {
      hide();
      return;
    }

    show();
  }, [entering, show, hide]);

  const onTriggerKeyDown = useCallback((event: KeyboardEvent<HTMLSpanElement>) => {
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      toggle();
    }
  }, [toggle]);

  useEffect(() => {
    if (!entering) {
      return;
    }

    const onKey = (event: globalThis.KeyboardEvent) => {
      if (event.key === "Escape") {
        hide();
        triggerRef.current?.focus();
      }
    };

    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [entering, hide]);

  useEffect(() => {
    if (!pos) {
      return;
    }

    window.addEventListener("scroll", hide, { passive: true, capture: true });
    window.addEventListener("resize", hide, { passive: true });

    return () => {
      window.removeEventListener("scroll", hide, { capture: true });
      window.removeEventListener("resize", hide);
    };
  }, [pos, hide]);

  useEffect(() => () => clearTimers(), [clearTimers]);

  return (
    <>
      <span
        ref={triggerRef}
        className="info-tip"
        role="button"
        tabIndex={0}
        aria-describedby={entering ? tooltipId : undefined}
        aria-label="Više informacija"
        onMouseEnter={show}
        onMouseLeave={hide}
        onFocus={show}
        onBlur={hide}
        onClick={toggle}
        onKeyDown={onTriggerKeyDown}
      >
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
      </span>

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
