import { useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";

export default function InfoTip({ text }: { text: string }) {
  const [rect, setRect] = useState<DOMRect | null>(null);
  const ref = useRef<HTMLSpanElement>(null);

  const show = useCallback(() => {
    if (ref.current) setRect(ref.current.getBoundingClientRect());
  }, []);

  const hide = useCallback(() => setRect(null), []);

  useEffect(() => {
    if (!rect) return;
    const dismiss = () => setRect(null);
    window.addEventListener("scroll", dismiss, { passive: true, capture: true });
    window.addEventListener("resize", dismiss, { passive: true });
    return () => {
      window.removeEventListener("scroll", dismiss, { capture: true });
      window.removeEventListener("resize", dismiss);
    };
  }, [rect]);

  return (
    <>
      <span
        ref={ref}
        className="info-tip"
        role="note"
        tabIndex={0}
        aria-label={text}
        onMouseEnter={show}
        onMouseLeave={hide}
        onFocus={show}
        onBlur={hide}
      >
        i
      </span>
      {rect &&
        createPortal(
          <span
            className="info-tip-portal"
            style={{
              position: "fixed",
              top: rect.top - 8,
              left: rect.left + rect.width / 2,
              transform: "translateX(-50%) translateY(-100%)",
            }}
          >
            {text}
          </span>,
          document.body,
        )}
    </>
  );
}
