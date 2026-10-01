import { useEffect, useRef, type RefObject } from "react";

type UseDialogA11yOptions = {
  isOpen: boolean;
  onClose: () => void;
  containerRef: RefObject<HTMLElement | null>;
  initialFocusRef?: RefObject<HTMLElement | null>;
  returnFocusRef?: RefObject<HTMLElement | null>;
};

export function useDialogA11y({
  isOpen,
  onClose,
  containerRef,
  initialFocusRef,
  returnFocusRef,
}: UseDialogA11yOptions) {
  const previouslyFocusedRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    previouslyFocusedRef.current = document.activeElement as HTMLElement | null;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onClose();
      }
    };

    const handleTabTrap = (event: KeyboardEvent) => {
      if (event.key !== "Tab" || !containerRef.current) {
        return;
      }

      const focusable = containerRef.current.querySelectorAll<HTMLElement>(
        'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
      );

      if (focusable.length === 0) {
        event.preventDefault();
        containerRef.current.focus();
        return;
      }

      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      const active = document.activeElement as HTMLElement | null;

      if (event.shiftKey && active === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && active === last) {
        event.preventDefault();
        first.focus();
      }
    };

    document.addEventListener("keydown", handleEscape);
    document.addEventListener("keydown", handleTabTrap);

    const frame = window.requestAnimationFrame(() => {
      if (initialFocusRef?.current) {
        initialFocusRef.current.focus();
        return;
      }

      containerRef.current?.focus();
    });

    return () => {
      window.cancelAnimationFrame(frame);
      document.removeEventListener("keydown", handleEscape);
      document.removeEventListener("keydown", handleTabTrap);
      document.body.style.overflow = previousOverflow;
      const focusTarget = returnFocusRef?.current ?? previouslyFocusedRef.current;
      focusTarget?.focus();
    };
  }, [containerRef, initialFocusRef, isOpen, onClose, returnFocusRef]);
}
