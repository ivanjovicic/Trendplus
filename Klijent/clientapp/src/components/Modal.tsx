import React, { useEffect, useRef } from "react";

export interface ModalProps {
    isOpen: boolean;
    onClose: () => void;
    title: string;
    children: React.ReactNode;
    footer?: React.ReactNode;
    size?: "sm" | "md" | "lg";
}

export default function Modal({ isOpen, onClose, title, children, footer, size = "md" }: ModalProps) {
    const modalRef = useRef<HTMLDivElement>(null);
    const closeButtonRef = useRef<HTMLButtonElement>(null);
    const previouslyFocusedRef = useRef<HTMLElement | null>(null);

    useEffect(() => {
        if (!isOpen) return;

        previouslyFocusedRef.current = document.activeElement as HTMLElement | null;

        const handleEscape = (e: KeyboardEvent) => {
            if (e.key === "Escape") onClose();
        };

        const handleTabTrap = (e: KeyboardEvent) => {
            if (e.key !== "Tab" || !modalRef.current) return;

            const focusable = modalRef.current.querySelectorAll<HTMLElement>(
                'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
            );

            if (focusable.length === 0) {
                e.preventDefault();
                modalRef.current.focus();
                return;
            }

            const first = focusable[0];
            const last = focusable[focusable.length - 1];
            const active = document.activeElement as HTMLElement | null;

            if (e.shiftKey && active === first) {
                e.preventDefault();
                last.focus();
            } else if (!e.shiftKey && active === last) {
                e.preventDefault();
                first.focus();
            }
        };

        document.addEventListener("keydown", handleEscape);
        document.addEventListener("keydown", handleTabTrap);
        document.body.style.overflow = "hidden";
        window.setTimeout(() => closeButtonRef.current?.focus(), 0);

        return () => {
            document.removeEventListener("keydown", handleEscape);
            document.removeEventListener("keydown", handleTabTrap);
            document.body.style.overflow = "unset";
            previouslyFocusedRef.current?.focus();
        };
    }, [isOpen, onClose]);

    if (!isOpen) return null;

    const sizeStyles = {
        sm: { minWidth: 320, maxWidth: 400 },
        md: { minWidth: 400, maxWidth: 600 },
        lg: { minWidth: 600, maxWidth: 800 },
    };

    return (
        <div className="modal-root">
            {/* Backdrop */}
            <div className="modal-backdrop" onClick={onClose} />

            {/* Modal Content */}
            <div
                ref={modalRef}
                role="dialog"
                aria-modal="true"
                aria-labelledby="modal-title"
                tabIndex={-1}
                className="modal-content"
                style={{ ...sizeStyles[size], maxHeight: "90vh" }}
            >
                {/* Header */}
                <div className="modal-header">
                    <h3 id="modal-title" style={{ fontSize: "1.25rem", fontWeight: 600, margin: 0 }}>{title}</h3>
                    <button ref={closeButtonRef} onClick={onClose} className="modal-close-button" aria-label="Zatvori">x</button>
                </div>

                {/* Body */}
                <div className="modal-body">{children}</div>

                {/* Footer */}
                {footer && <div className="modal-footer">{footer}</div>}
            </div>
        </div>
    );
}
